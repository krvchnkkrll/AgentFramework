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
    IConversationRepository conversationRepository,
    IAgentRepository agentRepository)
    : IRequestHandler<CreateChatCommand, Result<ChatResponse>>
{
    public async Task<Result<ChatResponse>> Handle(CreateChatCommand request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetUserId();
        if (userIdResult.IsFailure)
            return Result.Failure<ChatResponse>(userIdResult.Error);

        // Чужого агента подсунуть нельзя: неизвестный или не свой — молча падаем на встроенный.
        var agentId = await ResolveAgentIdAsync(request.Body.AgentId, userIdResult.Value, cancellationToken);

        var conversation = Conversation.Create(new CreateConversationParameter
        {
            UserId = userIdResult.Value,
            Title = request.Body.Title,
            AgentId = agentId,
        });

        await conversationRepository.AddAsync(conversation, cancellationToken);
        await conversationRepository.SaveChangesAsync(cancellationToken);

        return conversation.ToResponse();
    }

    private async Task<Guid?> ResolveAgentIdAsync(Guid? agentId, Guid userId, CancellationToken cancellationToken)
    {
        if (agentId is not { } id)
            return null;

        var agent = await agentRepository.GetByIdAsync(id, cancellationToken);

        return agent is not null && agent.UserId == userId ? agent.Id : null;
    }
}
