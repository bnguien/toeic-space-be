using Microsoft.EntityFrameworkCore;
using ToeicSpace.Classroom.Domain.Entities;

namespace ToeicSpace.Classroom.Infrastructure.Persistence;

public sealed class ClassroomDbContext(DbContextOptions<ClassroomDbContext> options) : DbContext(options)
{
    public DbSet<Class> Classes => Set<Class>();
    public DbSet<ClassInstructor> ClassInstructors => Set<ClassInstructor>();
    public DbSet<TeacherReadModel> Teachers => Set<TeacherReadModel>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ClassroomDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

}
