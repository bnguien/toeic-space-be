using Microsoft.EntityFrameworkCore;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Infrastructure.Persistence;

public sealed class ContentDbContext(DbContextOptions<ContentDbContext> options) : DbContext(options)
{
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<CourseModule> CourseModules => Set<CourseModule>();
    public DbSet<Lesson> Lessons => Set<Lesson>();
    public DbSet<LessonVideo> LessonVideos => Set<LessonVideo>();
    public DbSet<LessonResource> LessonResources => Set<LessonResource>();
    public DbSet<LessonUpload> LessonUploads => Set<LessonUpload>();
    public DbSet<ThumbnailUpload> ThumbnailUploads => Set<ThumbnailUpload>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ContentDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
