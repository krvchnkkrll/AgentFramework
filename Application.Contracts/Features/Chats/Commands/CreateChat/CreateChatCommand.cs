using Application.Contracts.Features.Chats.Responses;
using Domain.Common;
using MediatR;

namespace Application.Contracts.Features.Chats.Commands.CreateChat;

public sealed record CreateChatCommand(CreateChatRequest Body) : IRequest<Result<ChatResponse>>;
