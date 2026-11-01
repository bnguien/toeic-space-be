using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ToeicSpace.Content.API.Controllers;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Features.Courses.Queries.GetCourseById;
using ToeicSpace.Content.Application.Features.Modules.Commands.CreateModule;
using ToeicSpace.Content.Application.Features.Modules.Commands.DeleteModule;
using ToeicSpace.Content.Application.Features.Modules.Commands.ReorderModules;
using ToeicSpace.Content.Application.Features.Modules.Commands.UpdateModule;
using ToeicSpace.Content.Application.Features.Modules.Queries.GetModuleById;
using ToeicSpace.Content.Application.Features.Modules.Queries.GetModulesByCourse;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Exceptions;
using ToeicSpace.Content.Infrastructure.Persistence;
using ToeicSpace.Content.Infrastructure.Persistence.Repositories;
using Xunit;

namespace ToeicSpace.Content.Tests;

public sealed class CourseModuleTests
{
    private sealed class TestDb : IAsyncDisposable
    {
        private readonly SqliteConnection _connection = new("DataSource=:memory:");
        public ContentDbContext Db { get; private set; } = null!;
        public CourseRepository Courses => new(Db);
        public CourseModuleRepository Modules => new(Db);
        public LessonRepository Lessons => new(Db);
        public UnitOfWork Uow => new(Db);

        public async Task InitializeAsync()
        {
            await _connection.OpenAsync();
            Db = new ContentDbContext(new DbContextOptionsBuilder<ContentDbContext>()
                .UseSqlite(_connection).Options);
            await Db.Database.EnsureCreatedAsync();
        }

        public async Task<Guid> AddCourseAsync(string code = "COURSE", CourseStatus status = CourseStatus.Draft)
        {
            var course = new Course { Code = code, Title = code, Slug = code.ToLowerInvariant(),
                ExamType = CourseExamType.ListeningReading, Level = CourseLevel.Beginner, Status = status };
            Db.Courses.Add(course);
            await Db.SaveChangesAsync();
            return course.Id;
        }

