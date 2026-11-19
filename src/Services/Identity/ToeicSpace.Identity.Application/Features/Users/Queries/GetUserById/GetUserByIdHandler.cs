using ToeicSpace.Identity.Application.DTOs.Users;
using ToeicSpace.Identity.Application.Interfaces.Persistence;
using ToeicSpace.Identity.Application.Services;

namespace ToeicSpace.Identity.Application.Features.Users.Queries.GetUserById;

/// <summary>
/// Deleted accounts are returned too, so the trash can show what would be restored.
/// </summary>
public sealed class GetUserByIdHandler : IRequestHandler<GetUserByIdQuery, ManagedUserDetailResult>
{
    private readonly IUserRepository _userRepository;
    private readonly ManagedUserReader _reader;

    public GetUserByIdHandler(
        IUserRepository userRepository,
        ManagedUserReader reader)
    {
        _userRepository = userRepository;
        _reader = reader;
    }

    public async Task<ManagedUserDetailResult> Handle(
        GetUserByIdQuery request,
        CancellationToken cancellationToken)
    {
        var user = await UserAdministrationRules.GetExistingAsync(
            _userRepository,
            request.UserId,
            cancellationToken);

        return await _reader.ReadAsync(user, cancellationToken);
    }
}
