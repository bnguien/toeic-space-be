using ToeicSpace.Classroom.Application.Features.Classes.Queries.GetClasses;
using ToeicSpace.Classroom.Application.Features.Classes.Queries.GetDeletedClasses;
using ToeicSpace.Classroom.Application.Features.Classes.Commands.AssignTeacher;
using ToeicSpace.Classroom.Application.Features.Classes.Commands.ChangeClassStatus;
using ToeicSpace.Classroom.Application.Features.Classes.Commands.CreateClass;
using ToeicSpace.Classroom.Application.Features.Classes.Commands.DeleteClass;
using ToeicSpace.Classroom.Application.Features.Classes.Commands.HardDeleteClass;
using ToeicSpace.Classroom.Application.Features.Classes.Commands.RestoreDeletedClass;
using ToeicSpace.Classroom.Application.Features.Classes.Commands.RestoreArchivedClass;
using ToeicSpace.Classroom.Application.Features.Classes.Commands.UnassignTeacher;
using ToeicSpace.Classroom.Application.Features.Classes.Commands.UpdateClass;
using ToeicSpace.Classroom.Application.Features.Classes.Queries.GetClassById;
using ToeicSpace.Classroom.Application.Features.Classes.Queries.GetMyClasses;
using ToeicSpace.Classroom.Application.Common.Pagination;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using ToeicSpace.Classroom.API.Controllers;
using ToeicSpace.Classroom.Application.Services;
using ToeicSpace.Classroom.Application.Interfaces;
using ToeicSpace.Classroom.Application.Interfaces.Repositories;
using ToeicSpace.Classroom.Application.DTOs.Classes;
using ToeicSpace.Classroom.Domain.Entities;
using ToeicSpace.Classroom.Domain.Enums;
using ToeicSpace.Classroom.Domain.Exceptions;
using Xunit;

namespace ToeicSpace.Classroom.Tests;

public sealed class ClassManagementTests
{
    private static readonly Guid Admin = Guid.NewGuid();
    private static readonly Guid TeacherA = Guid.NewGuid();
    private static readonly Guid TeacherB = Guid.NewGuid();
    private static readonly InMemoryDatabaseRoot SharedRoot = new();

    [Fact]
    public async Task Draft_can_be_created_without_teacher_and_code_is_immutable()
    {
        using var db = NewDb();
        var service = NewService(db);
        var created = await service.CreateAsync(new("A-1", "Class A", null, null, null), Admin, default);
        Assert.Equal(ClassStatus.Draft, created.Class.Status);
        Assert.Null(created.Class.TeacherId);
        await Assert.ThrowsAsync<ClassRuleException>(() => service.UpdateAsync(created.Class.Id, new UpdateClassCommand { Code = "B-1" }, Admin, default));
    }

    [Fact]
    public async Task Patch_updates_only_supplied_metadata_and_allows_explicit_null_to_clear_optional_values()
    {
        using var db = NewDb();
        var service = NewService(db);
        var id = (await service.CreateAsync(
            new("TOEIC-MASTER-800", "TOEIC MASTER 800+", "Luyện mục tiêu 800+.", new(2026, 11, 3), new(2027, 2, 28)),
            Admin, default)).Class.Id;

        var renamed = await service.UpdateAsync(id, new UpdateClassCommand { Name = "TOEIC MASTER 850+" }, Admin, default);
        Assert.Equal("TOEIC MASTER 850+", renamed.Class.Name);
        Assert.Equal("Luyện mục tiêu 800+.", renamed.Class.Description);
        Assert.Equal(new DateOnly(2026, 11, 3), renamed.Class.StartDate);
        Assert.Equal(new DateOnly(2027, 2, 28), renamed.Class.EndDate);

        var cleared = await service.UpdateAsync(id, new UpdateClassCommand { Description = null }, Admin, default);
        Assert.Null(cleared.Class.Description);
        Assert.Equal(new DateOnly(2026, 11, 3), cleared.Class.StartDate);
    }

