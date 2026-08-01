using Application.Contracts.Features.Chats.Responses;
using Domain.Entities.Conversations;
using Domain.Entities.Messages;

namespace Application.Features.Chats;

internal static class ConversationMappingExtensions
{
    public static ChatResponse ToResponse(this Conversation conversation) =>
        new(
            conversation.Id,
            conversation.Title,
            conversation.CreatedAt,
            conversation.UpdatedAt,
            conversation.IsPinned,
            conversation.Messages.Select(message => message.ToResponse()).ToArray());

    public static MessageResponse ToResponse(this Message message) =>
        new(message.Id, message.RoleEnum, message.Text, message.CreatedAt);
}
