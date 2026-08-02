namespace Application.Contracts.Features.Chats.Responses;

public sealed record ChatResponse(
    Guid Id,
    string Title,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool IsPinned,
    Guid? AgentId,
    IReadOnlyCollection<MessageResponse> Messages);
