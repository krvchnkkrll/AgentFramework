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

    Task ChatRenamedAsync(Guid chatId, string title, CancellationToken cancellationToken = default);
}
