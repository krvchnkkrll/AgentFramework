using Application.Contracts.Features.Chats.Responses;
using Domain.Common;
using MediatR;

namespace Application.Contracts.Features.Chats.Commands.UnpinChat;

public sealed record UnpinChatCommand(UnpinChatRequest Body) : IRequest<Result<ChatResponse>>;
