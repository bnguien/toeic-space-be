using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ToeicSpace.Content.API.Controllers;
using ToeicSpace.Content.Application.Features.Courses.Queries.BrowsePublicCourses;
using ToeicSpace.Content.Application.Features.Courses.Queries.GetPublicCourse;
using ToeicSpace.Content.Application.Features.Lessons.Queries.GetPublicLessonPreview;
using ToeicSpace.Content.Domain.Exceptions;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Infrastructure.Persistence;
using ToeicSpace.Content.Infrastructure.Persistence.Repositories;
using Xunit;

namespace ToeicSpace.Content.Tests;

public sealed class PublicCourseTests
{
    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection = new("DataSource=:memory:");
        public ContentDbContext Db { get; private set; } = null!;
        public PublicCourseRepository Courses => new(Db);
        public FakeMediaStore Media { get; } = new();

        public async Task InitializeAsync()
        {
            await _connection.OpenAsync();
            Db = new ContentDbContext(new DbContextOptionsBuilder<ContentDbContext>().UseSqlite(_connection).Options);
            await Db.Database.EnsureCreatedAsync();
        }

        public Course AddCourse(string title, CourseStatus status = CourseStatus.Published,
            CourseLevel level = CourseLevel.Beginner, int? score = null)
        {
            var course = new Course { Code = Guid.NewGuid().ToString("N"), Title = title,
                Slug = Guid.NewGuid().ToString("N"), Status = status, Level = level,
                ExamType = CourseExamType.ListeningReading, TargetScore = score };
            Db.Courses.Add(course);
            return course;
        }

