namespace Application.Contracts.Features.Chats.Responses;

public sealed record ChatSummaryResponse(
    Guid Id,
    string Title,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool IsPinned,
    Guid? AgentId);
