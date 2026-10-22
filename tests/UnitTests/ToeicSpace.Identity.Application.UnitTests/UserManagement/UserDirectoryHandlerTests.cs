using FluentValidation;
using ToeicSpace.Identity.Application.Features.Register.Commands;
using ToeicSpace.Identity.Application.Features.UserManagement.Commands.ChangeUserStatus;
using ToeicSpace.Identity.Application.Features.UserManagement.Commands.CreateUser;
using ToeicSpace.Identity.Application.Features.UserManagement.Commands.UpdateUser;
using ToeicSpace.Identity.Application.Features.UserManagement.Queries.GetUserById;
using ToeicSpace.Identity.Application.Features.UserManagement.Queries.GetUsers;
using ToeicSpace.Identity.Application.Features.UserManagement.Queries.GetUserStatistics;
using ToeicSpace.Identity.Domain.Enums;
using ToeicSpace.Identity.Domain.Exceptions;
using ToeicSpace.Identity.Infrastructure.Services.Security;

namespace ToeicSpace.Identity.Application.UnitTests.UserManagement;

public sealed class UserDirectoryHandlerTests
{
    private static readonly CancellationToken None = CancellationToken.None;

    [Fact]
    public async Task Created_account_is_active_verified_and_signs_in_with_the_temporary_password()
    {
        await using var context = await UserManagementTestContext.CreateAsync();
        var admin = await context.AddUserAsync("admin@toeicspace.vn", UserRole.Admin);

        var result = await context.CreateHandler().Handle(
            new CreateUserCommand(admin.Id, "  Nguyễn Văn A ", " New.Teacher@ToeicSpace.vn ", "+84 912 345 678", "Teacher"),
            None);

        result.User.Email.Should().Be("new.teacher@toeicspace.vn");
        result.User.FullName.Should().Be("Nguyễn Văn A");
        result.User.Phone.Should().Be("0912345678");
        result.User.Role.Should().Be("Teacher");
        result.User.Status.Should().Be("Active");
        result.User.EmailVerifiedAt.Should().NotBeNull();

        var session = await context.SignInAsync(result.User.Email, result.TemporaryPassword);
        session.User.Role.Should().Be("Teacher");
    }

    [Fact]
    public async Task Creating_an_account_with_a_taken_email_or_phone_is_a_conflict()
    {
        await using var context = await UserManagementTestContext.CreateAsync();
        var admin = await context.AddUserAsync("admin@toeicspace.vn", UserRole.Admin);
        await context.AddUserAsync(
            "taken@toeicspace.vn", phone: "0912345678", deletedAt: context.Identity.Time.Now.UtcDateTime);

        // A deleted account still owns its email and phone.
        await AssertFailsAsync(
            () => context.CreateHandler().Handle(
                new CreateUserCommand(admin.Id, "Copy", "TAKEN@toeicspace.vn", null, "User"), None),
            ErrorCodes.EmailAlreadyExists);
        await AssertFailsAsync(
            () => context.CreateHandler().Handle(
                new CreateUserCommand(admin.Id, "Copy", "fresh@toeicspace.vn", "0912 345 678", "User"), None),
            ErrorCodes.PhoneAlreadyExists);
    }

    [Fact]
    public void Temporary_passwords_satisfy_the_registration_rules_and_are_unique()
    {
        var generator = new TemporaryPasswordGenerator();
        var validator = new RegisterValidator();
        var passwords = Enumerable.Range(0, 200).Select(_ => generator.Generate()).ToList();

        passwords.Should().OnlyHaveUniqueItems();

        foreach (var password in passwords)
        {
            validator.Validate(new RegisterCommand("Name", "a@b.vn", null, password, password))
                .IsValid.Should().BeTrue(because: $"'{password}' must pass the password rules");
        }
    }

    [Fact]
    public async Task Updating_normalises_the_phone_rejects_a_taken_one_and_null_clears_it()
    {
        await using var context = await UserManagementTestContext.CreateAsync();
        var admin = await context.AddUserAsync("admin@toeicspace.vn", UserRole.Admin);
        var student = await context.AddUserAsync("student@toeicspace.vn", phone: "0911111111");
        await context.AddUserAsync("other@toeicspace.vn", phone: "0922222222");

        var updated = await context.UpdateHandler().Handle(
            new UpdateUserCommand(admin.Id, student.Id, " Trần Thị B ", "+84 911 111 111"), None);
        updated.FullName.Should().Be("Trần Thị B");
        updated.Phone.Should().Be("0911111111");

        await AssertFailsAsync(
            () => context.UpdateHandler().Handle(
                new UpdateUserCommand(admin.Id, student.Id, "Trần Thị B", "0922222222"), None),
            ErrorCodes.PhoneAlreadyExists);

        var cleared = await context.UpdateHandler().Handle(
            new UpdateUserCommand(admin.Id, student.Id, "Trần Thị B", null), None);
        cleared.Phone.Should().BeNull();
    }

