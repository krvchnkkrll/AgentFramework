using Application.Contracts.Features.Chats.Queries.GetChats;
using Application.Contracts.Features.Chats.Responses;
using Domain.Common;
using MediatR;
using Persistence.Contracts.Repositories;
using Persistence.Contracts.Services;

namespace Application.Features.Chats.Queries.GetChats;

file sealed class GetChatsQueryHandler(
    ICurrentUserService currentUserService,
    IConversationRepository conversationRepository)
    : IRequestHandler<GetChatsQuery, Result<IReadOnlyCollection<ChatSummaryResponse>>>
{
    public async Task<Result<IReadOnlyCollection<ChatSummaryResponse>>> Handle(
        GetChatsQuery request,
        CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetUserId();
        if (userIdResult.IsFailure)
            return Result.Failure<IReadOnlyCollection<ChatSummaryResponse>>(userIdResult.Error);

        var conversations = await conversationRepository.GetByUserIdAsync(userIdResult.Value, cancellationToken);

        return conversations
            .Select(conversation => new ChatSummaryResponse(
                conversation.Id,
                conversation.Title,
                conversation.CreatedAt,
                conversation.UpdatedAt,
                conversation.IsPinned,
                conversation.AgentId))
            .ToArray();
    }
}
