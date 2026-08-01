using Application.Contracts.Features.Users.Queries;
using Application.Contracts.Features.Users.Responses;
using Domain.Common;
using Domain.Entities.Users;
using MediatR;
using Persistence.Contracts.Repositories;
using Persistence.Contracts.Services;

namespace Application.Features.Users.Queries;

file sealed class GetCurrentUserQueryHandler(
    ICurrentUserService currentUserService,
    IUserRepository userRepository)
    : IRequestHandler<GetCurrentUserQuery, Result<UserResponse>>
{
    public async Task<Result<UserResponse>> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetUserId();
        var emailResult = currentUserService.GetEmail();
        var usernameResult = currentUserService.GetUsername();

        var identityResult = Result.FirstFailureOrSuccess(userIdResult, emailResult, usernameResult);
        if (identityResult.IsFailure)
            return Result.Failure<UserResponse>(identityResult.Error);

        var userId = userIdResult.Value;
        var email = emailResult.Value;
        var username = usernameResult.Value;

        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            user = User.Provision(userId, email, username);
            await userRepository.AddAsync(user, cancellationToken);
        }
        else
        {
            user.SyncProfile(email, username);
        }

        user.RecordSignIn();
        await userRepository.SaveChangesAsync(cancellationToken);

        return new UserResponse(user.Id, user.Email, user.Username, user.CreatedAt, user.LastSeenAt);
    }
}
