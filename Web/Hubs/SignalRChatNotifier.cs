using Application.Contracts.Features.Chats;
using Application.Contracts.Features.Chats.Responses;
using Microsoft.AspNetCore.SignalR;

namespace Web.Hubs;

internal sealed class SignalRChatNotifier(IHubContext<ChatHub> hubContext) : IChatNotifier
{
    public Task MessageStartedAsync(Guid chatId, Guid messageId, CancellationToken cancellationToken = default) =>
        Group(chatId).SendAsync("messageStarted", new { chatId, messageId }, cancellationToken);

    public Task MessageDeltaAsync(
        Guid chatId,
        Guid messageId,
        string delta,
        CancellationToken cancellationToken = default) =>
        Group(chatId).SendAsync("messageDelta", new { chatId, messageId, delta }, cancellationToken);

    public Task MessageCompletedAsync(
        Guid chatId,
        MessageResponse message,
        CancellationToken cancellationToken = default) =>
        Group(chatId).SendAsync("messageCompleted", new { chatId, message }, cancellationToken);

    public Task ChatRenamedAsync(Guid chatId, string title, CancellationToken cancellationToken = default) =>
        Group(chatId).SendAsync("chatRenamed", new { chatId, title }, cancellationToken);

    private IClientProxy Group(Guid chatId) => hubContext.Clients.Group(ChatHub.GroupName(chatId));
}