        public CreateModuleHandler Creator => new(Courses, Modules, Uow, TimeProvider.System, NullLogger<CreateModuleHandler>.Instance);
        public UpdateModuleHandler Updater => new(Courses, Modules, Uow, TimeProvider.System, NullLogger<UpdateModuleHandler>.Instance);
        public DeleteModuleHandler Deleter => new(Courses, Modules, Uow, TimeProvider.System, NullLogger<DeleteModuleHandler>.Instance);
        public ReorderModulesHandler Reorderer => new(Courses, Modules, Uow, TimeProvider.System, NullLogger<ReorderModulesHandler>.Instance);

        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await _connection.DisposeAsync(); }
    }

    private static async Task<TestDb> NewDbAsync()
    {
        var fixture = new TestDb();
        await fixture.InitializeAsync();
        return fixture;
    }

    [Fact]
    public async Task Create_inserts_at_position_and_course_detail_contains_ordered_summaries()
    {
        await using var fixture = await NewDbAsync();
        var courseId = await fixture.AddCourseAsync();
        var first = await fixture.Creator.Handle(new CreateModuleCommand("Intro", null, null) { CourseId = courseId }, default);
        var third = await fixture.Creator.Handle(new CreateModuleCommand("Practice", null, null) { CourseId = courseId }, default);
        var second = await fixture.Creator.Handle(new CreateModuleCommand("Theory", "Detail", 2) { CourseId = courseId }, default);
        var list = await new GetModulesByCourseHandler(fixture.Courses, fixture.Modules)
            .Handle(new GetModulesByCourseQuery(courseId), default);
        Assert.Equal(new[] { first.Id, second.Id, third.Id }, list.Select(x => x.Id));
        Assert.Equal(new[] { 1, 2, 3 }, list.Select(x => x.SortOrder));
        Assert.Equal("Detail", (await new GetModuleByIdHandler(fixture.Courses, fixture.Modules, fixture.Lessons)
            .Handle(new GetModuleByIdQuery(courseId, second.Id), default)).Description);
        var detail = await new GetCourseByIdHandler(fixture.Courses, fixture.Modules)
            .Handle(new GetCourseByIdQuery(courseId), default);
        Assert.Equal(list.Select(x => x.Id), detail.Modules!.Select(x => x.Id));
        var index = fixture.Db.Model.FindEntityType(typeof(CourseModule))!.GetIndexes()
            .Single(x => x.Properties.Select(p => p.Name).SequenceEqual(new[] { "CourseId", "SortOrder" }));
        Assert.True(index.IsUnique);
    }

    [Fact]
    public async Task Reorder_is_transactional_and_requires_all_ids_owned_by_course()
    {
        await using var fixture = await NewDbAsync();
        var courseId = await fixture.AddCourseAsync();
        var first = await fixture.Creator.Handle(new CreateModuleCommand("First", null, null) { CourseId = courseId }, default);
        var second = await fixture.Creator.Handle(new CreateModuleCommand("Second", null, null) { CourseId = courseId }, default);
        var third = await fixture.Creator.Handle(new CreateModuleCommand("Third", null, null) { CourseId = courseId }, default);
        var another = await fixture.AddCourseAsync("OTHER");
        var foreign = await fixture.Creator.Handle(new CreateModuleCommand("Foreign", null, null) { CourseId = another }, default);
        foreach (var ids in new[] { new[] { first.Id, second.Id }, new[] { first.Id, first.Id, third.Id },
                     new[] { first.Id, second.Id, foreign.Id } })
            Assert.Equal("invalid_module_order", (await Assert.ThrowsAsync<ContentRuleException>(() =>
                fixture.Reorderer.Handle(new ReorderModulesCommand(ids) { CourseId = courseId }, default))).Code);
        var result = await fixture.Reorderer.Handle(new ReorderModulesCommand([third.Id, first.Id, second.Id])
            { CourseId = courseId }, default);
        Assert.Equal(new[] { third.Id, first.Id, second.Id }, result.Select(x => x.Id));
        Assert.Equal(new[] { 1, 2, 3 }, result.Select(x => x.SortOrder));
        Assert.Equal(1, (await fixture.Modules.GetAsync(another, foreign.Id, default))!.SortOrder);
    }

    [Fact]
    public async Task Failed_reorder_rolls_back_shift_and_course_version()
    {
        await using var fixture = await NewDbAsync();
        var courseId = await fixture.AddCourseAsync();
        var first = await fixture.Creator.Handle(new CreateModuleCommand("First", null, null) { CourseId = courseId }, default);
        var second = await fixture.Creator.Handle(new CreateModuleCommand("Second", null, null) { CourseId = courseId }, default);
        var version = (await fixture.Courses.GetAsync(courseId, default))!.Version;
        var handler = new ReorderModulesHandler(fixture.Courses, fixture.Modules,
            new FailSecondSave(fixture.Uow), TimeProvider.System, NullLogger<ReorderModulesHandler>.Instance);
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new ReorderModulesCommand([second.Id, first.Id]) { CourseId = courseId }, default));
        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(version, (await fixture.Courses.GetAsync(courseId, default))!.Version);
        var items = await fixture.Modules.ListAsync(courseId, tracking: false, default);
        Assert.Equal(new[] { first.Id, second.Id }, items.Select(x => x.Id));
        Assert.Equal(new[] { 1, 2 }, items.Select(x => x.SortOrder));
    }

    [Fact]
    public async Task Delete_compacts_order_and_update_preserves_position_and_ownership()
    {
        await using var fixture = await NewDbAsync();
        var courseId = await fixture.AddCourseAsync();
        var first = await fixture.Creator.Handle(new CreateModuleCommand("First", null, null) { CourseId = courseId }, default);
        var middle = await fixture.Creator.Handle(new CreateModuleCommand("Middle", null, null) { CourseId = courseId }, default);
        var last = await fixture.Creator.Handle(new CreateModuleCommand("Last", null, null) { CourseId = courseId }, default);
        var changed = await fixture.Updater.Handle(new UpdateModuleCommand("New title", "Updated")
            { CourseId = courseId, ModuleId = last.Id }, default);
        Assert.Equal(3, changed.SortOrder);
        Assert.Equal("Updated", changed.Description);
        await fixture.Deleter.Handle(new DeleteModuleCommand(courseId, middle.Id), default);
        var list = await new GetModulesByCourseHandler(fixture.Courses, fixture.Modules)
            .Handle(new GetModulesByCourseQuery(courseId), default);
        Assert.Equal(new[] { first.Id, last.Id }, list.Select(x => x.Id));
        Assert.Equal(new[] { 1, 2 }, list.Select(x => x.SortOrder));
        Assert.Equal("module_not_found", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            fixture.Updater.Handle(new UpdateModuleCommand("Bad", null)
                { CourseId = courseId, ModuleId = middle.Id }, default))).Code);
        var another = await fixture.AddCourseAsync("OTHER");
        Assert.Equal("module_not_found", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            fixture.Deleter.Handle(new DeleteModuleCommand(another, last.Id), default))).Code);
        Assert.Equal("module_not_found", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            new GetModuleByIdHandler(fixture.Courses, fixture.Modules, fixture.Lessons)
                .Handle(new GetModuleByIdQuery(another, last.Id), default))).Code);
    }

    [Fact]
    public async Task Module_with_lessons_cannot_be_deleted_by_cascade()
    {
        await using var fixture = await NewDbAsync();
        var courseId = await fixture.AddCourseAsync();
        var module = await fixture.Creator.Handle(new CreateModuleCommand("Module", null, null) { CourseId = courseId }, default);
        fixture.Db.Lessons.Add(new Lesson { ModuleId = module.Id, Title = "Lesson", SortOrder = 1 });
        await fixture.Db.SaveChangesAsync();
        Assert.Equal("module_in_use", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            fixture.Deleter.Handle(new DeleteModuleCommand(courseId, module.Id), default))).Code);
        fixture.Db.ChangeTracker.Clear();
        Assert.NotNull(await fixture.Modules.GetAsync(courseId, module.Id, default));
        Assert.Single(await fixture.Lessons.ListAsync(module.Id, tracking: false, default));
    }

    [Fact]
    public async Task Archived_course_is_readable_but_not_mutable()
    {
        await using var fixture = await NewDbAsync();
        var courseId = await fixture.AddCourseAsync();
        var module = await fixture.Creator.Handle(new CreateModuleCommand("Intro", null, null) { CourseId = courseId }, default);
        (await fixture.Courses.GetAsync(courseId, default))!.Status = CourseStatus.Archived;
        await fixture.Db.SaveChangesAsync();
        Assert.Single(await new GetModulesByCourseHandler(fixture.Courses, fixture.Modules)
            .Handle(new GetModulesByCourseQuery(courseId), default));
        Assert.Equal("invalid_state", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            fixture.Creator.Handle(new CreateModuleCommand("New", null, null) { CourseId = courseId }, default))).Code);
        Assert.Equal("invalid_state", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            fixture.Updater.Handle(new UpdateModuleCommand("New", null) { CourseId = courseId, ModuleId = module.Id }, default))).Code);
        Assert.Equal("invalid_state", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            fixture.Deleter.Handle(new DeleteModuleCommand(courseId, module.Id), default))).Code);
        Assert.Equal("invalid_state", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            fixture.Reorderer.Handle(new ReorderModulesCommand([module.Id]) { CourseId = courseId }, default))).Code);
    }

    [Fact]
    public async Task Invalid_ids_fields_and_positions_return_expected_errors()
    {
        await using var fixture = await NewDbAsync();
        var courseId = await fixture.AddCourseAsync();
        Assert.Equal("course_not_found", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            fixture.Creator.Handle(new CreateModuleCommand("A", null, null) { CourseId = Guid.NewGuid() }, default))).Code);
        Assert.Equal("invalid_module", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            fixture.Creator.Handle(new CreateModuleCommand("  ", null, null) { CourseId = courseId }, default))).Code);
        Assert.Equal("invalid_sort_order", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            fixture.Creator.Handle(new CreateModuleCommand("A", null, 2) { CourseId = courseId }, default))).Code);
        Assert.Equal("course_not_found", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            new GetModulesByCourseHandler(fixture.Courses, fixture.Modules)
                .Handle(new GetModulesByCourseQuery(Guid.NewGuid()), default))).Code);
        Assert.False(new ReorderModulesValidator().Validate(new ReorderModulesCommand([Guid.Empty, Guid.Empty])
            { CourseId = courseId }).IsValid);
    }

    [Fact]
    public void All_module_endpoints_require_admin()
    {
        Assert.Contains(typeof(CourseModulesController).GetCustomAttributes(typeof(AuthorizeAttribute), false)
            .Cast<AuthorizeAttribute>(), x => x.Policy == "Admin");
    }

    private sealed class FailSecondSave(IUnitOfWork inner) : IUnitOfWork
    {
        private int _calls;
        public Task<IContentTransaction> BeginTransactionAsync(CancellationToken ct) => inner.BeginTransactionAsync(ct);
        public Task<int> SaveChangesAsync(CancellationToken ct)
        {
            if (++_calls == 2) throw new InvalidOperationException("Simulated persistence failure");
            return inner.SaveChangesAsync(ct);
        }
    }
}