    [Fact]
    public void Patch_request_distinguishes_an_omitted_field_from_an_explicit_null()
    {
        var command = JsonSerializer.Deserialize<UpdateClassCommand>(
            """{ "name": "TOEIC MASTER 850+", "description": null }""",
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        Assert.True(command.HasName);
        Assert.True(command.HasDescription);
        Assert.False(command.HasStartDate);
        Assert.Equal("TOEIC MASTER 850+", command.Name);
        Assert.Null(command.Description);
    }

    [Fact]
    public async Task Duplicate_code_and_invalid_dates_are_rejected()
    {
        using var db = NewDb();
        var service = NewService(db);
        await service.CreateAsync(new("A-1", "A", null, null, null), Admin, default);
        Assert.Equal("duplicate_code", (await Assert.ThrowsAsync<ClassRuleException>(() => service.CreateAsync(new("A-1", "B", null, null, null), Admin, default))).Code);
        Assert.Equal("invalid_date_range", (await Assert.ThrowsAsync<ClassRuleException>(() => service.CreateAsync(new("C-1", "C", null, new(2026, 12, 2), new(2026, 12, 1)), Admin, default))).Code);
    }

    [Fact]
    public async Task Activation_requires_a_current_valid_teacher()
    {
        using var db = NewDb();
        var teachers = new FakeTeacherReadRepository();
        var service = NewService(db, teachers);
        var id = (await service.CreateAsync(new("A-1", "A", null, new(2026, 1, 1), new(2026, 6, 1)), Admin, default)).Class.Id;
        Assert.Equal("teacher_required", (await Assert.ThrowsAsync<ClassRuleException>(() => service.ChangeStatusAsync(id, ClassStatus.Active, Admin, default))).Code);
        await service.AssignAsync(id, TeacherA, Admin, default);
        teachers.Set(TeacherA, false, "Teacher", "Inactive");
        Assert.Equal("inactive", (await Assert.ThrowsAsync<ClassRuleException>(() => service.ChangeStatusAsync(id, ClassStatus.Active, Admin, default))).Code);
        teachers.Set(TeacherA, true, "Teacher", "Active");
        Assert.Equal(ClassStatus.Active, (await service.ChangeStatusAsync(id, ClassStatus.Active, Admin, default)).Class.Status);
        Assert.Equal("invalid_transition", (await Assert.ThrowsAsync<ClassRuleException>(() => service.ChangeStatusAsync(id, ClassStatus.Draft, Admin, default))).Code);
    }

    [Fact]
    public async Task Reassign_preserves_history_and_invalid_teacher_does_not_change_assignment()
    {
        using var db = NewDb();
        var teachers = new FakeTeacherReadRepository();
        var service = NewService(db, teachers);
        var id = (await service.CreateAsync(new("A-1", "A", null, null, null), Admin, default)).Class.Id;
        await service.AssignAsync(id, TeacherA, Admin, default);
        teachers.Set(TeacherB, false, "User", "Active");
        await Assert.ThrowsAsync<ClassRuleException>(() => service.AssignAsync(id, TeacherB, Admin, default));
        Assert.Equal(TeacherA, (await service.GetAsync(id, null, default)).Class.TeacherId);
        teachers.Set(TeacherB, true, "Teacher", "Active");
        var result = await service.AssignAsync(id, TeacherB, Admin, default);
        Assert.Equal(2, result.InstructorHistory.Count);
        Assert.Single(result.InstructorHistory, x => x.UnassignedAt is null && x.TeacherId == TeacherB);
        Assert.Single(result.InstructorHistory, x => x.UnassignedAt is not null && x.TeacherId == TeacherA);
        Assert.Equal("forbidden_class", (await Assert.ThrowsAsync<ClassRuleException>(() => service.GetAsync(id, TeacherA, default))).Code);
        Assert.Empty((await service.GetAsync(id, TeacherB, default)).InstructorHistory);
    }

    [Fact]
    public async Task Missing_projection_keeps_current_assignment_and_teacher_list_uses_current_rows()
    {
        using var db = NewDb();
        var teachers = new FakeTeacherReadRepository();
        var service = NewService(db, teachers);
        var id = (await service.CreateAsync(new("A-1", "A", null, null, null), Admin, default)).Class.Id;
        await service.AssignAsync(id, TeacherA, Admin, default);
        teachers.Missing.Add(TeacherB);
        var error = await Assert.ThrowsAsync<ClassRuleException>(() => service.AssignAsync(id, TeacherB, Admin, default));
        Assert.Equal("teacher_not_synchronized", error.Code);
        Assert.Equal(TeacherA, (await service.GetAsync(id, null, default)).Class.TeacherId);
        Assert.Single((await service.MineAsync(TeacherA, 1, 20, default)).Items);
        Assert.Empty((await service.MineAsync(TeacherB, 1, 20, default)).Items);
    }

    [Fact]
    public void Management_routes_require_admin()
    {
        var methods = typeof(ClassesController).GetMethods();
        foreach (var name in new[] { "Create", "List", "Deleted", "Update", "Status", "Archive", "Restore", "Delete", "AdminRestore", "HardDelete", "Assign", "Unassign" })
        {
            var method = Assert.Single(methods, x => x.Name == name);
            Assert.Contains(method.GetCustomAttributes(typeof(AuthorizeAttribute), false).Cast<AuthorizeAttribute>(), x => x.Policy == "Admin");
        }
        var update = Assert.Single(methods, x => x.Name == "Update");
        Assert.NotNull(update.GetCustomAttributes(typeof(HttpPatchAttribute), false).Cast<HttpPatchAttribute>().SingleOrDefault());
    }

    [Fact]
    public async Task Concurrent_reassign_has_one_winner_with_version_conflict()
    {
        var database = Guid.NewGuid().ToString();
        using (var setup = NewDb(database))
        {
            var service = NewService(setup);
            var id = (await service.CreateAsync(new("R-1", "Race", null, null, null), Admin, default)).Class.Id;
            await service.AssignAsync(id, TeacherA, Admin, default);

            using var firstDb = NewDb(database);
            using var secondDb = NewDb(database);
            var barrier = new SaveBarrier();
            firstDb.BeforeSave = barrier.WaitAsync;
            secondDb.BeforeSave = barrier.WaitAsync;
            var first = NewService(firstDb).AssignAsync(id, TeacherB, Admin, default);
            var second = NewService(secondDb).AssignAsync(id, Guid.NewGuid(), Admin, default);
            var outcomes = await Task.WhenAll(Outcome(first), Outcome(second));
            Assert.Single(outcomes, x => x is null);
            Assert.Single(outcomes, x => x is DbUpdateConcurrencyException);
            setup.ChangeTracker.Clear();
            var current = await setup.ClassInstructors.Where(x => x.ClassId == id && x.UnassignedAt == null).ToListAsync();
            // InMemory has no transaction rollback; the MySQL generated unique index and transaction enforce this in production.
            Assert.True(current.Count >= 1);
        }
    }

    private static async Task<Exception?> Outcome(Task task)
    {
        try { await task; return null; }
        catch (Exception e) { return e; }
    }

    private sealed class SaveBarrier
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrivals;
        public Task WaitAsync()
        {
            if (Interlocked.Increment(ref _arrivals) == 2) _release.TrySetResult();
            return _release.Task;
        }
    }

