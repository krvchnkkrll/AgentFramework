using Application.Contracts.Features.Chats.Commands.UnpinChat;
using Application.Contracts.Features.Chats.Responses;
using Domain.Common;
using MediatR;
using Persistence.Contracts.Repositories;
using Persistence.Contracts.Services;

namespace Application.Features.Chats.Commands.UnpinChat;

file sealed class UnpinChatCommandHandler(
    ICurrentUserService currentUserService,
    IConversationRepository conversationRepository)
    : IRequestHandler<UnpinChatCommand, Result<ChatResponse>>
{
    public async Task<Result<ChatResponse>> Handle(UnpinChatCommand request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetUserId();
        if (userIdResult.IsFailure)
            return Result.Failure<ChatResponse>(userIdResult.Error);

        var conversation = await conversationRepository.GetByIdAsync(request.Body.ChatId, cancellationToken);

        if (conversation is null || conversation.UserId != userIdResult.Value)
            return Result.Failure<ChatResponse>(Error.NotFound("Chat.NotFound", "Chat was not found."));

        conversation.Unpin();

        await conversationRepository.SaveChangesAsync(cancellationToken);

        return conversation.ToResponse();
    }
}
