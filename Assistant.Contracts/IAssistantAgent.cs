using Assistant.Contracts.Models;

namespace Assistant.Contracts;

/// <summary>
/// Ассистент, который отвечает пользователю. Единственная точка, через которую Application
/// разговаривает с LLM — про Microsoft Agent Framework, сессии и IChatClient слой Application не знает.
/// </summary>
public interface IAssistantAgent
{
    /// <summary>
    /// Генерирует ответ на <paramref name="userText"/> с учётом истории переписки и стримит его кусками.
    /// История передаётся без последнего сообщения пользователя — оно приходит отдельным параметром.
    /// </summary>
    IAsyncEnumerable<AssistantStreamUpdate> RunStreamingAsync(
        string userText,
        IReadOnlyCollection<AssistantMessage> history,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Придумывает название чата по первому сообщению пользователя.
    /// Никогда не кидает из-за проблем модели — в худшем случае вернёт обрезанный текст сообщения.
    /// </summary>
    Task<string> GenerateTitleAsync(string userText, CancellationToken cancellationToken = default);
}