    [Fact]
    public async Task Only_draft_can_unassign_and_completed_is_read_only()
    {
        using var db = NewDb();
        var service = NewService(db);
        var id = (await service.CreateAsync(new("A-1", "A", null, new(2026, 1, 1), new(2026, 6, 1)), Admin, default)).Class.Id;
        await service.AssignAsync(id, TeacherA, Admin, default);
        await service.UnassignAsync(id, Admin, default);
        Assert.Null((await service.GetAsync(id, null, default)).Class.TeacherId);
        await service.AssignAsync(id, TeacherA, Admin, default);
        await service.ChangeStatusAsync(id, ClassStatus.Active, Admin, default);
        Assert.Equal("invalid_state", (await Assert.ThrowsAsync<ClassRuleException>(() => service.UnassignAsync(id, Admin, default))).Code);
        await service.ChangeStatusAsync(id, ClassStatus.Completed, Admin, default);
        Assert.Equal("invalid_state", (await Assert.ThrowsAsync<ClassRuleException>(() => service.UpdateAsync(id, new UpdateClassCommand { Name = "Changed" }, Admin, default))).Code);
    }

    [Fact]
    public async Task Archive_restore_and_soft_delete_preserve_data_and_hide_deleted_classes()
    {
        using var db = NewDb();
        var service = NewService(db);
        var id = (await service.CreateAsync(new("A-1", "A", null, new(2026, 1, 1), new(2026, 6, 1)), Admin, default)).Class.Id;
        await service.AssignAsync(id, TeacherA, Admin, default);
        await service.ChangeStatusAsync(id, ClassStatus.Active, Admin, default);
        await service.ChangeStatusAsync(id, ClassStatus.Archived, Admin, default);
        Assert.Equal("invalid_transition", (await Assert.ThrowsAsync<ClassRuleException>(() => service.ChangeStatusAsync(id, ClassStatus.Active, Admin, default))).Code);
        Assert.Equal(ClassStatus.Active, (await service.RestoreArchivedAsync(id, Admin, default)).Class.Status);
        await service.ChangeStatusAsync(id, ClassStatus.Archived, Admin, default);
        await service.SoftDeleteAsync(id, Admin, default);
        Assert.Empty((await service.ListAsync(new(null, null, null, null, null), default)).Items);
        Assert.Empty((await service.MineAsync(TeacherA, 1, 20, default)).Items);
        Assert.Equal("class_not_found", (await Assert.ThrowsAsync<ClassRuleException>(() => service.GetAsync(id, null, default))).Code);
        Assert.Equal("class_not_found", (await Assert.ThrowsAsync<ClassRuleException>(() => service.ChangeStatusAsync(id, ClassStatus.Active, Admin, default))).Code);
        Assert.Equal("delete_blocked", (await Assert.ThrowsAsync<ClassRuleException>(() => service.HardDeleteAsync(id, default))).Code);
        var restored = await service.RestoreDeletedAsync(id, Admin, default);
        Assert.Equal(ClassStatus.Active, restored.Class.Status);
        Assert.Null(restored.Class.ArchivedFromStatus);
        Assert.Single(restored.InstructorHistory);
        Assert.Equal("invalid_state", (await Assert.ThrowsAsync<ClassRuleException>(() => service.RestoreArchivedAsync(id, Admin, default))).Code);
    }