    [Fact]
    public async Task Search_filters_by_text_role_status_and_trash()
    {
        await using var context = await UserManagementTestContext.CreateAsync();
        var now = context.Identity.Time.Now.UtcDateTime;
        await context.AddUserAsync("admin@toeicspace.vn", UserRole.Admin, fullName: "Quản trị");
        await context.AddUserAsync("lan@toeicspace.vn", UserRole.Teacher, fullName: "Cô Lan", phone: "0933333333");
        await context.AddUserAsync("minh@toeicspace.vn", UserRole.Teacher, UserStatus.Suspended, fullName: "Thầy Minh");
        await context.AddUserAsync("an@gmail.com", fullName: "An", createdAt: now.AddDays(-2));
        await context.AddUserAsync("binh@gmail.com", fullName: "Bình", deletedAt: now);

        var teachers = await List(context, role: "Teacher");
        teachers.TotalCount.Should().Be(2);

        var suspended = await List(context, role: "Teacher", status: "Suspended");
        suspended.Items.Should().ContainSingle().Which.Email.Should().Be("minh@toeicspace.vn");

        (await List(context, search: "0933")).Items.Should().ContainSingle().Which.FullName.Should().Be("Cô Lan");
        (await List(context, search: "gmail")).Items.Should().ContainSingle().Which.Email.Should().Be("an@gmail.com");

        var trash = await List(context, deleted: true);
        trash.Items.Should().ContainSingle().Which.DeletedAt.Should().NotBeNull();

        (await List(context)).TotalCount.Should().Be(4);
    }

    [Fact]
    public async Task Search_pages_in_a_stable_order_and_shows_the_last_activity()
    {
        await using var context = await UserManagementTestContext.CreateAsync();
        var now = context.Identity.Time.Now.UtcDateTime;
        for (var index = 0; index < 5; index++)
        {
            await context.AddUserAsync($"user{index}@toeicspace.vn", createdAt: now.AddMinutes(-index));
        }

        var newestFirst = await List(context, pageSize: 2, page: 1);
        newestFirst.TotalCount.Should().Be(5);
        newestFirst.TotalPages.Should().Be(3);
        newestFirst.Items.Select(user => user.Email).Should().Equal("user0@toeicspace.vn", "user1@toeicspace.vn");

        var lastPage = await List(context, pageSize: 2, page: 3);
        lastPage.Items.Should().ContainSingle().Which.Email.Should().Be("user4@toeicspace.vn");
        (await List(context, sort: "Oldest", pageSize: 1)).Items.Single().Email.Should().Be("user4@toeicspace.vn");

        lastPage.Items.Single().LastActiveAt.Should().BeNull();
        await context.SignInAsync("user4@toeicspace.vn");
        (await List(context, pageSize: 2, page: 3)).Items.Single().LastActiveAt.Should().Be(now);
    }

    [Fact]
    public async Task Statistics_count_one_role_and_keep_deleted_accounts_apart()
    {
        await using var context = await UserManagementTestContext.CreateAsync();
        var now = context.Identity.Time.Now.UtcDateTime;
        await context.AddUserAsync("admin@toeicspace.vn", UserRole.Admin);
        await context.AddUserAsync("a@gmail.com");
        await context.AddUserAsync("b@gmail.com", status: UserStatus.Suspended, createdAt: now.AddDays(-60));
        await context.AddUserAsync("c@gmail.com", status: UserStatus.Inactive, emailVerified: false);
        await context.AddUserAsync("d@gmail.com", deletedAt: now);

        var students = await context.StatisticsHandler().Handle(new GetUserStatisticsQuery("User"), None);

        students.Should().BeEquivalentTo(new
        {
            Total = 3, Active = 1, Suspended = 1, Unverified = 1, Deleted = 1, CreatedLast30Days = 2
        });

        (await context.StatisticsHandler().Handle(new GetUserStatisticsQuery(null), None)).Total.Should().Be(4);
    }

    [Fact]
    public async Task Weekly_trend_buckets_days_in_the_callers_time_zone_and_compares_with_the_week_before()
    {
        await using var context = await UserManagementTestContext.CreateAsync();
        // 2026-09-16 08:00 UTC is 15:00 in Vietnam (UTC+7).
        var now = context.Identity.Time.Now.UtcDateTime;
        var today = await context.AddUserAsync("today@gmail.com", createdAt: now);
        // 2026-09-15 20:00 UTC is already 2026-09-16 03:00 in Vietnam.
        await context.AddUserAsync("late@gmail.com", createdAt: now.AddHours(-12));
        await context.AddUserAsync("week@gmail.com", createdAt: now.AddDays(-5), deletedAt: now);
        await context.AddUserAsync("previous@gmail.com", createdAt: now.AddDays(-9));
        await context.AddUserAsync("teacher@gmail.com", UserRole.Teacher, createdAt: now);

        // Two sign-ins of the same user count once; the teacher is outside the role.
        await context.SignInAsync(today.Email);
        await context.SignInAsync(today.Email);
        await context.SignInAsync("teacher@gmail.com");

        var trend = (await context.StatisticsHandler()
            .Handle(new GetUserStatisticsQuery("User", 420, "7d"), None)).Trend;

        trend.Bucket.Should().Be("day");
        trend.Buckets.Should().HaveCount(7);
        trend.Buckets[0].Start.Should().Be(new DateTime(2026, 9, 10));
        trend.Buckets[^1].Should().Be(new Features.UserManagement.Results.TrendBucketResult(
            new DateTime(2026, 9, 16), Registrations: 2, ActiveUsers: 1));
        trend.Registrations.Should().Be(3, "deleted signups still happened");
        trend.PreviousRegistrations.Should().Be(1);
        trend.ActiveUsers.Should().Be(1);
        trend.PreviousActiveUsers.Should().Be(0);
    }

