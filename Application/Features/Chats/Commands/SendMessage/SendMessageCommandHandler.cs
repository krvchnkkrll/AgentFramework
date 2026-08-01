using Application.Contracts.Features.Chats.Commands.SendMessage;
using Application.Contracts.Features.Chats.Responses;
using Domain.Common;
using Domain.Entities.Conversations.Parameters;
using Domain.Enums;
using MediatR;
using Persistence.Contracts.Repositories;
using Persistence.Contracts.Services;

namespace Application.Features.Chats.Commands.SendMessage;

file sealed class SendMessageCommandHandler(
    ICurrentUserService currentUserService,
    IConversationRepository conversationRepository)
    : IRequestHandler<SendMessageCommand, Result<MessageResponse>>
{
    public async Task<Result<MessageResponse>> Handle(SendMessageCommand request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetUserId();
        if (userIdResult.IsFailure)
            return Result.Failure<MessageResponse>(userIdResult.Error);

        var conversation = await conversationRepository.GetByIdAsync(request.Body.ChatId, cancellationToken);

        // Not owned by the caller reads the same as not found — avoids confirming that a
        // chat id belongs to someone else.
        if (conversation is null || conversation.UserId != userIdResult.Value)
            return Result.Failure<MessageResponse>(Error.NotFound("Chat.NotFound", "Chat was not found."));

        // Always User here — a caller hitting this endpoint is a human sending a message,
        // never impersonating the assistant/system/tool roles.
        var message = conversation.AddMessage(new AddMessageParameter
        {
            RoleEnum = MessageRoleEnum.User,
            Text = request.Body.Text,
        });

        await conversationRepository.SaveChangesAsync(cancellationToken);

        return message.ToResponse();
    }
}