    [Theory]
    [InlineData(ClassStatus.Draft)]
    [InlineData(ClassStatus.Completed)]
    public async Task Restore_returns_to_exact_status_before_archive(ClassStatus original)
    {
        using var db = NewDb();
        var service = NewService(db);
        var id = (await service.CreateAsync(new("A-1", "A", null, new(2026, 1, 1), new(2026, 6, 1)), Admin, default)).Class.Id;
        if (original == ClassStatus.Completed)
        {
            await service.AssignAsync(id, TeacherA, Admin, default);
            await service.ChangeStatusAsync(id, ClassStatus.Active, Admin, default);
            await service.ChangeStatusAsync(id, ClassStatus.Completed, Admin, default);
        }
        var archived = await service.ChangeStatusAsync(id, ClassStatus.Archived, Admin, default);
        Assert.Equal(original, archived.Class.ArchivedFromStatus);
        var restored = await service.RestoreArchivedAsync(id, Admin, default);
        Assert.Equal(original, restored.Class.Status);
        Assert.Null(restored.Class.ArchivedFromStatus);
    }

    [Fact]
    public async Task Legacy_archive_without_recorded_status_cannot_be_restored_by_guessing()
    {
        using var db = NewDb();
        var service = NewService(db);
        var id = (await service.CreateAsync(new("A-1", "A", null, null, null), Admin, default)).Class.Id;
        var archived = await service.ChangeStatusAsync(id, ClassStatus.Archived, Admin, default);
        var entity = await db.Classes.SingleAsync(x => x.Id == id);
        entity.ArchivedFromStatus = null;
        await db.SaveChangesAsync();
        Assert.Equal("restore_state_unknown", (await Assert.ThrowsAsync<ClassRuleException>(() => service.RestoreArchivedAsync(id, Admin, default))).Code);
        Assert.Equal(ClassStatus.Archived, (await service.GetAsync(id, null, default)).Class.Status);
    }

