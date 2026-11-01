using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using ToeicSpace.Classroom.Application;
using ToeicSpace.Classroom.Application.Interfaces;
using ToeicSpace.Classroom.Application.Interfaces.Repositories;
using ToeicSpace.Classroom.Domain.Entities;
using ToeicSpace.Classroom.Domain.Enums;
using ToeicSpace.Classroom.Infrastructure.Persistence;
using ToeicSpace.Classroom.Infrastructure.Persistence.Repositories;
using ToeicSpace.Classroom.Infrastructure.Services.Classes;
using Xunit;

namespace ToeicSpace.Classroom.Tests;

public sealed class ClassAutoStartTests
{
    private static readonly InMemoryDatabaseRoot Root = new();
    private static readonly TimeZoneInfo Vietnam = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    private static ServiceProvider Services(string database, TimeProvider clock)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ClassroomDbContext>(options => options.UseInMemoryDatabase(database, Root));
        services.AddScoped<IClassRepository, ClassRepository>();
        services.AddScoped<ITeacherReadRepository, TeacherReadRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton(clock);
        services.AddSingleton(Vietnam);
        services.AddApplicationServices();
        services.AddSingleton<ClassAutoStartJob>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Starts_eligible_draft_on_local_start_date_only_and_does_not_repeat()
    {
        var clock = new AdjustableClock(new DateTimeOffset(2026, 10, 30, 16, 59, 0, TimeSpan.Zero));
        await using var provider = Services(Guid.NewGuid().ToString(), clock);
        Guid readyId, notReadyId, archivedId, expiredId;
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ClassroomDbContext>();
            var teacherId = Guid.NewGuid();
            db.Teachers.Add(new TeacherReadModel { TeacherId = teacherId, Name = "Teacher", Email = "teacher@example.com",
                Role = "Teacher", Status = "Active", IsEligible = true, Version = 1 });
            readyId = AddClass(db, "READY", teacherId, new DateOnly(2026, 10, 31), new DateOnly(2026, 12, 31));
            notReadyId = AddClass(db, "FUTURE", teacherId, new DateOnly(2026, 11, 1), new DateOnly(2026, 12, 31));
            archivedId = AddClass(db, "ARCHIVED", teacherId, new DateOnly(2026, 10, 31), new DateOnly(2026, 12, 31), ClassStatus.Archived);
            expiredId = AddClass(db, "EXPIRED", teacherId, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 29));
            await db.SaveChangesAsync();
        }

        var job = provider.GetRequiredService<ClassAutoStartJob>();
        await job.RunOnceAsync();
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ClassroomDbContext>();
            Assert.Equal(ClassStatus.Draft, (await db.Classes.SingleAsync(x => x.Id == readyId)).Status);
        }

        clock.Set(new DateTimeOffset(2026, 10, 30, 17, 0, 0, TimeSpan.Zero)); // Midnight October 31 in Vietnam.
        await job.RunOnceAsync();
        await job.RunOnceAsync();
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ClassroomDbContext>();
            var ready = await db.Classes.SingleAsync(x => x.Id == readyId);
            Assert.Equal(ClassStatus.Active, ready.Status);
            Assert.Equal(Guid.Empty, ready.UpdatedBy); // System actor.
            Assert.Equal(1, ready.Version); // No duplicate activation on subsequent checks.
            Assert.Equal(ClassStatus.Draft, (await db.Classes.SingleAsync(x => x.Id == notReadyId)).Status);
            Assert.Equal(ClassStatus.Archived, (await db.Classes.SingleAsync(x => x.Id == archivedId)).Status);
            Assert.Equal(ClassStatus.Draft, (await db.Classes.SingleAsync(x => x.Id == expiredId)).Status);
        }
    }

    [Fact]
    public async Task Missing_or_ineligible_teacher_keeps_class_draft_until_prerequisites_are_met()
    {
        var clock = new AdjustableClock(new DateTimeOffset(2026, 10, 30, 17, 0, 0, TimeSpan.Zero));
        await using var provider = Services(Guid.NewGuid().ToString(), clock);
        Guid classId, readyId;
        var teacherId = Guid.NewGuid();
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ClassroomDbContext>();
            classId = AddClass(db, "WAITING", teacherId, new DateOnly(2026, 10, 31), new DateOnly(2026, 12, 31));
            var readyTeacherId = Guid.NewGuid();
            db.Teachers.Add(new TeacherReadModel { TeacherId = readyTeacherId, Name = "Ready Teacher",
                Email = "ready@example.com", Role = "Teacher", Status = "Active", IsEligible = true, Version = 1 });
            readyId = AddClass(db, "READY", readyTeacherId, new DateOnly(2026, 10, 31), new DateOnly(2026, 12, 31));
            await db.SaveChangesAsync();
        }

        var job = provider.GetRequiredService<ClassAutoStartJob>();
        await job.RunOnceAsync();
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ClassroomDbContext>();
            Assert.Equal(ClassStatus.Draft, (await db.Classes.SingleAsync(x => x.Id == classId)).Status);
            Assert.Equal(ClassStatus.Active, (await db.Classes.SingleAsync(x => x.Id == readyId)).Status);
            db.Teachers.Add(new TeacherReadModel { TeacherId = teacherId, Name = "Teacher", Email = "teacher@example.com",
                Role = "Teacher", Status = "Inactive", IsEligible = false, Version = 1 });
            await db.SaveChangesAsync();
        }
        await job.RunOnceAsync();
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ClassroomDbContext>();
            var teacher = await db.Teachers.SingleAsync(x => x.TeacherId == teacherId);
            teacher.Status = "Active";
            teacher.IsEligible = true;
            teacher.Version++;
            await db.SaveChangesAsync();
        }
        await job.RunOnceAsync();
        await using (var scope = provider.CreateAsyncScope())
        {
            Assert.Equal(ClassStatus.Active, (await scope.ServiceProvider.GetRequiredService<ClassroomDbContext>()
                .Classes.SingleAsync(x => x.Id == classId)).Status);
        }
    }

    private static Guid AddClass(ClassroomDbContext db, string code, Guid teacherId, DateOnly start, DateOnly end,
        ClassStatus status = ClassStatus.Draft)
    {
        var entity = new Class { Code = code, Name = code, StartDate = start, EndDate = end, Status = status };
        db.Classes.Add(entity);
        db.ClassInstructors.Add(new ClassInstructor { ClassId = entity.Id, TeacherId = teacherId });
        return entity.Id;
    }

    private sealed class AdjustableClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Set(DateTimeOffset value) => _now = value;
    }
}
