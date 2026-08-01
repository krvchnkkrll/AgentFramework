using Application.Contracts.Features.Chats.Responses;
using Domain.Common;
using MediatR;

namespace Application.Contracts.Features.Chats.Commands.PinChat;

public sealed record PinChatCommand(PinChatRequest Body) : IRequest<Result<ChatResponse>>;