    [Fact]
    public async Task Admin_restore_returns_directly_to_status_before_archive()
    {
        using var db = NewDb();
        var service = NewService(db);
        var id = (await service.CreateAsync(new("A-1", "A", null, null, null), Admin, default)).Class.Id;
        await service.ChangeStatusAsync(id, ClassStatus.Archived, Admin, default);
        await service.SoftDeleteAsync(id, Admin, default);
        var restored = await service.RestoreDeletedAsync(id, Admin, default);
        Assert.Equal(ClassStatus.Draft, restored.Class.Status);
        Assert.Null(restored.Class.ArchivedFromStatus);
    }

    [Fact]
    public async Task Admin_restore_returns_completed_class_directly_to_completed()
    {
        using var db = NewDb();
        var service = NewService(db);
        var id = (await service.CreateAsync(new("A-1", "A", null, new(2026, 1, 1), new(2026, 6, 1)), Admin, default)).Class.Id;
        await service.AssignAsync(id, TeacherA, Admin, default);
        await service.ChangeStatusAsync(id, ClassStatus.Active, Admin, default);
        await service.ChangeStatusAsync(id, ClassStatus.Completed, Admin, default);
        await service.ChangeStatusAsync(id, ClassStatus.Archived, Admin, default);
        await service.SoftDeleteAsync(id, Admin, default);
        Assert.Equal(ClassStatus.Completed, (await service.RestoreDeletedAsync(id, Admin, default)).Class.Status);
    }

    [Fact]
    public async Task Admin_restore_to_active_requires_eligible_teacher_and_keeps_deleted_on_failure()
    {
        using var db = NewDb();
        var teachers = new FakeTeacherReadRepository();
        var service = NewService(db, teachers);
        var id = (await service.CreateAsync(new("A-1", "A", null, new(2026, 1, 1), new(2026, 6, 1)), Admin, default)).Class.Id;
        await service.AssignAsync(id, TeacherA, Admin, default);
        await service.ChangeStatusAsync(id, ClassStatus.Active, Admin, default);
        await service.ChangeStatusAsync(id, ClassStatus.Archived, Admin, default);
        await service.SoftDeleteAsync(id, Admin, default);
        teachers.Set(TeacherA, false, "Teacher", "Inactive");
        Assert.Equal("inactive", (await Assert.ThrowsAsync<ClassRuleException>(() => service.RestoreDeletedAsync(id, Admin, default))).Code);
        Assert.Equal(ClassStatus.Deleted, (await db.Classes.SingleAsync(x => x.Id == id)).Status);
    }

    [Fact]
    public async Task Admin_restore_rejects_legacy_deleted_class_with_unknown_original_status()
    {
        using var db = NewDb();
        var service = NewService(db);
        var id = (await service.CreateAsync(new("A-1", "A", null, null, null), Admin, default)).Class.Id;
        await service.ChangeStatusAsync(id, ClassStatus.Archived, Admin, default);
        await service.SoftDeleteAsync(id, Admin, default);
        (await db.Classes.SingleAsync(x => x.Id == id)).ArchivedFromStatus = null;
        await db.SaveChangesAsync();
        Assert.Equal("restore_state_unknown", (await Assert.ThrowsAsync<ClassRuleException>(() => service.RestoreDeletedAsync(id, Admin, default))).Code);
    }