    [Fact]
    public async Task Day_trend_uses_hourly_buckets_ending_with_the_current_hour()
    {
        await using var context = await UserManagementTestContext.CreateAsync();
        var now = context.Identity.Time.Now.UtcDateTime;
        await context.AddUserAsync("now@gmail.com", createdAt: now);
        await context.AddUserAsync("earlier@gmail.com", createdAt: now.AddHours(-3));
        await context.AddUserAsync("yesterday@gmail.com", createdAt: now.AddHours(-30));

        var trend = (await context.StatisticsHandler()
            .Handle(new GetUserStatisticsQuery(null, 420, "24h"), None)).Trend;

        trend.Bucket.Should().Be("hour");
        trend.Buckets.Should().HaveCount(24);
        trend.Buckets[^1].Start.Should().Be(new DateTime(2026, 9, 16, 15, 0, 0));
        trend.Buckets[^1].Registrations.Should().Be(1);
        trend.Buckets[^4].Registrations.Should().Be(1);
        trend.Registrations.Should().Be(2);
        trend.PreviousRegistrations.Should().Be(1);
    }

    [Theory]
    [InlineData("7d", true)]
    [InlineData("90D", true)]
    [InlineData("1y", false)]
    public void Statistics_validator_accepts_only_the_offered_periods(string period, bool valid)
    {
        new GetUserStatisticsValidator()
            .Validate(new GetUserStatisticsQuery(null, 420, period))
            .IsValid.Should().Be(valid);
    }

    [Fact]
    public async Task Search_can_order_by_last_activity_with_never_active_users_last()
    {
        await using var context = await UserManagementTestContext.CreateAsync();
        await context.AddUserAsync("never@gmail.com");
        await context.AddUserAsync("earlier@gmail.com");
        await context.AddUserAsync("latest@gmail.com");

        await context.SignInAsync("earlier@gmail.com");
        context.Identity.Time.Advance(TimeSpan.FromHours(1));
        await context.SignInAsync("latest@gmail.com");

        (await List(context, sort: "LastActive")).Items.Select(user => user.Email)
            .Should().Equal("latest@gmail.com", "earlier@gmail.com", "never@gmail.com");
    }

    [Fact]
    public async Task Detail_includes_deleted_accounts_and_active_sessions()
    {
        await using var context = await UserManagementTestContext.CreateAsync();
        var student = await context.AddUserAsync("student@toeicspace.vn");
        await context.SignInAsync(student.Email);

        var detail = await context.DetailHandler().Handle(new GetUserByIdQuery(student.Id), None);

        detail.ActiveSessionCount.Should().Be(1);
        detail.LastActiveAt.Should().Be(context.Identity.Time.Now.UtcDateTime);
    }

    [Theory]
    [InlineData("2", null, null, 1, 20)]
    [InlineData(null, "Deleted", null, 1, 20)]
    [InlineData(null, null, "Email", 1, 20)]
    [InlineData(null, null, null, 0, 20)]
    [InlineData(null, null, null, 1, 101)]
    public void List_validator_rejects_unknown_filters_and_page_sizes(
        string? role, string? status, string? sort, int page, int pageSize)
    {
        new GetUsersValidator()
            .Validate(new GetUsersQuery(null, role, status, false, sort, page, pageSize))
            .IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("Active", true)]
    [InlineData("suspended", true)]
    [InlineData("Inactive", false)]
    [InlineData("3", false)]
    public void Only_active_and_suspended_can_be_assigned(string status, bool valid)
    {
        new ChangeUserStatusValidator()
            .Validate(new ChangeUserStatusCommand(Guid.NewGuid(), Guid.NewGuid(), status))
            .IsValid.Should().Be(valid);
    }

    private static Task<BuildingBlocks.Pagination.PaginatedResult<Features.UserManagement.Results.ManagedUserResult>> List(
        UserManagementTestContext context,
        string? search = null,
        string? role = null,
        string? status = null,
        bool deleted = false,
        string? sort = null,
        int page = 1,
        int pageSize = 20)
        => context.ListHandler().Handle(
            new GetUsersQuery(search, role, status, deleted, sort, page, pageSize),
            None);

    private static async Task AssertFailsAsync(Func<Task> action, string code)
    {
        var result = await FluentActions.Awaiting(action).Should().ThrowAsync<AppException>();

        result.Which.Type.Should().Be(ErrorType.Conflict);
        result.Which.Code.Should().Be(code);
    }
}
