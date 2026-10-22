namespace ToeicSpace.Identity.API.Contracts;

public sealed class UserListRequest
{
    public string? Search { get; init; }

    public string? Role { get; init; }

    public string? Status { get; init; }

    public bool Deleted { get; init; }

    public string? Sort { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;
}

public sealed record CreateUserRequest(
    string FullName,
    string Email,
    string? Phone,
    string Role);

public sealed record UpdateUserRequest(
    string FullName,
    string? Phone);

public sealed record ChangeUserRoleRequest(string Role);

public sealed record ChangeUserStatusRequest(string Status);