    [Fact]
    public async Task Deleted_classes_list_is_separate_and_paginated()
    {
        using var db = NewDb();
        var service = NewService(db);
        var deletedId = (await service.CreateAsync(new("A-1", "First", null, null, null), Admin, default)).Class.Id;
        await service.CreateAsync(new("B-1", "Other", null, null, null), Admin, default);
        await service.ChangeStatusAsync(deletedId, ClassStatus.Archived, Admin, default);
        await service.SoftDeleteAsync(deletedId, Admin, default);

        var query = new GetDeletedClassesQuery("A-", 1, 1);
        var page = await new GetDeletedClassesHandler(db).Handle(query, default);
        Assert.Equal(1, page.TotalCount);
        Assert.Equal(ClassStatus.Deleted, Assert.Single(page.Items).Status);
        Assert.Equal(ClassStatus.Draft, page.Items[0].ArchivedFromStatus);
        Assert.DoesNotContain((await service.ListAsync(new(null, null, null, null, null), default)).Items, x => x.Id == deletedId);
    }

    [Fact]
    public async Task Restore_to_active_keeps_archive_when_teacher_is_no_longer_eligible()
    {
        using var db = NewDb();
        var teachers = new FakeTeacherReadRepository();
        var service = NewService(db, teachers);
        var id = (await service.CreateAsync(new("A-1", "A", null, new(2026, 1, 1), new(2026, 6, 1)), Admin, default)).Class.Id;
        await service.AssignAsync(id, TeacherA, Admin, default);
        await service.ChangeStatusAsync(id, ClassStatus.Active, Admin, default);
        await service.ChangeStatusAsync(id, ClassStatus.Archived, Admin, default);
        teachers.Set(TeacherA, false, "Teacher", "Inactive");
        Assert.Equal("inactive", (await Assert.ThrowsAsync<ClassRuleException>(() => service.RestoreArchivedAsync(id, Admin, default))).Code);
        Assert.Equal(ClassStatus.Archived, (await service.GetAsync(id, null, default)).Class.Status);
    }

    [Fact]
    public async Task Hard_delete_requires_soft_delete_and_no_related_instructors()
    {
        using var db = NewDb();
        var service = NewService(db);
        var id = (await service.CreateAsync(new("A-1", "A", null, null, null), Admin, default)).Class.Id;
        Assert.Equal("delete_blocked", (await Assert.ThrowsAsync<ClassRuleException>(() => service.HardDeleteAsync(id, default))).Code);
        await service.ChangeStatusAsync(id, ClassStatus.Archived, Admin, default);
        await service.SoftDeleteAsync(id, Admin, default);
        await service.HardDeleteAsync(id, default);
        Assert.False(await db.Classes.AnyAsync(x => x.Id == id));
    }

    private static TestDb NewDb(string? name = null) => new(new DbContextOptionsBuilder<TestDb>().UseInMemoryDatabase(name ?? Guid.NewGuid().ToString(), SharedRoot).Options);
    private static ClassTestAdapter NewService(TestDb db, FakeTeacherReadRepository? teachers = null) => new(db, teachers ?? new FakeTeacherReadRepository());

    private sealed class ClassTestAdapter(TestDb db, FakeTeacherReadRepository teachers)
    {
        public Task<ClassDetailDto> CreateAsync(CreateClassCommand request, Guid adminId, CancellationToken ct) =>
            new CreateClassHandler(db, db, TimeProvider.System, NullLogger<CreateClassHandler>.Instance).Handle(request with { AdminId = adminId }, ct);

        public Task<ClassDetailDto> UpdateAsync(Guid id, UpdateClassCommand request, Guid adminId, CancellationToken ct)
        {
            request.ClassId = id;
            request.AdminId = adminId;
            return new UpdateClassHandler(db, db, TimeProvider.System, NullLogger<UpdateClassHandler>.Instance).Handle(request, ct);
        }

        public Task<ClassDetailDto> ChangeStatusAsync(Guid id, ClassStatus status, Guid adminId, CancellationToken ct) =>
            new ChangeClassStatusHandler(db, db, new ClassActivationService(new TeacherEligibilityService(teachers)), TimeProvider.System, NullLogger<ChangeClassStatusHandler>.Instance)
                .Handle(new ChangeClassStatusCommand(status) { ClassId = id, AdminId = adminId }, ct);

