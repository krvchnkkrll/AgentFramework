using Application.Contracts.Features.Users.Responses;
using Domain.Common;
using MediatR;

namespace Application.Contracts.Features.Users.Queries;

public sealed record GetCurrentUserQuery : IRequest<Result<UserResponse>>;
