using Domain.Enums;

namespace Application.Contracts.Features.Chats.Responses;

public sealed record MessageResponse(Guid Id, MessageRoleEnum RoleEnum, string Text, DateTimeOffset CreatedAt);
