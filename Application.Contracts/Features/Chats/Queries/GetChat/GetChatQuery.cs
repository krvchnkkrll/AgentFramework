using Application.Contracts.Features.Chats.Responses;
using Domain.Common;
using MediatR;

namespace Application.Contracts.Features.Chats.Queries.GetChat;

public sealed record GetChatQuery(GetChatRequest Body) : IRequest<Result<ChatResponse>>;
