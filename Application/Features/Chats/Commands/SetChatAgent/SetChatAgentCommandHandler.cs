using Application.Contracts.Features.Chats.Commands.SetChatAgent;
using Application.Contracts.Features.Chats.Responses;
using Application.Contracts.Services;
using Domain.Common;
using MediatR;
using Persistence.Contracts.Repositories;
using Persistence.Contracts.Services;

namespace Application.Features.Chats.Commands.SetChatAgent;

file sealed class SetChatAgentCommandHandler(
    ICurrentUserService currentUserService,
    IConversationRepository conversationRepository,
    IAgentRepository agentRepository,
    IGenerationRegistryService generationRegistryService)
    : IRequestHandler<SetChatAgentCommand, Result<ChatResponse>>
{
    public async Task<Result<ChatResponse>> Handle(SetChatAgentCommand request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetUserId();
        if (userIdResult.IsFailure)
            return Result.Failure<ChatResponse>(userIdResult.Error);

        var conversation = await conversationRepository.GetByIdAsync(request.Body.ChatId, cancellationToken);

        if (conversation is null || conversation.UserId != userIdResult.Value)
            return Result.Failure<ChatResponse>(Error.NotFound("Chat.NotFound", "Chat was not found."));

        // Менять агента на лету нельзя: генерация уже идёт со старым промптом и его скиллами.
        if (generationRegistryService.IsGenerationActive(conversation.Id))
        {
            return Result.Failure<ChatResponse>(
                Error.Conflict("Chat.GenerationInProgress", "Дождитесь окончания текущей генерации."));
        }

        if (request.Body.AgentId is { } agentId)
        {
            var agent = await agentRepository.GetByIdAsync(agentId, cancellationToken);

            if (agent is null || agent.UserId != userIdResult.Value)
                return Result.Failure<ChatResponse>(Error.NotFound("Agent.NotFound", "Агент не найден."));
        }

        // Сбрасывает и состояние сессии: контекст, собранный прошлым агентом, новому не подходит.
        conversation.AssignAgent(request.Body.AgentId);

        await conversationRepository.SaveChangesAsync(cancellationToken);

        return conversation.ToResponse();
    }
}
