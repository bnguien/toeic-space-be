using ToeicSpace.Identity.Application.Features.UserManagement.Common;
using ToeicSpace.Identity.Application.Features.UserManagement.Models;

namespace ToeicSpace.Identity.Application.Features.UserManagement.Queries.GetUsers;

public sealed class GetUsersValidator : AbstractValidator<GetUsersQuery>
{
    public GetUsersValidator()
    {
        RuleFor(query => query.Search)
            .MaximumLength(100);

        RuleFor(query => query.Role)
            .Must(role => UserAdministrationRules.TryParseRole(role, out _))
            .When(query => !string.IsNullOrWhiteSpace(query.Role))
            .WithMessage("Role must be User, Teacher or Admin.");

        RuleFor(query => query.Status)
            .Must(status => UserAdministrationRules.TryParseStatus(status, out _))
            .When(query => !string.IsNullOrWhiteSpace(query.Status))
            .WithMessage("Status must be Active, Inactive or Suspended.");

        RuleFor(query => query.Sort)
            .Must(sort => Enum.GetNames<UserSortOrder>()
                .Contains(sort?.Trim(), StringComparer.OrdinalIgnoreCase))
            .When(query => !string.IsNullOrWhiteSpace(query.Sort))
            .WithMessage("Sort must be Newest, Oldest, Name or LastActive.");

        RuleFor(query => query.Page)
            .GreaterThanOrEqualTo(1);

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, 100);
    }
}
