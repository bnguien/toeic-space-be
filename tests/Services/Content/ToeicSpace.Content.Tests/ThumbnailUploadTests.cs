using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ToeicSpace.Content.Application.Features.Courses.Commands.ConfirmThumbnail;
using ToeicSpace.Content.Application.Features.Courses.Commands.CreateCourse;
using ToeicSpace.Content.Application.Features.Courses.Commands.PresignThumbnail;
using ToeicSpace.Content.Application.Features.Courses.Commands.UpdateCourse;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Interfaces.Services;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Exceptions;
using ToeicSpace.Content.Infrastructure.Persistence;
using ToeicSpace.Content.Infrastructure.Persistence.Repositories;
using ToeicSpace.Content.Infrastructure.Services;
using Xunit;

namespace ToeicSpace.Content.Tests;

public sealed class ThumbnailUploadTests
{
    private static readonly Guid Admin = Guid.NewGuid();
    private static CreateCourseCommand Course(string key) => new("C-1", "Course", "course", "Short", "Description",
        key, CourseExamType.ListeningReading, CourseLevel.Beginner, 800, ["Reading"], ["Goal"], [], Admin);
    private static ContentDbContext NewDb() => new(new DbContextOptionsBuilder<ContentDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public async Task Presign_validates_type_size_and_confirmation_verifies_owner_object_and_signature()
    {
        await using var db = NewDb();
        var storage = new FakeStorage();
        var uploads = new ThumbnailUploadRepository(db);
        var presign = new PresignThumbnailHandler(uploads, storage, new UnitOfWork(db), TimeProvider.System);
        var confirm = new ConfirmThumbnailHandler(uploads, storage, new UnitOfWork(db), TimeProvider.System);
        var first = await presign.Handle(new PresignThumbnailCommand("image/png", 200, Admin), default);
        var second = await presign.Handle(new PresignThumbnailCommand("image/png", 200, Admin), default);
        Assert.NotEqual(first.Key, second.Key);
        Assert.StartsWith("courses/thumbnails/", first.Key);
        Assert.EndsWith(".png", first.Key);
        Assert.Contains(first.Key, first.UploadUrl);
        Assert.Equal(TimeSpan.FromMinutes(10), storage.LastExpiration);
        Assert.Equal("upload_not_found", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            confirm.Handle(new ConfirmThumbnailCommand(first.Key, Guid.NewGuid()), default))).Code);
        Assert.Equal("upload_missing", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            confirm.Handle(new ConfirmThumbnailCommand(first.Key, Admin), default))).Code);
        storage.Objects[first.Key] = new StoredThumbnail("image/png", 200, false);
        Assert.Equal("upload_mismatch", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            confirm.Handle(new ConfirmThumbnailCommand(first.Key, Admin), default))).Code);
        storage.Objects[first.Key] = new StoredThumbnail("image/png", 201, true);
        Assert.Equal("upload_mismatch", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            confirm.Handle(new ConfirmThumbnailCommand(first.Key, Admin), default))).Code);
        storage.Objects[first.Key] = new StoredThumbnail("image/png", 200, true);
        Assert.Equal(first.Key, (await confirm.Handle(new ConfirmThumbnailCommand(first.Key, Admin), default)).Key);
        Assert.NotNull((await uploads.GetAsync(first.Key, default))!.ConfirmedAt);
        Assert.True(await uploads.IsConfirmedByAsync(first.Key, Admin, default));
        Assert.False(await uploads.IsConfirmedByAsync(first.Key, Guid.NewGuid(), default));
        Assert.False(await uploads.IsConfirmedByAsync(second.Key, Admin, default));
        Assert.Equal("unconfirmed_thumbnail", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            new CreateCourseHandler(new CourseRepository(db), uploads, new UnitOfWork(db), TimeProvider.System,
                NullLogger<CreateCourseHandler>.Instance).Handle(Course(second.Key), default))).Code);
        Assert.Equal(first.Key, (await new CreateCourseHandler(new CourseRepository(db), uploads, new UnitOfWork(db),
            TimeProvider.System, NullLogger<CreateCourseHandler>.Instance).Handle(Course(first.Key), default)).ThumbnailKey);
        Assert.Equal("invalid_thumbnail", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            presign.Handle(new PresignThumbnailCommand("image/gif", 200, Admin), default))).Code);
        Assert.Equal("invalid_thumbnail", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            presign.Handle(new PresignThumbnailCommand("image/png", 5 * 1024 * 1024 + 1, Admin), default))).Code);
    }

    [Fact]
    public async Task Expired_upload_cannot_be_confirmed_and_replacing_thumbnail_keeps_old_object()
    {
        await using var db = NewDb();
        var storage = new FakeStorage();
        var uploads = new ThumbnailUploadRepository(db);
        var presign = new PresignThumbnailHandler(uploads, storage, new UnitOfWork(db), TimeProvider.System);
        var confirm = new ConfirmThumbnailHandler(uploads, storage, new UnitOfWork(db), TimeProvider.System);
        var old = await presign.Handle(new PresignThumbnailCommand("image/jpeg", 120, Admin), default);
        storage.Objects[old.Key] = new StoredThumbnail("image/jpeg", 120, true);
        await confirm.Handle(new ConfirmThumbnailCommand(old.Key, Admin), default);
        var course = await new CreateCourseHandler(new CourseRepository(db), uploads, new UnitOfWork(db), TimeProvider.System,
            NullLogger<CreateCourseHandler>.Instance).Handle(Course(old.Key), default);
        var replacement = await presign.Handle(new PresignThumbnailCommand("image/webp", 140, Admin), default);
        storage.Objects[replacement.Key] = new StoredThumbnail("image/webp", 140, true);
        await confirm.Handle(new ConfirmThumbnailCommand(replacement.Key, Admin), default);
        var updated = await new UpdateCourseHandler(new CourseRepository(db), uploads, new UnitOfWork(db), TimeProvider.System,
            NullLogger<UpdateCourseHandler>.Instance).Handle(new UpdateCourseCommand(course.Code, course.Title,
            course.Slug, course.ShortDescription, course.Description, replacement.Key, course.ExamType,
            course.Level, course.TargetScore, course.Skills.ToList(), course.Objectives.ToList(),
            course.Prerequisites.ToList()) { CourseId = course.Id, AdminId = Admin }, default);
        Assert.Equal(replacement.Key, updated.ThumbnailKey);
        Assert.Contains(old.Key, storage.Objects.Keys);

        var expired = await presign.Handle(new PresignThumbnailCommand("image/png", 100, Admin), default);
        (await uploads.GetAsync(expired.Key, default))!.ExpiresAt = DateTime.UtcNow.AddSeconds(-1);
        await db.SaveChangesAsync();
        Assert.Equal("upload_expired", (await Assert.ThrowsAsync<ContentRuleException>(() =>
            confirm.Handle(new ConfirmThumbnailCommand(expired.Key, Admin), default))).Code);
    }

    [Fact]
    public async Task Cleanup_removes_only_abandoned_uploads_after_grace_period()
    {
        var name = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        var storage = new FakeStorage();
        services.AddLogging();
        services.AddSingleton<IThumbnailObjectStore>(storage);
        services.AddSingleton(TimeProvider.System);
        services.AddDbContext<ContentDbContext>(o => o.UseInMemoryDatabase(name));
        services.AddScoped<IThumbnailUploadRepository, ThumbnailUploadRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        await using var provider = services.BuildServiceProvider();
        string abandonedKey, confirmedKey, recentKey;
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();
            abandonedKey = "courses/thumbnails/abandoned.jpg";
            confirmedKey = "courses/thumbnails/confirmed.jpg";
            recentKey = "courses/thumbnails/recent.jpg";
            db.ThumbnailUploads.AddRange(
                new() { Key = abandonedKey, ExpiresAt = DateTime.UtcNow.AddHours(-25) },
                new() { Key = confirmedKey, ExpiresAt = DateTime.UtcNow.AddHours(-25), ConfirmedAt = DateTime.UtcNow.AddHours(-26) },
                new() { Key = recentKey, ExpiresAt = DateTime.UtcNow.AddHours(-1) });
            await db.SaveChangesAsync();
        }
        await new ExpiredThumbnailCleanup(provider.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System,
            NullLogger<ExpiredThumbnailCleanup>.Instance).RunOnceAsync();
        Assert.Equal([abandonedKey], storage.DeletedKeys);
        await using var verify = provider.CreateAsyncScope();
        var remaining = await verify.ServiceProvider.GetRequiredService<ContentDbContext>().ThumbnailUploads
            .Select(x => x.Key).ToListAsync();
        Assert.DoesNotContain(abandonedKey, remaining);
        Assert.Contains(confirmedKey, remaining);
        Assert.Contains(recentKey, remaining);
    }

    [Fact]
    public async Task Multiple_thumbnails_can_be_presigned_and_confirmed_before_any_course_exists()
    {
        await using var db = NewDb();
        var storage = new FakeStorage();
        var uploads = new ThumbnailUploadRepository(db);
        var presign = new PresignThumbnailHandler(uploads, storage, new UnitOfWork(db), TimeProvider.System);
        var keys = new List<string>();
        foreach (var (type, bytes, signature) in new[]
        {
            ("image/jpeg", 120L, true), ("image/webp", 150L, true)
        })
        {
            var ticket = await presign.Handle(new PresignThumbnailCommand(type, bytes, Admin), default);
            keys.Add(ticket.Key);
            storage.Objects[ticket.Key] = new StoredThumbnail(type, bytes, signature);
        }
        Assert.Empty(await db.Courses.ToListAsync());

        var batch = await new ConfirmThumbnailsHandler(uploads, storage, new UnitOfWork(db), TimeProvider.System)
            .Handle(new ConfirmThumbnailsCommand(keys, Admin), default);
        Assert.Equal(2, batch.TotalCount);
        Assert.Equal(2, batch.ConfirmedCount);
        Assert.Equal(2, (await db.ThumbnailUploads.ToListAsync()).Count(x => x.ConfirmedAt is not null));

        var course = await new CreateCourseHandler(new CourseRepository(db), uploads, new UnitOfWork(db),
            TimeProvider.System, NullLogger<CreateCourseHandler>.Instance).Handle(Course(keys[0]), default);
        Assert.Equal(keys[0], course.ThumbnailKey);
        var wrongOwner = await new ConfirmThumbnailsHandler(uploads, storage, new UnitOfWork(db), TimeProvider.System)
            .Handle(new ConfirmThumbnailsCommand([keys[1]], Guid.NewGuid()), default);
        Assert.Equal("upload_not_found", Assert.Single(wrongOwner.Items).Code);
    }

    [Fact]
    public void Signature_verification_rejects_spoofed_content_types()
    {
        Assert.True(R2ThumbnailObjectStore.HasValidSignature("image/jpeg", [0xff, 0xd8, 0xff]));
        Assert.True(R2ThumbnailObjectStore.HasValidSignature("image/png", [137, 80, 78, 71, 13, 10, 26, 10]));
        Assert.True(R2ThumbnailObjectStore.HasValidSignature("image/webp", "RIFF1234WEBP"u8));
        Assert.False(R2ThumbnailObjectStore.HasValidSignature("image/png", "<script>"u8));
        Assert.False(R2ThumbnailObjectStore.HasValidSignature("image/webp", "RIFF1234NONE"u8));
    }

    private sealed class FakeStorage : IThumbnailObjectStore
    {
        public Dictionary<string, StoredThumbnail> Objects { get; } = [];
        public List<string> DeletedKeys { get; } = [];
        public TimeSpan LastExpiration { get; private set; }
        public Task<string> PresignAsync(string key, string contentType, TimeSpan expiration, CancellationToken ct)
        {
            LastExpiration = expiration;
            return Task.FromResult($"https://r2.example/{key}");
        }
        public Task<StoredThumbnail?> HeadAsync(string key, CancellationToken ct) =>
            Task.FromResult(Objects.TryGetValue(key, out var value) ? value : null);
        public Task<bool> IsConfirmedByAsync(string key, Guid adminId, CancellationToken ct) =>
            Task.FromResult(false);
        public Task DeleteAsync(string key, CancellationToken ct)
        {
            DeletedKeys.Add(key);
            Objects.Remove(key);
            return Task.CompletedTask;
        }
    }
}
