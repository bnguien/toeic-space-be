using Microsoft.EntityFrameworkCore;
using ToeicSpace.Classroom.Domain.Entities;

namespace ToeicSpace.Classroom.Infrastructure.Persistence;

public sealed class ClassroomDbContext(DbContextOptions<ClassroomDbContext> options) : DbContext(options)
{
    public DbSet<Class> Classes => Set<Class>();
    public DbSet<ClassInstructor> ClassInstructors => Set<ClassInstructor>();
    public DbSet<TeacherReadModel> Teachers => Set<TeacherReadModel>();
    public DbSet<StudentReadModel> Students => Set<StudentReadModel>();
    public DbSet<ClassEnrollment> ClassEnrollments => Set<ClassEnrollment>();
    public DbSet<Center> Centers => Set<Center>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<ClassRoomChange> ClassRoomChanges => Set<ClassRoomChange>();
    public DbSet<ClassSchedule> ClassSchedules => Set<ClassSchedule>();
    public DbSet<ClassScheduleAudit> ClassScheduleAudits => Set<ClassScheduleAudit>();
    public DbSet<ClassSession> ClassSessions => Set<ClassSession>();
    public DbSet<ClassSessionAudit> ClassSessionAudits => Set<ClassSessionAudit>();
    public DbSet<SchedulingResource> SchedulingResources => Set<SchedulingResource>();
    public DbSet<TeacherGoogleConnection> TeacherGoogleConnections => Set<TeacherGoogleConnection>();
    public DbSet<GoogleOAuthAttempt> GoogleOAuthAttempts => Set<GoogleOAuthAttempt>();
    public DbSet<SessionMeetProvisioning> SessionMeetProvisionings => Set<SessionMeetProvisioning>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ClassroomDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

}