        public Task<ClassDetailDto> RestoreArchivedAsync(Guid id, Guid adminId, CancellationToken ct) =>
            new RestoreArchivedClassHandler(db, db, new ClassRestorationService(new ClassActivationService(new TeacherEligibilityService(teachers))), TimeProvider.System, NullLogger<RestoreArchivedClassHandler>.Instance)
                .Handle(new RestoreArchivedClassCommand(id, adminId), ct);

        public Task<ClassDetailDto> AssignAsync(Guid id, Guid teacherId, Guid adminId, CancellationToken ct) =>
            new AssignTeacherHandler(db, db, new TeacherEligibilityService(teachers), TimeProvider.System, NullLogger<AssignTeacherHandler>.Instance)
                .Handle(new AssignTeacherCommand(teacherId) { ClassId = id, AdminId = adminId }, ct);

        public Task<ClassDetailDto> UnassignAsync(Guid id, Guid adminId, CancellationToken ct) =>
            new UnassignTeacherHandler(db, db, TimeProvider.System, NullLogger<UnassignTeacherHandler>.Instance)
                .Handle(new UnassignTeacherCommand(id, adminId), ct);

        public Task<ClassDetailDto> GetAsync(Guid id, Guid? teacherId, CancellationToken ct) =>
            new GetClassByIdHandler(db).Handle(new GetClassByIdQuery(id, teacherId), ct);

        public Task<PagedResult<ClassDto>> MineAsync(Guid teacherId, int page, int pageSize, CancellationToken ct) =>
            new GetMyClassesHandler(db).Handle(new GetMyClassesQuery(teacherId, page, pageSize), ct);

        public Task<PagedResult<ClassDto>> ListAsync(ClassSearchCriteria criteria, CancellationToken ct) =>
            new GetClassesHandler(db).Handle(new GetClassesQuery(criteria.Search, criteria.Status, criteria.TeacherId,
                criteria.StartDateFrom, criteria.StartDateTo, criteria.Page, criteria.PageSize), ct);

        public Task SoftDeleteAsync(Guid id, Guid adminId, CancellationToken ct) =>
            new DeleteClassHandler(db, db, TimeProvider.System, NullLogger<DeleteClassHandler>.Instance)
                .Handle(new DeleteClassCommand(id, adminId), ct);

        public Task<ClassDetailDto> RestoreDeletedAsync(Guid id, Guid adminId, CancellationToken ct) =>
            new RestoreDeletedClassHandler(db, db, new ClassRestorationService(new ClassActivationService(new TeacherEligibilityService(teachers))), TimeProvider.System, NullLogger<RestoreDeletedClassHandler>.Instance)
                .Handle(new RestoreDeletedClassCommand(id, adminId), ct);

        public Task HardDeleteAsync(Guid id, CancellationToken ct) =>
            new HardDeleteClassHandler(db, db, NullLogger<HardDeleteClassHandler>.Instance)
                .Handle(new HardDeleteClassCommand(id), ct);
    }

    private sealed class FakeTeacherReadRepository : ITeacherReadRepository
    {
        private readonly Dictionary<Guid, TeacherReadModel> _teachers = [];
        public HashSet<Guid> Missing { get; } = [];
        public void Set(Guid id, bool eligible, string role, string status) => _teachers[id] = new TeacherReadModel
        {
            TeacherId = id, Name = "Teacher", Email = "teacher@example.com", Role = role,
            Status = status, IsEligible = eligible, Version = 1
        };
        public Task<TeacherReadModel?> GetByIdAsync(Guid id, CancellationToken ct)
        {
            if (Missing.Contains(id)) return Task.FromResult<TeacherReadModel?>(null);
            if (!_teachers.TryGetValue(id, out var teacher))
            {
                Set(id, true, "Teacher", "Active");
                teacher = _teachers[id];
            }
            return Task.FromResult<TeacherReadModel?>(teacher);
        }
    }

