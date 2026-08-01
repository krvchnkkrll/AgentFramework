using Domain.Common;
using MediatR;

namespace Application.Contracts.Features.Chats.Commands.DeleteChat;

public sealed record DeleteChatCommand(DeleteChatRequest Body) : IRequest<Result>;