        public ValueTask DisposeAsync() => DisposeCoreAsync();
        private async ValueTask DisposeCoreAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class FakeMediaStore : ToeicSpace.Content.Application.Interfaces.Services.ILessonMediaStore
    {
        public List<string> ReadKeys { get; } = [];
        public TimeSpan? Lifetime { get; private set; }
        public Task<string> PresignReadAsync(string key, TimeSpan lifetime, CancellationToken ct)
        {
            ReadKeys.Add(key);
            Lifetime = lifetime;
            return Task.FromResult($"https://signed.example/{ReadKeys.Count}");
        }
        public Task<string> PresignUploadAsync(string key, string type, TimeSpan lifetime, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task<ToeicSpace.Content.Application.Interfaces.Services.StoredThumbnail?> HeadAsync(string key, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task DeleteAsync(string key, CancellationToken ct) => throw new NotSupportedException();
    }

    [Fact]
    public async Task Browse_only_returns_published_courses_and_does_not_sign_hidden_thumbnails()
    {
        await using var f = new Fixture();
        await f.InitializeAsync();
        f.AddCourse("Public").ThumbnailKey = "visible";
        f.AddCourse("Draft", CourseStatus.Draft).ThumbnailKey = "private";
        f.AddCourse("Archived", CourseStatus.Archived);
        await f.Db.SaveChangesAsync();

        var result = await new BrowsePublicCoursesHandler(f.Courses, f.Media)
            .Handle(new(null, null, null, null), default);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal("Public", Assert.Single(result.Items).Title);
        Assert.Equal(new[] { "visible" }, f.Media.ReadKeys);
        Assert.Equal("https://signed.example/1", result.Items[0].ThumbnailUrl);
    }

    [Fact]
    public async Task Browse_filters_search_type_level_and_score_and_paginates()
    {
        await using var f = new Fixture();
        await f.InitializeAsync();
        f.AddCourse("TOEIC Alpha", score: 500);
        f.AddCourse("TOEIC Beta", score: 500);
        f.AddCourse("TOEIC Advanced", level: CourseLevel.Advanced, score: 500);
        f.AddCourse("Other", score: 500);
        f.AddCourse("TOEIC Different", score: 600);
        await f.Db.SaveChangesAsync();

        var handler = new BrowsePublicCoursesHandler(f.Courses, f.Media);
        var first = await handler.Handle(new(" TOEIC ", CourseExamType.ListeningReading,
            CourseLevel.Beginner, 500, 1, 1), default);
        var second = await handler.Handle(new("TOEIC", CourseExamType.ListeningReading,
            CourseLevel.Beginner, 500, 2, 1), default);
        Assert.Equal(2, first.TotalCount);
        Assert.Equal(2, second.TotalCount);
        Assert.NotEqual(first.Items[0].Id, second.Items[0].Id);
        Assert.Equal(2, second.Page);
        Assert.Equal(1, second.PageSize);
    }

    [Fact]
    public async Task Detail_returns_404_for_unknown_draft_and_archived_courses_without_signing()
    {
        await using var f = new Fixture();
        await f.InitializeAsync();
        var draft = f.AddCourse("Draft", CourseStatus.Draft);
        draft.ThumbnailKey = "secret";
        var archived = f.AddCourse("Archived", CourseStatus.Archived);
        await f.Db.SaveChangesAsync();
        var handler = new GetPublicCourseHandler(f.Courses, f.Media);
        foreach (var id in new[] { Guid.NewGuid(), draft.Id, archived.Id })
            Assert.Equal(404, (await Assert.ThrowsAsync<ContentRuleException>(() =>
                handler.Handle(new(id), default))).StatusCode);
        Assert.Empty(f.Media.ReadKeys);
    }

    [Fact]
    public async Task Detail_orders_modules_and_published_lessons_without_private_content()
    {
        await using var f = new Fixture();
        await f.InitializeAsync();
        var course = f.AddCourse("Public");
        course.ThumbnailKey = "thumbnail";
        var late = new CourseModule { CourseId = course.Id, Title = "Second", SortOrder = 2 };
        var early = new CourseModule { CourseId = course.Id, Title = "First", SortOrder = 1 };
        f.Db.CourseModules.AddRange(late, early);
        f.Db.Lessons.AddRange(
            new Lesson { ModuleId = early.Id, Title = "Later", SortOrder = 2, Status = LessonStatus.Published,
                TextContent = "PRIVATE MARKDOWN", IsFreePreview = true },
            new Lesson { ModuleId = early.Id, Title = "First", SortOrder = 1, Status = LessonStatus.Published },
            new Lesson { ModuleId = early.Id, Title = "Draft", SortOrder = 3, Status = LessonStatus.Draft },
            new Lesson { ModuleId = late.Id, Title = "Other", SortOrder = 1, Status = LessonStatus.Published });
        await f.Db.SaveChangesAsync();

        var result = await new GetPublicCourseHandler(f.Courses, f.Media).Handle(new(course.Id), default);
        Assert.Equal(new[] { "First", "Second" }, result.Modules.Select(x => x.Title));
        Assert.Equal(new[] { "First", "Later" }, result.Modules[0].Lessons.Select(x => x.Title));
        Assert.Equal("https://signed.example/1", result.ThumbnailUrl);
        Assert.DoesNotContain("PRIVATE MARKDOWN", System.Text.Json.JsonSerializer.Serialize(result));
        Assert.DoesNotContain("thumbnail", System.Text.Json.JsonSerializer.Serialize(result));
    }

    [Fact]
    public async Task Preview_rejects_draft_archived_wrong_course_and_non_free_lessons_before_signing()
    {
        await using var f = new Fixture();
        await f.InitializeAsync();
        var published = f.AddCourse("Published");
        var draftCourse = f.AddCourse("Draft", CourseStatus.Draft);
        var archived = f.AddCourse("Archived", CourseStatus.Archived);
        var modules = new[] { published, draftCourse, archived }
            .Select(x => new CourseModule { CourseId = x.Id, Title = "Module", SortOrder = 1 }).ToArray();
        f.Db.CourseModules.AddRange(modules);
        Lesson Add(CourseModule module, LessonStatus status, bool free)
        {
            var lesson = new Lesson { ModuleId = module.Id, Title = "Lesson", SortOrder = 1,
                Status = status, IsFreePreview = free, TextContent = "private" };
            f.Db.Lessons.Add(lesson);
            return lesson;
        }
        var eligible = Add(modules[0], LessonStatus.Published, true);
        var draftLesson = Add(modules[0], LessonStatus.Draft, true);
        draftLesson.SortOrder = 2;
        var gated = Add(modules[0], LessonStatus.Published, false);
        gated.SortOrder = 3;
        var hiddenCourseLesson = Add(modules[1], LessonStatus.Published, true);
        var archivedLesson = Add(modules[2], LessonStatus.Published, true);
        await f.Db.SaveChangesAsync();
        var handler = new GetPublicLessonPreviewHandler(f.Courses, f.Media);
        foreach (var (course, lesson) in new[]
        {
            (published.Id, draftLesson.Id), (published.Id, gated.Id),
            (draftCourse.Id, hiddenCourseLesson.Id), (archived.Id, archivedLesson.Id),
            (draftCourse.Id, eligible.Id), (published.Id, Guid.NewGuid())
        })
            Assert.Equal(404, (await Assert.ThrowsAsync<ContentRuleException>(() =>
                handler.Handle(new(course, lesson), default))).StatusCode);
        Assert.Empty(f.Media.ReadKeys);
    }

    [Fact]
    public async Task Preview_signs_only_eligible_media_and_returns_ordered_metadata_without_keys()
    {
        await using var f = new Fixture();
        await f.InitializeAsync();
        var course = f.AddCourse("Public");
        var module = new CourseModule { CourseId = course.Id, Title = "Module", SortOrder = 1 };
        var lesson = new Lesson { ModuleId = module.Id, Title = "Preview", SortOrder = 1,
            Status = LessonStatus.Published, IsFreePreview = true, TextContent = "# Public markdown" };
        f.Db.CourseModules.Add(module);
        f.Db.Lessons.Add(lesson);
        f.Db.LessonVideos.AddRange(
            new LessonVideo { LessonId = lesson.Id, ObjectKey = "second-key", FileName = "second.mp4", ContentType = "video/mp4", SortOrder = 2 },
            new LessonVideo { LessonId = lesson.Id, ObjectKey = "first-key", FileName = "first.mp4", ContentType = "video/mp4", SortOrder = 1 });
        f.Db.LessonResources.Add(new LessonResource { LessonId = lesson.Id, ObjectKey = "attachment-key",
            FileName = "file.pdf", ContentType = "application/pdf" });
        await f.Db.SaveChangesAsync();

        var preview = await new GetPublicLessonPreviewHandler(f.Courses, f.Media)
            .Handle(new(course.Id, lesson.Id), default);
        Assert.Equal("# Public markdown", preview.TextContent);
        Assert.Equal(new[] { "first.mp4", "second.mp4" }, preview.Videos.Select(x => x.FileName));
        Assert.Equal(new[] { "first-key", "second-key", "attachment-key" }, f.Media.ReadKeys);
        Assert.Equal(TimeSpan.FromMinutes(5), f.Media.Lifetime);
        var json = System.Text.Json.JsonSerializer.Serialize(preview);
        Assert.DoesNotContain("ObjectKey", json);
        Assert.DoesNotContain("first-key", json);
        Assert.DoesNotContain("attachment-key", json);
    }

    [Fact]
    public void Public_routes_allow_anonymous_without_changing_admin_authorization()
    {
        var publicController = typeof(PublicCoursesController);
        Assert.NotNull(Attribute.GetCustomAttribute(publicController, typeof(AllowAnonymousAttribute)));
        Assert.Equal("api/v1/public/courses", publicController.GetCustomAttributes(typeof(RouteAttribute), true)
            .Cast<RouteAttribute>().Single().Template);
        var routes = publicController.GetMethods().Where(x => x.IsDefined(typeof(HttpGetAttribute), true))
            .Select(x => x.GetCustomAttributes(typeof(HttpGetAttribute), true)
                .Cast<HttpGetAttribute>().Single().Template ?? "").ToArray();
        Assert.Equal(3, routes.Length);
        Assert.Contains("", routes);
        Assert.Contains("{id:guid}", routes);
        Assert.Contains("{courseId:guid}/lessons/{lessonId:guid}/preview", routes);
        Assert.Equal("Admin", typeof(CoursesController).GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>().Single().Policy);
    }

    [Fact]
    public async Task Browse_returns_empty_page_with_consistent_metadata_when_no_courses_match()
    {
        await using var f = new Fixture();
        await f.InitializeAsync();
        f.AddCourse("Hidden", CourseStatus.Draft);
        await f.Db.SaveChangesAsync();
        var page = await new BrowsePublicCoursesHandler(f.Courses, f.Media)
            .Handle(new("missing", null, null, null, 3, 10), default);
        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
        Assert.Equal(3, page.Page);
        Assert.Equal(10, page.PageSize);
        Assert.Empty(f.Media.ReadKeys);
    }

    [Fact]
    public async Task Detail_includes_non_free_published_lesson_summary_but_not_its_content()
    {
        await using var f = new Fixture();
        await f.InitializeAsync();
        var course = f.AddCourse("Public");
        var module = new CourseModule { CourseId = course.Id, Title = "Module", SortOrder = 1 };
        f.Db.CourseModules.Add(module);
        f.Db.Lessons.Add(new Lesson { ModuleId = module.Id, Title = "Locked", SortOrder = 1,
            Status = LessonStatus.Published, IsFreePreview = false, TextContent = "locked-body" });
        await f.Db.SaveChangesAsync();
        var detail = await new GetPublicCourseHandler(f.Courses, f.Media).Handle(new(course.Id), default);
        Assert.False(Assert.Single(Assert.Single(detail.Modules).Lessons).IsFreePreview);
        Assert.DoesNotContain("locked-body", System.Text.Json.JsonSerializer.Serialize(detail));
    }
}
