using Application.Contracts.Features.Chats.Responses;
using Domain.Common;
using MediatR;

namespace Application.Contracts.Features.Chats.Commands.RenameChat;

public sealed record RenameChatCommand(RenameChatRequest Body) : IRequest<Result<ChatResponse>>;
