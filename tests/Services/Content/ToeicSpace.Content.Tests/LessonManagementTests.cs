using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MediatR;
using ToeicSpace.Content.API.Controllers;
using ToeicSpace.Content.Application;
using ToeicSpace.Content.Application.Features.Lessons.Commands.AddLessonResource;
using ToeicSpace.Content.Application.Features.Lessons.Commands.CreateLesson;
using ToeicSpace.Content.Application.Features.Lessons.Commands.DeleteLesson;
using ToeicSpace.Content.Application.Features.Lessons.Commands.PresignLessonUpload;
using ToeicSpace.Content.Application.Features.Lessons.Commands.ConfirmLessonUpload;
using ToeicSpace.Content.Application.Features.Lessons.Commands.PublishLesson;
using ToeicSpace.Content.Application.Features.Lessons.Commands.RemoveLessonResource;
using ToeicSpace.Content.Application.Features.Lessons.Commands.ReorderLessons;
using ToeicSpace.Content.Application.Features.Lessons.Commands.SetLessonFreePreview;
using ToeicSpace.Content.Application.Features.Lessons.Commands.UnpublishLesson;
using ToeicSpace.Content.Application.Features.Lessons.Commands.UpdateLesson;
using ToeicSpace.Content.Application.Features.Lessons.Queries.GetLessonDetail;
using ToeicSpace.Content.Application.Features.Lessons.Queries.GetLessonPreview;
using ToeicSpace.Content.Application.Features.Lessons.Queries.GetLessons;
using ToeicSpace.Content.Application.Features.Modules.Queries.GetModuleById;
using ToeicSpace.Content.Application.DTOs.Lessons;
using ToeicSpace.Content.Application.Interfaces.Services;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Services;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Exceptions;
using ToeicSpace.Content.Infrastructure.Persistence;
using ToeicSpace.Content.Infrastructure.Persistence.Repositories;
using ToeicSpace.Content.Infrastructure.Services;
using Xunit;

namespace ToeicSpace.Content.Tests;

public sealed class LessonManagementTests
{
    private static readonly Guid Admin = Guid.NewGuid();

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection = new("DataSource=:memory:");
        private ServiceProvider _provider = null!;
        public ContentDbContext Db { get; private set; } = null!;
        public CourseRepository Courses => new(Db);
        public CourseModuleRepository Modules => new(Db);
        public LessonRepository Lessons => new(Db);
        public LessonUploadRepository Uploads => new(Db);
        public UnitOfWork Uow => new(Db);
        public LessonScopeService Scope => new(Courses, Modules, Lessons);
        public FakeMediaStore Media { get; } = new();
        public ISender Sender => _provider.GetRequiredService<ISender>();

        public async Task<(Guid CourseId, Guid ModuleId)> SetupAsync()
        {
            await _connection.OpenAsync();
            Db = new ContentDbContext(new DbContextOptionsBuilder<ContentDbContext>().UseSqlite(_connection).Options);
            await Db.Database.EnsureCreatedAsync();
            var course = new Course { Code = "TEST", Title = "Test", Slug = "test",
                ExamType = CourseExamType.ListeningReading, Level = CourseLevel.Beginner };
            Db.Courses.Add(course);
            var module = new CourseModule { CourseId = course.Id, Title = "Module", SortOrder = 1 };
            Db.CourseModules.Add(module);
            await Db.SaveChangesAsync();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddApplication();
            services.AddSingleton(Db);
            services.AddSingleton<ILessonMediaStore>(Media);
            services.AddSingleton<TimeProvider>(TimeProvider.System);
            services.AddScoped<ICourseRepository, CourseRepository>();
            services.AddScoped<ICourseModuleRepository, CourseModuleRepository>();
            services.AddScoped<ILessonRepository, LessonRepository>();
            services.AddScoped<ILessonUploadRepository, LessonUploadRepository>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            _provider = services.BuildServiceProvider();
            return (course.Id, module.Id);
        }

