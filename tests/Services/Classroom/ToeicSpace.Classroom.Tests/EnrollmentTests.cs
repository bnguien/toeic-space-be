using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using ToeicSpace.BuildingBlocks.Messaging.Events;
using ToeicSpace.Classroom.API.Controllers;
using ToeicSpace.Classroom.Application.Features.Classes.Commands.HardDeleteClass;
using ToeicSpace.Classroom.Application.Features.Enrollments.Commands.EnrollStudent;
using ToeicSpace.Classroom.Application.Features.Enrollments.Commands.WithdrawStudent;
using ToeicSpace.Classroom.Application.Features.Enrollments.Queries.GetClassEnrollments;
using ToeicSpace.Classroom.Application.Interfaces;
using ToeicSpace.Classroom.Application.Services;
using ToeicSpace.Classroom.Domain.Entities;
using ToeicSpace.Classroom.Domain.Enums;
using ToeicSpace.Classroom.Domain.Exceptions;
using ToeicSpace.Classroom.Infrastructure.Consumers;
using ToeicSpace.Classroom.Infrastructure.Persistence;
using ToeicSpace.Classroom.Infrastructure.Persistence.Repositories;
using Xunit;

namespace ToeicSpace.Classroom.Tests;

public sealed class EnrollmentTests
{
    private static readonly Guid Admin = Guid.NewGuid();
    private static readonly InMemoryDatabaseRoot Root = new();
    private static ClassroomDbContext NewDb(string? name = null) => new(
        new DbContextOptionsBuilder<ClassroomDbContext>().UseInMemoryDatabase(name ?? Guid.NewGuid().ToString(), Root).Options);

    private static EnrollStudentHandler EnrollHandler(ClassroomDbContext db, IUnitOfWork? uow = null) =>
        new(new ClassRepository(db), new EnrollmentRepository(db), new StudentEligibilityService(new StudentReadRepository(db),
                NullLogger<StudentEligibilityService>.Instance),
            uow ?? new UnitOfWork(db), TimeProvider.System, NullLogger<EnrollStudentHandler>.Instance);

    private static WithdrawStudentHandler WithdrawHandler(ClassroomDbContext db) =>
        new(new ClassRepository(db), new EnrollmentRepository(db), new StudentReadRepository(db),
            new UnitOfWork(db), TimeProvider.System, NullLogger<WithdrawStudentHandler>.Instance);

    private static GetClassEnrollmentsHandler ListHandler(ClassroomDbContext db) =>
        new(new ClassRepository(db), new EnrollmentRepository(db), NullLogger<GetClassEnrollmentsHandler>.Instance);

    private static async Task<(Guid ClassId, Guid StudentId)> SetupAsync(ClassroomDbContext db,
        ClassStatus status = ClassStatus.Draft, string role = "User", string studentStatus = "Active", bool verified = true)
    {
        var classroom = new Class { Code = Guid.NewGuid().ToString(), Name = "Test", Status = status };
        var student = new StudentReadModel
        {
            StudentId = Guid.NewGuid(), Name = "Learner", Email = "student@example.com",
            Role = role, Status = studentStatus, IsVerified = verified,
            IsEligible = role == "User" && studentStatus == "Active" && verified, Version = 1
        };
        db.Classes.Add(classroom);
        db.Students.Add(student);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return (classroom.Id, student.StudentId);
    }

    [Fact]
    public async Task Enroll_withdraw_and_reenroll_preserve_history_and_filter_results()
    {
        await using var db = NewDb();
        var (classId, studentId) = await SetupAsync(db);
        var first = await EnrollHandler(db).Handle(new EnrollStudentCommand(studentId) { ClassId = classId, AdminId = Admin }, default);
        Assert.Equal(EnrollmentStatus.Active, first.Status);
        Assert.Equal("Learner", first.StudentName);
        Assert.Equal("already_enrolled", (await Assert.ThrowsAsync<ClassRuleException>(() =>
            EnrollHandler(db).Handle(new EnrollStudentCommand(studentId) { ClassId = classId, AdminId = Admin }, default))).Code);

        var withdrawn = await WithdrawHandler(db).Handle(new WithdrawStudentCommand(classId, first.Id, Admin), default);
        Assert.Equal(EnrollmentStatus.Withdrawn, withdrawn.Status);
        Assert.NotNull(withdrawn.WithdrawnAt);
        Assert.Equal("enrollment_not_active", (await Assert.ThrowsAsync<ClassRuleException>(() =>
            WithdrawHandler(db).Handle(new WithdrawStudentCommand(classId, first.Id, Admin), default))).Code);

        var second = await EnrollHandler(db).Handle(new EnrollStudentCommand(studentId) { ClassId = classId, AdminId = Admin }, default);
        Assert.NotEqual(first.Id, second.Id);
        var all = await ListHandler(db).Handle(new GetClassEnrollmentsQuery(classId, null, studentId, 1, 1), default);
        Assert.Equal(2, all.TotalCount);
        Assert.Single(all.Items);
        var current = await ListHandler(db).Handle(new GetClassEnrollmentsQuery(classId, EnrollmentStatus.Active, studentId), default);
        Assert.Equal(second.Id, Assert.Single(current.Items).Id);
        var history = await ListHandler(db).Handle(new GetClassEnrollmentsQuery(classId, EnrollmentStatus.Withdrawn, studentId), default);
        Assert.Equal(first.Id, Assert.Single(history.Items).Id);
        Assert.Equal("Learner", history.Items[0].StudentName);
        Assert.Empty((await ListHandler(db).Handle(new GetClassEnrollmentsQuery(classId, EnrollmentStatus.Active, Guid.NewGuid()), default)).Items);
    }

