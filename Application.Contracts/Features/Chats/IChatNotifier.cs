using Application.Contracts.Features.Chats.Responses;

namespace Application.Contracts.Features.Chats;

/// <summary>
/// Pushes chat events to whatever clients are listening (SignalR, in Web). Application
/// depends only on this abstraction — it doesn't know or care that SignalR exists.
/// </summary>
public interface IChatNotifier
{
    Task MessageStartedAsync(Guid chatId, Guid messageId, CancellationToken cancellationToken = default);

    Task MessageDeltaAsync(Guid chatId, Guid messageId, string delta, CancellationToken cancellationToken = default);

    Task MessageCompletedAsync(Guid chatId, MessageResponse message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Агент начал вызывать инструмент. Между этим событием и следующей дельтой текста может
    /// пройти много времени (скилл читается, файл ищется), и без индикации клиент выглядит зависшим.
    /// </summary>
    Task ToolCallStartedAsync(
        Guid chatId,
        Guid messageId,
        ToolCallResponse toolCall,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Инструмент отработал. <paramref name="error"/> не пустой, если он упал —
    /// агент в этом случае обычно продолжает отвечать, просто без его результата.
    /// </summary>
    Task ToolCallCompletedAsync(
        Guid chatId,
        Guid messageId,
        Guid toolCallId,
        string? error,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Генерация оборвалась, сообщения не будет. Без этого события клиент, который ждёт
    /// messageCompleted, висел бы вечно.
    /// </summary>
    Task MessageFailedAsync(Guid chatId, Guid messageId, string error, CancellationToken cancellationToken = default);

    Task ChatRenamedAsync(Guid chatId, string title, CancellationToken cancellationToken = default);
}
