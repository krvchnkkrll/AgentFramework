using Application.Contracts.Features.Chats.Commands.DeleteChat;
using Domain.Common;
using MediatR;
using Persistence.Contracts.Repositories;
using Persistence.Contracts.Services;

namespace Application.Features.Chats.Commands.DeleteChat;

file sealed class DeleteChatCommandHandler(
    ICurrentUserService currentUserService,
    IConversationRepository conversationRepository)
    : IRequestHandler<DeleteChatCommand, Result>
{
    public async Task<Result> Handle(DeleteChatCommand request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetUserId();
        if (userIdResult.IsFailure)
            return Result.Failure(userIdResult.Error);

        var conversation = await conversationRepository.GetByIdAsync(request.Body.ChatId, cancellationToken);

        if (conversation is null || conversation.UserId != userIdResult.Value)
            return Result.Failure(Error.NotFound("Chat.NotFound", "Chat was not found."));

        conversationRepository.Remove(conversation);
        await conversationRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
