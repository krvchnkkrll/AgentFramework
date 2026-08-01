using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Persistence.Contracts.Repositories;
using Persistence.Contracts.Services;

namespace Web.Hubs;

[Authorize]
public sealed class ChatHub(ICurrentUserService currentUserService, IConversationRepository conversationRepository)
    : Hub
{
    public async Task JoinChat(Guid chatId)
    {
        var userIdResult = currentUserService.GetUserId();
        if (userIdResult.IsFailure)
            throw new HubException("Not authenticated.");

        var conversation = await conversationRepository.GetByIdAsync(chatId, Context.ConnectionAborted);
        if (conversation is null || conversation.UserId != userIdResult.Value)
            throw new HubException("Chat not found.");

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(chatId), Context.ConnectionAborted);
    }

    public Task LeaveChat(Guid chatId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(chatId), Context.ConnectionAborted);

    internal static string GroupName(Guid chatId) => $"chat-{chatId}";
}
