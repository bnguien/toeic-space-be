using ToeicSpace.Identity.Application.Features.UserManagement.Common;
using ToeicSpace.Identity.Application.Features.UserManagement.Models;

namespace ToeicSpace.Identity.Application.Features.UserManagement.Queries.GetUserStatistics;

public sealed class GetUserStatisticsValidator : AbstractValidator<GetUserStatisticsQuery>
{
    public GetUserStatisticsValidator()
    {
        RuleFor(query => query.Role)
            .Must(role => UserAdministrationRules.TryParseRole(role, out _))
            .When(query => !string.IsNullOrWhiteSpace(query.Role))
            .WithMessage("Role must be User, Teacher or Admin.");

        // Real offsets range from UTC-12:00 to UTC+14:00.
        RuleFor(query => query.TimeZoneOffsetMinutes)
            .InclusiveBetween(-720, 840);

        RuleFor(query => query.Period)
            .Must(period => StatisticsPeriod.Find(period) is not null)
            .When(query => !string.IsNullOrWhiteSpace(query.Period))
            .WithMessage("Period must be 24h, 3d, 7d, 30d or 90d.");
    }
}
