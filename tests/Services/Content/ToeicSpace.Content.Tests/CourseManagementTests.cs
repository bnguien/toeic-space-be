using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ToeicSpace.Content.API.Controllers;
using ToeicSpace.Content.API.Middlewares;
using ToeicSpace.Content.Application.Features.Courses.Commands.ArchiveCourse;
using ToeicSpace.Content.Application.Features.Courses.Commands.CreateCourse;
using ToeicSpace.Content.Application.Features.Courses.Commands.PublishCourse;
using ToeicSpace.Content.Application.Features.Courses.Commands.RestoreCourse;
using ToeicSpace.Content.Application.Features.Courses.Commands.UpdateCourse;
using ToeicSpace.Content.Application.Features.Courses.Queries.GetCourses;
using ToeicSpace.Content.Application.Features.Courses.Queries.GetCourseById;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Exceptions;
using ToeicSpace.Content.Infrastructure.Persistence;
using ToeicSpace.Content.Infrastructure.Persistence.Repositories;
using Xunit;

namespace ToeicSpace.Content.Tests;

public sealed class CourseManagementTests
{
    private static ContentDbContext NewDb() => new(new DbContextOptionsBuilder<ContentDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static CreateCourseCommand Create(string code = "LR-800", string slug = "lr-800", string? key = null,
        CourseExamType exam = CourseExamType.ListeningReading, int? score = 800) =>
        new(code, "TOEIC 800", slug, "Short", "Description", key, exam, CourseLevel.Intermediate, score,
            ["Listening", "Reading"], ["Score 800"], []);

    private static CreateCourseHandler Creator(ContentDbContext db) =>
        new(new CourseRepository(db), new ThumbnailUploadRepository(db), new UnitOfWork(db), TimeProvider.System,
            NullLogger<CreateCourseHandler>.Instance);

    [Fact]
    public async Task Creates_draft_and_rejects_duplicate_code_slug_and_invalid_metadata()
    {
        await using var db = NewDb();
        var created = await Creator(db).Handle(Create(), default);
        Assert.Equal(CourseStatus.Draft, created.Status);
        Assert.Null(created.PublishedAt);
        Assert.Equal("duplicate_code", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            Creator(db).Handle(Create("LR-800", "another"), default))).Code);
        Assert.Equal("duplicate_slug", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            Creator(db).Handle(Create("SW-400", "lr-800"), default))).Code);
        Assert.Equal("invalid_target_score", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            Creator(db).Handle(Create("SW", "sw", exam: CourseExamType.SpeakingWriting, score: 900), default))).Code);
        Assert.Equal("invalid_course", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            Creator(db).Handle(Create("BAD", "Invalid Slug"), default))).Code);
        Assert.Equal("course_not_found", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            new GetCourseByIdHandler(new CourseRepository(db), new CourseModuleRepository(db))
                .Handle(new GetCourseByIdQuery(Guid.NewGuid()), default))).Code);
        var indexes = db.Model.FindEntityType(typeof(Course))!.GetIndexes();
        Assert.True(indexes.Single(x => x.Properties.Count == 1 && x.Properties[0].Name == "Code").IsUnique);
        Assert.True(indexes.Single(x => x.Properties.Count == 1 && x.Properties[0].Name == "Slug").IsUnique);
    }

    [Fact]
    public async Task Updates_course_without_changing_code_and_requires_confirmed_thumbnail()
    {
        await using var db = NewDb();
        var created = await Creator(db).Handle(Create(), default);
        await Creator(db).Handle(Create("SECOND", "second"), default);
        var updater = new UpdateCourseHandler(new CourseRepository(db), new ThumbnailUploadRepository(db),
            new UnitOfWork(db), TimeProvider.System, NullLogger<UpdateCourseHandler>.Instance);
        UpdateCourseCommand Command(string? code, string slug, string? key = null) =>
            new(code, "Updated", slug, "Short", "Description", key,
                CourseExamType.ListeningReading, CourseLevel.Advanced, 900, ["Reading"], ["Score"], [] ) { CourseId = created.Id };
        Assert.Equal("immutable_code", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            updater.Handle(Command("OTHER", "new-slug"), default))).Code);
        Assert.Equal("unconfirmed_thumbnail", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            updater.Handle(Command(null, "new-slug", "courses/thumbnails/unconfirmed.jpg"), default))).Code);
        Assert.Equal("duplicate_slug", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            updater.Handle(Command(null, "second"), default))).Code);
        var updated = await updater.Handle(Command(created.Code, "new-slug"), default);
        Assert.Equal(created.Code, updated.Code);
        Assert.Equal("new-slug", updated.Slug);
        Assert.Equal(CourseLevel.Advanced, updated.Level);
    }

    [Fact]
    public async Task Publish_archive_restore_only_follow_allowed_transitions_and_search_filters()
    {
        await using var db = NewDb();
        var course = await Creator(db).Handle(Create(), default);
        var other = await Creator(db).Handle(Create("SW", "sw", exam: CourseExamType.SpeakingWriting, score: 350), default);
        var repository = new CourseRepository(db);
        var uow = new UnitOfWork(db);
        var publisher = new PublishCourseHandler(repository, uow, TimeProvider.System, NullLogger<PublishCourseHandler>.Instance);
        var archiver = new ArchiveCourseHandler(repository, uow, TimeProvider.System, NullLogger<ArchiveCourseHandler>.Instance);
        var restorer = new RestoreCourseHandler(repository, uow, TimeProvider.System, NullLogger<RestoreCourseHandler>.Instance);
        Assert.Equal("invalid_state", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            restorer.Handle(new RestoreCourseCommand(course.Id), default))).Code);
        var published = await publisher.Handle(new PublishCourseCommand(course.Id), default);
        Assert.NotNull(published.PublishedAt);
        var archived = await archiver.Handle(new ArchiveCourseCommand(course.Id), default);
        Assert.Equal(CourseStatus.Archived, archived.Status);
        Assert.Equal(published.PublishedAt, archived.PublishedAt);
        Assert.Equal(published.Description, archived.Description);
        Assert.Equal("invalid_state", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            publisher.Handle(new PublishCourseCommand(course.Id), default))).Code);
        Assert.Equal(CourseStatus.Draft, (await restorer.Handle(new RestoreCourseCommand(course.Id), default)).Status);
        Assert.Equal("invalid_state", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            archiver.Handle(new ArchiveCourseCommand(course.Id), default))).Code);

        var handler = new GetCoursesHandler(repository);
        var filtered = await handler.Handle(new GetCoursesQuery("  TOEIC  ", CourseStatus.Draft,
            CourseLevel.Advanced, CourseExamType.ListeningReading), default);
        Assert.Empty(filtered.Items); // Original course is Intermediate, and status is Draft.
        var result = await handler.Handle(new GetCoursesQuery("LR-800", CourseStatus.Draft,
            CourseLevel.Intermediate, CourseExamType.ListeningReading, 1, 1), default);
        Assert.Equal(course.Id, Assert.Single(result.Items).Id);
        Assert.Equal(1, result.TotalCount);
        Assert.Empty((await handler.Handle(new GetCoursesQuery("no-match", null, null, null), default)).Items);
        Assert.Equal(other.Id, Assert.Single((await handler.Handle(new GetCoursesQuery(null, null, null,
            CourseExamType.SpeakingWriting, 1, 1), default)).Items).Id);
        Assert.False(new GetCoursesValidator().Validate(new GetCoursesQuery(null, null, null, null, 0, 200)).IsValid);
    }

    [Fact]
    public async Task Publishing_requires_complete_metadata_and_valid_scores_for_each_exam()
    {
        await using var db = NewDb();
        var draft = await Creator(db).Handle(new CreateCourseCommand("X", "X", "x", null, null, null,
            CourseExamType.FourSkills, CourseLevel.Beginner, 1390, [], [], []), default);
        var publisher = new PublishCourseHandler(new CourseRepository(db), new UnitOfWork(db), TimeProvider.System,
            NullLogger<PublishCourseHandler>.Instance);
        Assert.Equal("publish_requirements", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            publisher.Handle(new PublishCourseCommand(draft.Id), default))).Code);
        Assert.Equal("invalid_target_score", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            Creator(db).Handle(Create("FOUR", "four", exam: CourseExamType.FourSkills, score: 1400), default))).Code);
    }

    [Fact]
    public async Task Admin_routes_and_problem_details_use_standardized_errors()
    {
        foreach (var controller in new[] { typeof(CoursesController), typeof(UploadsController) })
            Assert.Contains(controller.GetCustomAttributes(typeof(AuthorizeAttribute), false)
                .Cast<AuthorizeAttribute>(), x => x.Policy == "Admin");
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        await new ContentExceptionHandler(NullLogger<ContentExceptionHandler>.Instance)
            .TryHandleAsync(context, new ContentRuleException("duplicate_slug", "Already exists.", 409), default);
        Assert.Equal(409, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal("duplicate_slug", json.RootElement.GetProperty("code").GetString());
    }
}
