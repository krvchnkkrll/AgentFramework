namespace Assistant.Agents;

/// <summary>
/// Ключи, под которыми мы кладём свои данные в <see cref="Microsoft.Agents.AI.AgentSessionStateBag"/>.
///
/// StateBag — это типизированный key-value стор внутри сессии агента. Всё, что в нём лежит,
/// уезжает в JSON при сериализации сессии и приезжает обратно при десериализации, вместе с
/// состоянием провайдеров (сжатая история, todo-лист, текущий режим, выданные подтверждения).
/// Поэтому сюда кладут контекст, который должен пережить перезапуск процесса, а не просто
/// объект в памяти.
/// </summary>
public static class AssistantSessionKeys
{
    /// <summary>Идентификатор пользователя, от имени которого идёт диалог.</summary>
    public const string UserId = "assistant.user_id";

    /// <summary>Идентификатор чата.</summary>
    public const string ConversationId = "assistant.conversation_id";
}
