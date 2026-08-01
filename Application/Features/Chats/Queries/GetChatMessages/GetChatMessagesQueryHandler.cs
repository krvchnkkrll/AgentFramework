using Application.Contracts.Features.Chats.Queries.GetChatMessages;
using Application.Contracts.Features.Chats.Responses;
using Domain.Common;
using MediatR;
using Persistence.Contracts.Repositories;
using Persistence.Contracts.Services;

namespace Application.Features.Chats.Queries.GetChatMessages;

file sealed class GetChatMessagesQueryHandler(
    ICurrentUserService currentUserService,
    IConversationRepository conversationRepository)
    : IRequestHandler<GetChatMessagesQuery, Result<IReadOnlyCollection<MessageResponse>>>
{
    public async Task<Result<IReadOnlyCollection<MessageResponse>>> Handle(
        GetChatMessagesQuery request,
        CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetUserId();
        if (userIdResult.IsFailure)
            return Result.Failure<IReadOnlyCollection<MessageResponse>>(userIdResult.Error);

        var conversation = await conversationRepository.GetByIdAsync(request.Body.ChatId, cancellationToken);

        if (conversation is null || conversation.UserId != userIdResult.Value)
            return Result.Failure<IReadOnlyCollection<MessageResponse>>(
                Error.NotFound("Chat.NotFound", "Chat was not found."));

        return conversation.Messages.Select(message => message.ToResponse()).ToArray();
    }
}
