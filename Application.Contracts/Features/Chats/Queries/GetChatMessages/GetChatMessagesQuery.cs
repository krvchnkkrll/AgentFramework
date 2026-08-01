using Application.Contracts.Features.Chats.Responses;
using Domain.Common;
using MediatR;

namespace Application.Contracts.Features.Chats.Queries.GetChatMessages;

public sealed record GetChatMessagesQuery(GetChatMessagesRequest Body)
    : IRequest<Result<IReadOnlyCollection<MessageResponse>>>;
