using Application.Contracts.Features.Chats.Commands.RenameChat;
using Application.Contracts.Features.Chats.Responses;
using Domain.Common;
using MediatR;
using Persistence.Contracts.Repositories;
using Persistence.Contracts.Services;

namespace Application.Features.Chats.Commands.RenameChat;

file sealed class RenameChatCommandHandler(
    ICurrentUserService currentUserService,
    IConversationRepository conversationRepository)
    : IRequestHandler<RenameChatCommand, Result<ChatResponse>>
{
    public async Task<Result<ChatResponse>> Handle(RenameChatCommand request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetUserId();
        if (userIdResult.IsFailure)
            return Result.Failure<ChatResponse>(userIdResult.Error);

        var conversation = await conversationRepository.GetByIdAsync(request.Body.ChatId, cancellationToken);

        if (conversation is null || conversation.UserId != userIdResult.Value)
            return Result.Failure<ChatResponse>(Error.NotFound("Chat.NotFound", "Chat was not found."));

        conversation.Rename(request.Body.Title);

        await conversationRepository.SaveChangesAsync(cancellationToken);

        return conversation.ToResponse();
    }
}
