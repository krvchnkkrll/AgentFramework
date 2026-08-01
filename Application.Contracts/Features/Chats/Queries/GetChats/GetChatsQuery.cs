using Application.Contracts.Features.Chats.Responses;
using Domain.Common;
using MediatR;

namespace Application.Contracts.Features.Chats.Queries.GetChats;

public sealed record GetChatsQuery : IRequest<Result<IReadOnlyCollection<ChatSummaryResponse>>>;