    [Theory]
    [InlineData("Teacher", "Active", true, "not_student")]
    [InlineData("User", "Inactive", true, "inactive_student")]
    [InlineData("User", "Active", false, "unverified_student")]
    public async Task Rejects_ineligible_students(string role, string status, bool verified, string code)
    {
        await using var db = NewDb();
        var (classId, studentId) = await SetupAsync(db, role: role, studentStatus: status, verified: verified);
        var ex = await Assert.ThrowsAsync<ClassRuleException>(() =>
            EnrollHandler(db).Handle(new EnrollStudentCommand(studentId) { ClassId = classId, AdminId = Admin }, default));
        Assert.Equal(code, ex.Code);
        Assert.Empty(await db.ClassEnrollments.ToListAsync());
    }

    [Fact]
    public async Task Missing_student_projection_fails_closed()
    {
        await using var db = NewDb();
        var (classId, _) = await SetupAsync(db);
        var missingId = Guid.NewGuid();
        var ex = await Assert.ThrowsAsync<ClassRuleException>(() =>
            EnrollHandler(db).Handle(new EnrollStudentCommand(missingId) { ClassId = classId, AdminId = Admin }, default));
        Assert.Equal("student_not_synchronized", ex.Code);
        Assert.Equal(503, ex.StatusCode);

        await new StudentChangedConsumer(db, TimeProvider.System, NullLogger<StudentChangedConsumer>.Instance)
            .ApplyAsync(new StudentChangedIntegrationEvent(missingId, "Late Student", "late@example.com", "User", "Active", true, true, 1));
        var enrolled = await EnrollHandler(db).Handle(
            new EnrollStudentCommand(missingId) { ClassId = classId, AdminId = Admin }, default);
        Assert.Equal(missingId, enrolled.StudentId);
    }

    [Theory]
    [InlineData(ClassStatus.Completed)]
    [InlineData(ClassStatus.Archived)]
    [InlineData(ClassStatus.Deleted)]
    public async Task Rejects_changes_in_non_modifiable_class_states(ClassStatus status)
    {
        await using var db = NewDb();
        var (classId, studentId) = await SetupAsync(db);
        var original = await EnrollHandler(db).Handle(new EnrollStudentCommand(studentId) { ClassId = classId, AdminId = Admin }, default);
        var classroom = await db.Classes.SingleAsync(x => x.Id == classId);
        classroom.Status = status;
        await db.SaveChangesAsync();

        var expected = status == ClassStatus.Deleted ? "class_not_found" : "invalid_state";
        Assert.Equal(expected, (await Assert.ThrowsAsync<ClassRuleException>(() =>
            EnrollHandler(db).Handle(new EnrollStudentCommand(Guid.NewGuid()) { ClassId = classId, AdminId = Admin }, default))).Code);
        Assert.Equal(expected, (await Assert.ThrowsAsync<ClassRuleException>(() =>
            WithdrawHandler(db).Handle(new WithdrawStudentCommand(classId, original.Id, Admin), default))).Code);
        Assert.Single(await db.ClassEnrollments.Where(x => x.ClassId == classId && x.Status == EnrollmentStatus.Active).ToListAsync());
    }

    [Fact]
    public async Task Archive_and_soft_delete_preserve_history_and_prevent_hard_delete()
    {
        await using var db = NewDb();
        var (classId, studentId) = await SetupAsync(db);
        var enrollment = await EnrollHandler(db).Handle(new EnrollStudentCommand(studentId) { ClassId = classId, AdminId = Admin }, default);
        await WithdrawHandler(db).Handle(new WithdrawStudentCommand(classId, enrollment.Id, Admin), default);
        var classroom = await db.Classes.SingleAsync(x => x.Id == classId);
        classroom.Status = ClassStatus.Archived;
        await db.SaveChangesAsync();
        Assert.Single(await db.ClassEnrollments.ToListAsync());
        classroom.Status = ClassStatus.Deleted;
        await db.SaveChangesAsync();
        var history = await ListHandler(db).Handle(new GetClassEnrollmentsQuery(classId, null, null), default);
        Assert.Single(history.Items);
        Assert.Equal("delete_blocked", (await Assert.ThrowsAsync<ClassRuleException>(() =>
            new HardDeleteClassHandler(new ClassRepository(db), new EnrollmentRepository(db), new UnitOfWork(db),
                NullLogger<HardDeleteClassHandler>.Instance).Handle(new HardDeleteClassCommand(classId), default))).Code);
        Assert.Single(await db.ClassEnrollments.ToListAsync());
    }