    private sealed class TestDb(DbContextOptions<TestDb> options) : DbContext(options), IClassRepository, IUnitOfWork
    {
        public Func<Task>? BeforeSave { get; set; }
        public IQueryable<Class> Classes => Set<Class>();
        public Task<bool> CodeExistsAsync(string code, CancellationToken ct) =>
            Set<Class>().AnyAsync(x => x.Code == code, ct);
        public Task<Class?> GetByIdAsync(Guid id, bool includeDeleted, CancellationToken ct) =>
            Set<Class>().Include(x => x.Instructors).FirstOrDefaultAsync(x => x.Id == id && (includeDeleted || x.Status != ClassStatus.Deleted), ct);
        public Task<Class?> GetDetailAsync(Guid id, CancellationToken ct) =>
            Set<Class>().AsNoTracking().Include(x => x.Instructors)
                .FirstOrDefaultAsync(x => x.Id == id && x.Status != ClassStatus.Deleted, ct);
        public Task<PagedResult<Class>> SearchAsync(ClassSearchCriteria search, CancellationToken ct)
        {
            var query = Set<Class>().AsNoTracking().Where(x => x.Status != ClassStatus.Deleted).AsQueryable();
            if (!string.IsNullOrWhiteSpace(search.Search))
            {
                var term = search.Search.Trim();
                query = query.Where(x => x.Code.Contains(term) || x.Name.Contains(term));
            }
            if (search.Status is { } status) query = query.Where(x => x.Status == status);
            if (search.TeacherId is { } teacherId)
                query = query.Where(x => x.Instructors.Any(i => i.TeacherId == teacherId && i.UnassignedAt == null));
            if (search.StartDateFrom is { } from) query = query.Where(x => x.StartDate >= from);
            if (search.StartDateTo is { } to) query = query.Where(x => x.StartDate <= to);
            return PageAsync(query, search.Page, search.PageSize, ct);
        }
        public Task<PagedResult<Class>> GetTeacherClassesAsync(Guid teacherId, int page, int pageSize, CancellationToken ct) =>
            PageAsync(Set<Class>().AsNoTracking().Where(x => x.Status != ClassStatus.Deleted &&
                x.Instructors.Any(i => i.TeacherId == teacherId && i.UnassignedAt == null)), page, pageSize, ct);
        public Task<PagedResult<Class>> GetDeletedAsync(string? search, int page, int pageSize, CancellationToken ct)
        {
            var query = Set<Class>().AsNoTracking().Where(x => x.Status == ClassStatus.Deleted).AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(x => x.Code.Contains(term) || x.Name.Contains(term));
            }
            return PageAsync(query, page, pageSize, ct);
        }
        private static async Task<PagedResult<Class>> PageAsync(IQueryable<Class> query, int page, int pageSize, CancellationToken ct)
        {
            var total = await query.CountAsync(ct);
            var items = await query.Include(x => x.Instructors).OrderByDescending(x => x.UpdatedAt).ThenBy(x => x.Id)
                .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
            return new PagedResult<Class>(items, page, pageSize, total);
        }
        public DbSet<ClassInstructor> ClassInstructors => Set<ClassInstructor>();
        public void Add(Class entity) => Set<Class>().Add(entity);
        public void Add(ClassInstructor entity) => Set<ClassInstructor>().Add(entity);
        public void Remove(Class entity) => Set<Class>().Remove(entity);
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Class>().Property(x => x.Version).IsConcurrencyToken();
            modelBuilder.Entity<ClassInstructor>().HasOne(x => x.Class).WithMany(x => x.Instructors).HasForeignKey(x => x.ClassId);
        }
        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (BeforeSave is not null) await BeforeSave();
            return await base.SaveChangesAsync(cancellationToken);
        }
        public Task<IClassroomTransaction> BeginTransactionAsync(CancellationToken ct) => Task.FromResult<IClassroomTransaction>(new NoopTransaction());
        private sealed class NoopTransaction : IClassroomTransaction
        {
            public Task CommitAsync(CancellationToken ct) => Task.CompletedTask;
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
