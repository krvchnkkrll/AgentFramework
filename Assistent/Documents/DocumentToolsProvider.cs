using Assistant.Agents;
using Microsoft.Agents.AI;

namespace Assistant.Documents;

/// <summary>
/// Подкладывает агенту инструменты работы с документами того чата, в котором он сейчас отвечает.
///
/// Почему провайдером, а не обычными инструментами агента: агенты собираются один раз и живут
/// в кэше, а документы у каждого чата свои. Инструменты, привязанные к конкретному чату,
/// нельзя вшить в агента — их надо выдавать на каждый прогон. Ровно для этого у AIContextProvider
/// есть AIContext.Tools.
///
/// Если к чату ничего не приложено, ни инструментов, ни инструкций не добавляется —
/// обычный разговор не платит за эту возможность ни одним токеном.
/// </summary>
public sealed class DocumentToolsProvider(InMemoryDocumentStore store) : AIContextProvider
{
    /// <summary>
    /// Возвращает только свою добавку — фреймворк сольёт её с тем, что дали остальные
    /// провайдеры. Исходный контекст здесь не правим.
    /// </summary>
    protected override ValueTask<AIContext> ProvideAIContextAsync(
        InvokingContext context,
        CancellationToken cancellationToken)
    {
        var conversationId = ReadConversationId(context.Session);

        if (conversationId is not { } id)
            return ValueTask.FromResult(new AIContext());

        var documents = store.GetForConversation(id);

        if (documents.Count == 0)
            return ValueTask.FromResult(new AIContext());

        var instructions = string.Join(
            "\n",
            "К этому разговору приложены документы. Их текста у тебя нет — он слишком большой,",
            "чтобы поместиться в переписку. Работай с ними инструментами:",
            string.Join("\n", documents.Select(document =>
                $"- {document.FileName}: {document.LineCount} строк, {document.CharacterCount} символов")),
            string.Empty,
            "Порядок работы: document_search находит нужные места, document_read читает вокруг них.",
            "Никогда не выдумывай содержимое документа — если не прочитал, так и скажи.",
            "Читая частями, помни: прочитанный кусок — это ещё не весь документ.");

        return ValueTask.FromResult(new AIContext
        {
            Instructions = instructions,
            Tools = [.. new DocumentTools(store, id).Create()],
        });
    }

    /// <summary>
    /// Идентификатор чата кладёт в состояние сессии DefaultAgent перед прогоном —
    /// он же единственное, что связывает сессию агента с приложенными документами.
    /// </summary>
    private static Guid? ReadConversationId(AgentSession? session)
    {
        if (session is null)
            return null;

        return session.StateBag.TryGetValue<string>(AssistantSessionKeys.ConversationId, out var raw)
            && Guid.TryParse(raw, out var conversationId)
                ? conversationId
                : null;
    }
}