        public CreateLessonHandler Creator => new(Scope, Lessons, Uploads, Uow, TimeProvider.System);
        public UpdateLessonHandler Updater => new(Scope, Uploads, Lessons, Uow, TimeProvider.System);
        public DeleteLessonHandler Deleter => new(Scope, Lessons, Uow, TimeProvider.System);
        public ReorderLessonsHandler Reorderer => new(Scope, Lessons, Uow, TimeProvider.System);
        public PresignLessonUploadHandler Presign => new(Uploads, Media, Uow, TimeProvider.System);
        public ConfirmLessonUploadHandler Confirm => new(Uploads, Media, Uow, TimeProvider.System);
        public async ValueTask DisposeAsync() { await _provider.DisposeAsync(); await Db.DisposeAsync(); await _connection.DisposeAsync(); }
    }

    private static CreateLessonCommand NewLesson(Guid courseId, Guid moduleId, string title, int? position = null) =>
        new(title, null, "# Markdown", null, null, position)
        { CourseId = courseId, ModuleId = moduleId, AdminId = Admin };

    [Fact]
    public async Task Create_insert_reorder_delete_keep_unique_contiguous_positions_and_module_detail()
    {
        await using var f = new Fixture();
        var (course, module) = await f.SetupAsync();
        var first = await f.Creator.Handle(NewLesson(course, module, "First"), default);
        var last = await f.Creator.Handle(NewLesson(course, module, "Last"), default);
        var middle = await f.Creator.Handle(NewLesson(course, module, "Middle", 2), default);
        Assert.Equal(new[] { 1, 2, 3 }, (await f.Lessons.ListAsync(module, false, default)).Select(x => x.SortOrder));
        var detail = await new GetModuleByIdHandler(f.Courses, f.Modules, f.Lessons)
            .Handle(new(course, module), default);
        Assert.Equal(new[] { first.Id, middle.Id, last.Id }, detail.Lessons!.Select(x => x.Id));
        Assert.Equal("invalid_lesson_order", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            f.Reorderer.Handle(new ReorderLessonsCommand([first.Id, first.Id, last.Id])
                { CourseId = course, ModuleId = module }, default))).Code);
        var changed = await f.Reorderer.Handle(new ReorderLessonsCommand([last.Id, first.Id, middle.Id])
            { CourseId = course, ModuleId = module }, default);
        Assert.Equal(new[] { last.Id, first.Id, middle.Id }, changed.Select(x => x.Id));
        Assert.Equal(new[] { 1, 2, 3 }, changed.Select(x => x.SortOrder));
        await f.Deleter.Handle(new(course, module, first.Id), default);
        var remaining = await new GetLessonsHandler(f.Scope, f.Lessons).Handle(new(course, module), default);
        Assert.Equal(new[] { last.Id, middle.Id }, remaining.Select(x => x.Id));
        Assert.Equal(new[] { 1, 2 }, remaining.Select(x => x.SortOrder));
        Assert.True(f.Db.Model.FindEntityType(typeof(Lesson))!.GetIndexes()
            .Single(x => x.Properties.Select(p => p.Name).SequenceEqual(new[] { "ModuleId", "SortOrder" })).IsUnique);
    }

    [Fact]
    public async Task Lesson_can_combine_markdown_primary_video_and_multiple_attachments_with_admin_preview()
    {
        await using var f = new Fixture();
        var (course, module) = await f.SetupAsync();
        async Task<string> Upload(LessonUploadKind kind, string name, string contentType, long size)
        {
            var ticket = await f.Presign.Handle(new(kind, name, contentType, size, Admin), default);
            f.Media.Objects[ticket.Key] = new StoredThumbnail(contentType, size, true);
            await f.Confirm.Handle(new(ticket.Key, Admin), default);
            return ticket.Key;
        }
        var video = await Upload(LessonUploadKind.Video, "video.mp4", "video/mp4", 1000);
        var pdf = await Upload(LessonUploadKind.Attachment, "guide.pdf", "application/pdf", 200);
        var image = await Upload(LessonUploadKind.Attachment, "chart.png", "image/png", 300);
        var video2 = await Upload(LessonUploadKind.Video, "video2.mp4", "video/mp4", 1050);
        var created = await f.Creator.Handle(new CreateLessonCommand("Lesson", "Desc", "# Markdown", [video, video2], 120, null)
            { CourseId = course, ModuleId = module, AdminId = Admin }, default);
        Assert.False(created.IsFreePreview);
        var first = await new AddLessonResourceHandler(f.Scope, f.Lessons, f.Uploads, f.Uow, TimeProvider.System)
            .Handle(new(pdf) { CourseId = course, ModuleId = module, LessonId = created.Id, AdminId = Admin }, default);
        await new AddLessonResourceHandler(f.Scope, f.Lessons, f.Uploads, f.Uow, TimeProvider.System)
            .Handle(new(image) { CourseId = course, ModuleId = module, LessonId = created.Id, AdminId = Admin }, default);
        f.Db.ChangeTracker.Clear();
        var detail = await new GetLessonDetailHandler(f.Scope).Handle(new(course, module, created.Id), default);
        Assert.Equal("# Markdown", detail.TextContent);
        Assert.Equal(new[] { video, video2 }, detail.Videos.Select(x => x.ObjectKey));
        Assert.Equal(2, detail.Resources.Count);
        var preview = await new GetLessonPreviewHandler(f.Scope, f.Media)
            .Handle(new(course, module, created.Id), default);
        Assert.Equal(2, preview.Videos.Count);
        Assert.Contains(video, preview.Videos[0].StreamUrl);
        Assert.Equal(2, preview.Attachments.Count);
        Assert.Equal(TimeSpan.FromMinutes(5), f.Media.LastReadLifetime);
        Assert.Equal(LessonStatus.Published, (await new PublishLessonHandler(f.Scope, f.Uow, TimeProvider.System)
            .Handle(new(course, module, created.Id), default)).Status);
        var toggled = await new SetLessonFreePreviewHandler(f.Scope, f.Uow, TimeProvider.System)
            .Handle(new(true) { CourseId = course, ModuleId = module, LessonId = created.Id }, default);
        Assert.True(toggled.IsFreePreview);
        var replacement = await Upload(LessonUploadKind.Video, "new.mp4", "video/mp4", 1100);
        var updated = await f.Updater.Handle(new UpdateLessonCommand("Lesson", "Desc", "# Markdown", [video2, replacement], 150, true)
            { CourseId = course, ModuleId = module, LessonId = created.Id, AdminId = Admin }, default);
        Assert.Equal(new[] { video2, replacement }, updated.Videos.Select(x => x.ObjectKey));
        Assert.Equal(2, updated.Resources.Count);
        await new RemoveLessonResourceHandler(f.Scope, f.Lessons, f.Uow, TimeProvider.System)
            .Handle(new(course, module, created.Id, first.Id), default);
        f.Db.ChangeTracker.Clear();
        Assert.Single((await new GetLessonDetailHandler(f.Scope).Handle(new(course, module, created.Id), default)).Resources);
        Assert.Equal(LessonStatus.Draft, (await new UnpublishLessonHandler(f.Scope, f.Uow, TimeProvider.System)
            .Handle(new(course, module, created.Id), default)).Status);
        await f.Deleter.Handle(new(course, module, created.Id), default);
        Assert.Empty(await f.Db.LessonResources.ToListAsync());
        Assert.NotNull(await f.Uploads.GetOwnedAsync(image, Admin, default)); // R2 cleanup owns the orphan.
    }

    [Fact]
    public async Task Ownership_archived_state_and_unconfirmed_media_are_rejected()
    {
        await using var f = new Fixture();
        var (course, module) = await f.SetupAsync();
        var created = await f.Creator.Handle(NewLesson(course, module, "Valid"), default);
        var another = new CourseModule { CourseId = course, Title = "Other", SortOrder = 2 };
        f.Db.CourseModules.Add(another);
        await f.Db.SaveChangesAsync();
        Assert.Equal("lesson_not_found", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            f.Updater.Handle(new UpdateLessonCommand("Wrong", null, null, [], null, false)
                { CourseId = course, ModuleId = another.Id, LessonId = created.Id, AdminId = Admin }, default))).Code);
        Assert.Equal("unconfirmed_media", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            f.Updater.Handle(new UpdateLessonCommand("Valid", null, null, ["unknown.mp4"], 10, false)
                { CourseId = course, ModuleId = module, LessonId = created.Id, AdminId = Admin }, default))).Code);
        (await f.Courses.GetAsync(course, default))!.Status = CourseStatus.Archived;
        await f.Db.SaveChangesAsync();
        Assert.Equal("invalid_state", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            f.Creator.Handle(NewLesson(course, module, "Blocked"), default))).Code);
        Assert.Equal("invalid_state", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            f.Deleter.Handle(new(course, module, created.Id), default))).Code);
        Assert.Equal("invalid_state", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            f.Reorderer.Handle(new ReorderLessonsCommand([created.Id])
                { CourseId = course, ModuleId = module }, default))).Code);
        Assert.Equal("invalid_state", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            f.Updater.Handle(new UpdateLessonCommand("No", null, "text", null, null, false)
                { CourseId = course, ModuleId = module, LessonId = created.Id, AdminId = Admin }, default))).Code);
        Assert.Equal("invalid_state", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            new SetLessonFreePreviewHandler(f.Scope, f.Uow, TimeProvider.System)
                .Handle(new(true) { CourseId = course, ModuleId = module, LessonId = created.Id }, default))).Code);
        Assert.Equal("# Markdown", (await new GetLessonDetailHandler(f.Scope)
            .Handle(new(course, module, created.Id), default)).TextContent);
    }

    [Fact]
    public void All_lesson_and_media_endpoints_require_admin()
    {
        foreach (var type in new[] { typeof(CourseLessonsController), typeof(LessonUploadsController) })
            Assert.Contains(type.GetCustomAttributes(typeof(AuthorizeAttribute), false)
                .Cast<AuthorizeAttribute>(), x => x.Policy == "Admin");
    }

    [Fact]
    public async Task Failed_reorder_rolls_back_lesson_positions()
    {
        await using var f = new Fixture();
        var (course, module) = await f.SetupAsync();
        var first = await f.Creator.Handle(NewLesson(course, module, "First"), default);
        var second = await f.Creator.Handle(NewLesson(course, module, "Second"), default);
        var version = (await f.Courses.GetAsync(course, default))!.Version;
        var handler = new ReorderLessonsHandler(f.Scope, f.Lessons,
            new FailSecondSave(f.Uow), TimeProvider.System);
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new ReorderLessonsCommand([second.Id, first.Id]) { CourseId = course, ModuleId = module }, default));
        f.Db.ChangeTracker.Clear();
        Assert.Equal(version, (await f.Courses.GetAsync(course, default))!.Version);
        var lessons = await f.Lessons.ListAsync(module, tracking: false, default);
        Assert.Equal(new[] { first.Id, second.Id }, lessons.Select(x => x.Id));
        Assert.Equal(new[] { 1, 2 }, lessons.Select(x => x.SortOrder));
    }

    [Fact]
    public void Real_R2_media_signature_checks_reject_spoofed_files()
    {
        Assert.True(R2LessonMediaStore.HasValidSignature("video/mp4", [0, 0, 0, 12, 102, 116, 121, 112]));
        Assert.True(R2LessonMediaStore.HasValidSignature("application/pdf", "%PDF-1.7"u8));
        Assert.True(R2LessonMediaStore.HasValidSignature("image/png", [137, 80, 78, 71, 13, 10, 26, 10]));
        Assert.False(R2LessonMediaStore.HasValidSignature("video/mp4", "<script>"u8));
        Assert.False(R2LessonMediaStore.HasValidSignature("application/pdf", "<html>"u8));
    }

    [Fact]
    public async Task Uploads_validate_type_size_signature_owner_and_expiration()
    {
        await using var f = new Fixture();
        var (course, module) = await f.SetupAsync();
        Assert.Equal("invalid_media", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            f.Presign.Handle(new(LessonUploadKind.Video, "clip.avi", "video/x-msvideo", 100, Admin), default))).Code);
        Assert.Equal("invalid_media", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            f.Presign.Handle(new(LessonUploadKind.Attachment, "too-large.pdf", "application/pdf",
                20L * 1024 * 1024 + 1, Admin), default))).Code);
        var upload = await f.Presign.Handle(new(LessonUploadKind.Video, "clip.mp4", "video/mp4", 100, Admin), default);
        Assert.StartsWith($"courses/lessons/{Admin:N}/", upload.Key);
        Assert.Equal("upload_not_found", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            f.Confirm.Handle(new(upload.Key, Guid.NewGuid()), default))).Code);
        Assert.Equal("upload_missing", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            f.Confirm.Handle(new(upload.Key, Admin), default))).Code);
        f.Media.Objects[upload.Key] = new StoredThumbnail("video/mp4", 100, false);
        Assert.Equal("upload_mismatch", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            f.Confirm.Handle(new(upload.Key, Admin), default))).Code);
        f.Media.Objects[upload.Key] = new StoredThumbnail("video/mp4", 100, true);
        Assert.Equal(upload.Key, (await f.Confirm.Handle(new(upload.Key, Admin), default)).Key);
        Assert.Equal("unconfirmed_media", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            f.Creator.Handle(new CreateLessonCommand("Lesson", null, null, [upload.Key], 30, null)
                { CourseId = course, ModuleId = module, AdminId = Guid.NewGuid() }, default))).Code);
        var expired = await f.Presign.Handle(new(LessonUploadKind.Attachment, "file.pdf", "application/pdf", 10, Admin), default);
        (await f.Uploads.GetOwnedAsync(expired.Key, Admin, default))!.ExpiresAt = DateTime.UtcNow.AddSeconds(-1);
        await f.Db.SaveChangesAsync();
        Assert.Equal("upload_expired", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            f.Confirm.Handle(new(expired.Key, Admin), default))).Code);
    }

    [Fact]
    public async Task Batch_presign_confirm_and_video_collection_support_multiple_mixed_media_uploads()
    {
        await using var f = new Fixture();
        var (course, module) = await f.SetupAsync();
        var request = new PresignLessonUploadsCommand([
            new(LessonUploadKind.Video, "one.mp4", "video/mp4", 1000),
            new(LessonUploadKind.Video, "two.mp4", "video/mp4", 1200),
            new(LessonUploadKind.Attachment, "guide.pdf", "application/pdf", 200)
        ], Admin);
        var presigned = await new PresignLessonUploadsHandler(f.Sender).Handle(request, default);
        Assert.Equal(3, presigned.PresignedCount);
        Assert.Equal(3, presigned.Items.Select(x => x.Upload!.Key).Distinct().Count());
        for (var i = 0; i < presigned.Items.Count; i++)
        {
            var upload = presigned.Items[i].Upload!;
            f.Media.Objects[upload.Key] = new StoredThumbnail(upload.ContentType, request.Files[i].SizeBytes, true);
        }
        var confirmed = await new ConfirmLessonUploadsHandler(f.Sender).Handle(
            new ConfirmLessonUploadsCommand(presigned.Items.Select(x => x.Upload!.Key).ToArray(), Admin), default);
        Assert.Equal(3, confirmed.ConfirmedCount);
        var videoKeys = presigned.Items.Where(x => x.Upload!.Kind == LessonUploadKind.Video)
            .Select(x => x.Upload!.Key).ToArray();
        var attachmentKey = presigned.Items.Single(x => x.Upload!.Kind == LessonUploadKind.Attachment).Upload!.Key;
        var lesson = await f.Creator.Handle(new CreateLessonCommand("Lesson", null, "Text", videoKeys, 40, null)
            { CourseId = course, ModuleId = module, AdminId = Admin }, default);
        var attachment = await f.Sender.Send(new AddLessonResourceCommand(attachmentKey)
            { CourseId = course, ModuleId = module, LessonId = lesson.Id, AdminId = Admin });
        Assert.Equal(2, lesson.Videos.Count);
        Assert.Equal(attachmentKey, attachment.ObjectKey);
        var replace = await f.Presign.Handle(new(LessonUploadKind.Video, "replacement.mp4", "video/mp4", 1300, Admin), default);
        f.Media.Objects[replace.Key] = new StoredThumbnail("video/mp4", 1300, true);
        await f.Confirm.Handle(new(replace.Key, Admin), default);
        var changed = await f.Sender.Send(new UpdateLessonCommand("Lesson", null, "Text", [replace.Key], 40, false)
            { CourseId = course, ModuleId = module, LessonId = lesson.Id, AdminId = Admin });
        Assert.Equal(replace.Key, Assert.Single(changed.Videos).ObjectKey);
        Assert.Equal(attachmentKey, Assert.Single(changed.Resources).ObjectKey);
    }

    [Fact]
    public async Task Single_batch_upload_request_accepts_multiple_videos_and_attachments()
    {
        await using var f = new Fixture();
        var (course, module) = await f.SetupAsync();
        var files = new LessonUploadRequest[]
        {
            new(LessonUploadKind.Video, "first.mp4", "video/mp4", 1000),
            new(LessonUploadKind.Video, "second.mp4", "video/mp4", 1200),
            new(LessonUploadKind.Attachment, "guide.pdf", "application/pdf", 200),
            new(LessonUploadKind.Attachment, "image.webp", "image/webp", 300)
        };
        var presign = await f.Sender.Send(new PresignLessonUploadsCommand(files, Admin));
        Assert.Equal(4, presign.PresignedCount);
        for (var i = 0; i < files.Length; i++)
            f.Media.Objects[presign.Items[i].Upload!.Key] = new StoredThumbnail(files[i].ContentType, files[i].SizeBytes, true);
        var keys = presign.Items.Select(x => x.Upload!.Key).ToArray();
        var confirm = await f.Sender.Send(new ConfirmLessonUploadsCommand(keys, Admin));
        Assert.Equal(4, confirm.ConfirmedCount);
        var lesson = await f.Creator.Handle(new CreateLessonCommand("Many media", null, "Text",
            keys.Take(2).ToArray(), 50, null) { CourseId = course, ModuleId = module, AdminId = Admin }, default);
        var resources = await f.Sender.Send(new AddLessonResourcesCommand(course, module, lesson.Id, Admin, keys.Skip(2).ToArray()));
        Assert.Equal(2, resources.AddedCount);
        Assert.Equal(2, lesson.Videos.Count);
        var detail = await new GetLessonDetailHandler(f.Scope).Handle(new(course, module, lesson.Id), default);
        Assert.Equal(2, detail.Videos.Count);
        Assert.Equal(2, detail.Resources.Count);
    }

    [Fact]
    public async Task Publish_requires_content_and_resources_can_be_added_or_removed_independently()
    {
        await using var f = new Fixture();
        var (course, module) = await f.SetupAsync();
        var empty = await f.Creator.Handle(new CreateLessonCommand("Empty", null, null, null, null, null)
            { CourseId = course, ModuleId = module, AdminId = Admin }, default);
        Assert.Equal("publish_requirements", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            new PublishLessonHandler(f.Scope, f.Uow, TimeProvider.System).Handle(new(course, module, empty.Id), default))).Code);
        var upload = await f.Presign.Handle(new(LessonUploadKind.Attachment, "guide.pdf", "application/pdf", 200, Admin), default);
        f.Media.Objects[upload.Key] = new StoredThumbnail("application/pdf", 200, true);
        await f.Confirm.Handle(new(upload.Key, Admin), default);
        var adder = new AddLessonResourceHandler(f.Scope, f.Lessons, f.Uploads, f.Uow, TimeProvider.System);
        Assert.Equal("unconfirmed_media", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            adder.Handle(new("unknown.pdf") { CourseId = course, ModuleId = module, LessonId = empty.Id,
                AdminId = Admin }, default))).Code);
        var resource = await adder.Handle(new(upload.Key)
            { CourseId = course, ModuleId = module, LessonId = empty.Id, AdminId = Admin }, default);
        Assert.Equal("media_in_use", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            adder.Handle(new(upload.Key) { CourseId = course, ModuleId = module, LessonId = empty.Id,
                AdminId = Admin }, default))).Code);
        Assert.Equal(LessonStatus.Published, (await new PublishLessonHandler(f.Scope, f.Uow, TimeProvider.System)
            .Handle(new(course, module, empty.Id), default)).Status);
        Assert.Equal("publish_requirements", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            new RemoveLessonResourceHandler(f.Scope, f.Lessons, f.Uow, TimeProvider.System)
                .Handle(new(course, module, empty.Id, resource.Id), default))).Code);
        await new UnpublishLessonHandler(f.Scope, f.Uow, TimeProvider.System)
            .Handle(new(course, module, empty.Id), default);
        await new RemoveLessonResourceHandler(f.Scope, f.Lessons, f.Uow, TimeProvider.System)
            .Handle(new(course, module, empty.Id, resource.Id), default);
        Assert.Empty((await new GetLessonDetailHandler(f.Scope).Handle(new(course, module, empty.Id), default)).Resources);
    }

    [Fact]
    public async Task Cleanup_deletes_only_unreferenced_media_after_grace_period()
    {
        var media = new FakeMediaStore();
        var services = new ServiceCollection();
        var databaseName = Guid.NewGuid().ToString();
        services.AddLogging();
        services.AddDbContext<ContentDbContext>(o => o.UseInMemoryDatabase(databaseName));
        services.AddScoped<ILessonUploadRepository, LessonUploadRepository>();
        services.AddScoped<ILessonRepository, LessonRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<ILessonMediaStore>(media);
        services.AddSingleton(TimeProvider.System);
        await using var provider = services.BuildServiceProvider();
        var age = DateTime.UtcNow.AddHours(-26);
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();
            var course = new Course { Code = "C", Title = "C", Slug = "c" };
            var module = new CourseModule { CourseId = course.Id, Title = "M", SortOrder = 1 };
            var lesson = new Lesson { ModuleId = module.Id, Title = "L", SortOrder = 1 };
            db.Courses.Add(course);
            db.CourseModules.Add(module);
            db.Lessons.Add(lesson);
            db.LessonVideos.Add(new LessonVideo { LessonId = lesson.Id, ObjectKey = "used.mp4", FileName = "used.mp4",
                ContentType = "video/mp4", SortOrder = 1 });
            db.LessonUploads.AddRange(
                new LessonUpload { Key = "used.mp4", FileName = "used.mp4", ContentType = "video/mp4", ConfirmedAt = age, CreatedAt = age, ExpiresAt = age },
                new LessonUpload { Key = "orphan.mp4", FileName = "orphan.mp4", ContentType = "video/mp4", ConfirmedAt = age, CreatedAt = age, ExpiresAt = age },
                new LessonUpload { Key = "abandoned.pdf", FileName = "abandoned.pdf", ContentType = "application/pdf", CreatedAt = age, ExpiresAt = age });
            await db.SaveChangesAsync();
        }
        var job = new ExpiredLessonUploadCleanup(provider.GetRequiredService<IServiceScopeFactory>(),
            TimeProvider.System, Microsoft.Extensions.Logging.Abstractions.NullLogger<ExpiredLessonUploadCleanup>.Instance);
        await job.RunOnceAsync();
        Assert.Contains("orphan.mp4", media.Deleted);
        Assert.Contains("abandoned.pdf", media.Deleted);
        Assert.DoesNotContain("used.mp4", media.Deleted);
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();
            db.LessonVideos.Remove(await db.LessonVideos.SingleAsync()); // Video replaced/removed; old object is now orphaned.
            await db.SaveChangesAsync();
        }
        await job.RunOnceAsync();
        Assert.Contains("used.mp4", media.Deleted);
    }

    private sealed class FakeMediaStore : ILessonMediaStore
    {
        public Dictionary<string, StoredThumbnail> Objects { get; } = [];
        public List<string> Deleted { get; } = [];
        public TimeSpan LastReadLifetime { get; private set; }
        public Task<string> PresignUploadAsync(string key, string contentType, TimeSpan lifetime, CancellationToken ct) =>
            Task.FromResult($"https://upload.example/{key}");
        public Task<StoredThumbnail?> HeadAsync(string key, CancellationToken ct) =>
            Task.FromResult(Objects.TryGetValue(key, out var info) ? info : null);
        public Task<string> PresignReadAsync(string key, TimeSpan lifetime, CancellationToken ct)
        {
            LastReadLifetime = lifetime;
            return Task.FromResult($"https://private.example/{key}");
        }
        public Task DeleteAsync(string key, CancellationToken ct)
        {
            Deleted.Add(key);
            Objects.Remove(key);
            return Task.CompletedTask;
        }
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
