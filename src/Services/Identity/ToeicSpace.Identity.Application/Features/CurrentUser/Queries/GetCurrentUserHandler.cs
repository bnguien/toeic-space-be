using ToeicSpace.Identity.Application.Extensions;
using ToeicSpace.Identity.Application.Interfaces.Persistence;
using ToeicSpace.Identity.Application.Models;
using ToeicSpace.Identity.Domain.Exceptions;

namespace ToeicSpace.Identity.Application.Features.CurrentUser.Queries;

public sealed class GetCurrentUserHandler : IRequestHandler<GetCurrentUserQuery, AuthenticatedUser>
{
    private readonly IUserRepository _userRepository;

    public GetCurrentUserHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<AuthenticatedUser> Handle(
        GetCurrentUserQuery request,
        CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);

        // A valid token for a disabled account must not keep working.
        if (user is null || !user.CanSignIn())
        {
            throw AppException.Unauthenticated(
                "Your session has ended. Please sign in again.",
                ErrorCodes.TokenRevoked);
        }

        return user.ToAuthenticatedUser();
    }
}
