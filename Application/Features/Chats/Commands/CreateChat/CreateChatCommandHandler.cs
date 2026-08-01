using Application.Contracts.Features.Chats.Commands.CreateChat;
using Application.Contracts.Features.Chats.Responses;
using Domain.Common;
using Domain.Entities.Conversations;
using Domain.Entities.Conversations.Parameters;
using MediatR;
using Persistence.Contracts.Repositories;
using Persistence.Contracts.Services;

namespace Application.Features.Chats.Commands.CreateChat;

file sealed class CreateChatCommandHandler(
    ICurrentUserService currentUserService,
    IConversationRepository conversationRepository)
    : IRequestHandler<CreateChatCommand, Result<ChatResponse>>
{
    public async Task<Result<ChatResponse>> Handle(CreateChatCommand request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetUserId();
        if (userIdResult.IsFailure)
            return Result.Failure<ChatResponse>(userIdResult.Error);

        var conversation = Conversation.Create(new CreateConversationParameter
        {
            UserId = userIdResult.Value,
            Title = request.Body.Title,
        });

        await conversationRepository.AddAsync(conversation, cancellationToken);
        await conversationRepository.SaveChangesAsync(cancellationToken);

        return conversation.ToResponse();
    }
}
