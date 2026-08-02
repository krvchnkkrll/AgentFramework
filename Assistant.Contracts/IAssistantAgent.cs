using Assistant.Contracts.Models;

namespace Assistant.Contracts;

/// <summary>
/// Ассистент, который отвечает пользователю. Единственная точка, через которую Application
/// разговаривает с LLM — про Microsoft Agent Framework, сессии и IChatClient слой Application не знает.
/// </summary>
public interface IAssistantAgent
{
    /// <summary>
    /// Генерирует ответ и стримит его кусками. Последним событием приходит
    /// <see cref="AssistantUpdateKindEnum.SessionState"/> — его надо сохранить рядом с чатом
    /// и вернуть в следующем запросе через <see cref="AssistantRunRequest.SessionState"/>.
    /// Без этого агент на каждый ход забывает сжатую историю, todo-лист, режим и подтверждения.
    /// </summary>
    IAsyncEnumerable<AssistantStreamUpdate> RunStreamingAsync(
        AssistantRunRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Придумывает название чата по первому сообщению пользователя.
    /// Никогда не кидает из-за проблем модели — в худшем случае вернёт обрезанный текст сообщения.
    /// </summary>
    Task<string> GenerateTitleAsync(string userText, CancellationToken cancellationToken = default);

    /// <summary>
    /// Скиллы, доступные для выбора в конструкторе агента. Читаются из папок со скиллами,
    /// поэтому список меняется без пересборки приложения.
    /// </summary>
    Task<IReadOnlyList<AssistantSkillInfo>> GetAvailableSkillsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Забывает собранного в память агента. Вызывается после правки или удаления агента
    /// в конструкторе: без этого до перезапуска приложения отвечал бы старый промпт.
    /// </summary>
    void EvictAgent(Guid agentId);

    /// <summary>
    /// Короткая форма для случаев, когда состояние сессии не сохраняется: агент собирает
    /// контекст заново из истории переписки.
    /// </summary>
    IAsyncEnumerable<AssistantStreamUpdate> RunStreamingAsync(
        string userText,
        IReadOnlyCollection<AssistantMessage> history,
        CancellationToken cancellationToken = default) =>
        RunStreamingAsync(
            new AssistantRunRequest { UserText = userText, History = history },
            cancellationToken);
}
