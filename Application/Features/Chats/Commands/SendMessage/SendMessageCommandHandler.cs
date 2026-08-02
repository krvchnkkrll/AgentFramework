using Application.Contracts.Features.Chats.Commands.SendMessage;
using Application.Contracts.Features.Chats.Responses;
using Application.Contracts.Models;
using Application.Contracts.Services;
using Domain.Common;
using Domain.Entities.Conversations.Parameters;
using Domain.Enums;
using MediatR;
using Persistence.Contracts.Repositories;
using Persistence.Contracts.Services;

namespace Application.Features.Chats.Commands.SendMessage;

file sealed class SendMessageCommandHandler(
    ICurrentUserService currentUserService,
    IConversationRepository conversationRepository,
    IGenerationRegistryService generationRegistryService)
    : IRequestHandler<SendMessageCommand, Result<MessageResponse>>
{
    public async Task<Result<MessageResponse>> Handle(SendMessageCommand request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetUserId();
        if (userIdResult.IsFailure)
            return Result.Failure<MessageResponse>(userIdResult.Error);

        var conversation = await conversationRepository.GetByIdAsync(request.Body.ChatId, cancellationToken);

        if (conversation is null || conversation.UserId != userIdResult.Value)
            return Result.Failure<MessageResponse>(Error.NotFound("Chat.NotFound", "Chat was not found."));

        if (generationRegistryService.IsGenerationActive(conversation.Id))
            return Result.Failure<MessageResponse>(
                Error.Conflict("Chat.GenerationInProgress", "Дождитесь окончания текущей генерации."));

        conversation.ResetError();

        var message = conversation.AddMessage(new AddMessageParameter
        {
            RoleEnum = MessageRoleEnum.User,
            Text = request.Body.Text,
        });

        await conversationRepository.SaveChangesAsync(cancellationToken);

        _ = Task.Run(() => generationRegistryService.StartGeneration(new StartGenerationParameters
        {
            UserId = userIdResult.Value,
            ConversationId = conversation.Id,
        }), CancellationToken.None);

        return message.ToResponse();
    }
}
