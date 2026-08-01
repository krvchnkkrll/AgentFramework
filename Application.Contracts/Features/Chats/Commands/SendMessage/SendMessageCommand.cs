using Application.Contracts.Features.Chats.Responses;
using Domain.Common;
using MediatR;

namespace Application.Contracts.Features.Chats.Commands.SendMessage;

public sealed record SendMessageCommand(SendMessageRequest Body) : IRequest<Result<MessageResponse>>;
