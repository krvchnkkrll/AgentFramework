using Application.Contracts.Features.Chats.Responses;
using Domain.Common;
using MediatR;

namespace Application.Contracts.Features.Chats.Commands.SetChatAgent;

public sealed record SetChatAgentRequest
{
    public required Guid ChatId { get; init; }

    /// <summary>Пусто — вернуть чат встроенному агенту.</summary>
    public Guid? AgentId { get; init; }
}

public sealed record SetChatAgentCommand(SetChatAgentRequest Body) : IRequest<Result<ChatResponse>>;