    [Fact]
    public async Task Two_concurrent_enrollments_have_one_winner_and_database_unique_index_exists()
    {
        var name = Guid.NewGuid().ToString();
        await using var setup = NewDb(name);
        var (classId, studentId) = await SetupAsync(setup);
        await using var first = NewDb(name);
        await using var second = NewDb(name);
        var barrier = new SaveBarrier();
        async Task<Exception?> Outcome(Task task)
        {
            try { await task; return null; }
            catch (Exception ex) { return ex; }
        }
        var results = await Task.WhenAll(
            Outcome(EnrollHandler(first, new BarrierUnitOfWork(new UnitOfWork(first), barrier)).Handle(
                new EnrollStudentCommand(studentId) { ClassId = classId, AdminId = Admin }, default)),
            Outcome(EnrollHandler(second, new BarrierUnitOfWork(new UnitOfWork(second), barrier)).Handle(
                new EnrollStudentCommand(studentId) { ClassId = classId, AdminId = Admin }, default)));
        Assert.Single(results, x => x is null);
        Assert.Single(results, x => x is ClassRuleException { Code: "concurrency_conflict" });
        var index = setup.Model.FindEntityType(typeof(ClassEnrollment))!.GetIndexes()
            .Single(x => x.Properties.Select(p => p.Name).SequenceEqual(new[] { "ClassId", "CurrentStudentId" }));
        Assert.True(index.IsUnique);
        var column = setup.Model.FindEntityType(typeof(ClassEnrollment))!.FindProperty("CurrentStudentId")!;
        Assert.Contains("WithdrawnAt", column.GetComputedColumnSql());
    }

    [Fact]
    public async Task Student_projection_ignores_stale_events_and_updates_eligibility()
    {
        await using var db = NewDb();
        var studentId = Guid.NewGuid();
        var consumer = new StudentChangedConsumer(db, TimeProvider.System, NullLogger<StudentChangedConsumer>.Instance);
        await consumer.ApplyAsync(new(studentId, "Student", "s@example.com", "User", "Active", true, true, 2));
        await consumer.ApplyAsync(new(studentId, "Old", "old@example.com", "User", "Inactive", false, false, 1));
        await consumer.ApplyAsync(new(studentId, "Duplicate", "duplicate@example.com", "User", "Inactive", false, false, 2));
        Assert.Equal("Student", (await db.Students.SingleAsync()).Name);
        await consumer.ApplyAsync(new(studentId, "Student", "s@example.com", "User", "Inactive", true, false, 3));
        Assert.False((await db.Students.SingleAsync()).IsEligible);
    }

    [Fact]
    public void Enrollment_endpoints_require_admin()
    {
        foreach (var method in typeof(ClassEnrollmentsController).GetMethods().Where(x => x.Name is "List" or "Enroll" or "Bulk" or "Import" or "Withdraw"))
            Assert.Contains(method.DeclaringType!.GetCustomAttributes(typeof(AuthorizeAttribute), false)
                .Cast<AuthorizeAttribute>(), x => x.Policy == "Admin");
    }

    [Fact]
    public void Enrollment_query_validator_rejects_invalid_filters_and_pagination()
    {
        var validator = new GetClassEnrollmentsValidator();
        Assert.False(validator.Validate(new GetClassEnrollmentsQuery(Guid.Empty, (EnrollmentStatus)99, null, 0, 101)).IsValid);
        Assert.False(validator.Validate(new GetClassEnrollmentsQuery(Guid.NewGuid(), null, null, int.MaxValue, 20)).IsValid);
        Assert.True(validator.Validate(new GetClassEnrollmentsQuery(Guid.NewGuid(), EnrollmentStatus.Withdrawn, null)).IsValid);
    }

    private sealed class SaveBarrier
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _count;
        public Task WaitAsync()
        {
            if (Interlocked.Increment(ref _count) == 2) _release.TrySetResult();
            return _release.Task;
        }
    }

    private sealed class BarrierUnitOfWork(IUnitOfWork inner, SaveBarrier barrier) : IUnitOfWork
    {
        public async Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            await barrier.WaitAsync();
            return await inner.SaveChangesAsync(ct);
        }
        public Task<IClassroomTransaction> BeginTransactionAsync(CancellationToken ct) => inner.BeginTransactionAsync(ct);
    }
}
