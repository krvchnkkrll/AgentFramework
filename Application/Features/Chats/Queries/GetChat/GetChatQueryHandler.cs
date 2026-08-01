using Application.Contracts.Features.Chats.Queries.GetChat;
using Application.Contracts.Features.Chats.Responses;
using Domain.Common;
using MediatR;
using Persistence.Contracts.Repositories;
using Persistence.Contracts.Services;

namespace Application.Features.Chats.Queries.GetChat;

file sealed class GetChatQueryHandler(
    ICurrentUserService currentUserService,
    IConversationRepository conversationRepository)
    : IRequestHandler<GetChatQuery, Result<ChatResponse>>
{
    public async Task<Result<ChatResponse>> Handle(GetChatQuery request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetUserId();
        if (userIdResult.IsFailure)
            return Result.Failure<ChatResponse>(userIdResult.Error);

        var conversation = await conversationRepository.GetByIdAsync(request.Body.Id, cancellationToken);

        // Not owned by the caller reads the same as not found — avoids confirming that a
        // chat id belongs to someone else.
        if (conversation is null || conversation.UserId != userIdResult.Value)
            return Result.Failure<ChatResponse>(Error.NotFound("Chat.NotFound", "Chat was not found."));

        return conversation.ToResponse();
    }
}
