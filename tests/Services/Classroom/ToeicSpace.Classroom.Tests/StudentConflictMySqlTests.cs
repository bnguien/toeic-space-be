using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using ToeicSpace.Classroom.Application.DTOs.Scheduling;
using ToeicSpace.Classroom.Domain.Entities;
using ToeicSpace.Classroom.Domain.Enums;
using ToeicSpace.Classroom.Infrastructure.Persistence;
using ToeicSpace.Classroom.Infrastructure.Services.Scheduling;
using Xunit;

namespace ToeicSpace.Classroom.Tests;

public sealed class StudentConflictMySqlFactAttribute : FactAttribute
{
    public StudentConflictMySqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CLASSROOM_SCHEDULING_TEST_DB")))
            Skip = "Requires a disposable *_test MySQL schema in CLASSROOM_SCHEDULING_TEST_DB.";
    }
}

public sealed class StudentConflictMySqlTests
{
    [StudentConflictMySqlFact]
    public async Task Preview_uses_bounded_mysql_queries_and_never_persists()
    {
        var connection = Environment.GetEnvironmentVariable("CLASSROOM_SCHEDULING_TEST_DB")!;
        if (!new MySqlConnectionStringBuilder(connection).Database.EndsWith("_test", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only a disposable *_test schema is permitted.");
        await using var db = new ClassroomDbContext(new DbContextOptionsBuilder<ClassroomDbContext>()
            .UseMySql(connection, new MySqlServerVersion(new Version(8, 4, 0))).Options);
        await db.Database.MigrateAsync();
        var student = Guid.NewGuid();
        var a = new Class { Code = Guid.NewGuid().ToString("N"), Name = "Preview A", Status = ClassStatus.Active };
        var b = new Class { Code = Guid.NewGuid().ToString("N"), Name = "Preview B", Status = ClassStatus.Active };
        db.Classes.AddRange(a, b);
        db.ClassEnrollments.AddRange(new ClassEnrollment { ClassId = a.Id, StudentId = student },
            new ClassEnrollment { ClassId = b.Id, StudentId = student });
        var start = DateTime.UtcNow.Date.AddDays(2).AddHours(9);
        db.ClassSessions.Add(new ClassSession { ClassId = b.Id, Title = "Existing",
            StartAt = start.AddMinutes(30), EndAt = start.AddHours(1) });
        await db.SaveChangesAsync();

        var proposed = new StudentConflictSession(Guid.NewGuid(), a.Id, start, start.AddHours(1));
        var result = await new StudentConflictDetector(db).PreviewAsync([proposed], null, new(1, 10), default);
        Assert.Equal(1, result.AffectedStudentCount);
        Assert.Equal(1, result.ConflictCount);
        Assert.Equal(1, result.Details!.TotalCount);
        Assert.Single(result.Details.Items);
        Assert.Equal(1, await db.ClassSessions.CountAsync(x => x.ClassId == b.Id));
        Assert.Equal(0, await db.ClassSessions.CountAsync(x => x.ClassId == a.Id));

        db.ClassSessions.Add(new ClassSession { ClassId = a.Id, Title = "New class session",
            StartAt = start, EndAt = start.AddHours(1) });
        await db.SaveChangesAsync();
        var own = await new StudentConflictReadService(db, new StudentConflictDetector(db))
            .GetMineAsync(student, start.AddHours(-1), start.AddHours(2), new(1, 10), default);
        Assert.Equal(1, own.ConflictCount);
        Assert.All(own.Details!.Items, x => Assert.Equal(student, x.StudentId));

        var newcomer = Guid.NewGuid();
        db.Students.Add(new StudentReadModel { StudentId = newcomer, Name = "New student", Email = "new@example.com",
            Role = "User", Status = "Active", IsVerified = true, IsEligible = true });
        db.ClassEnrollments.Add(new ClassEnrollment { ClassId = b.Id, StudentId = newcomer });
        await db.SaveChangesAsync();
        var enrollment = await new EnrollmentConflictPreviewService(db, new StudentConflictDetector(db), TimeProvider.System)
            .PreviewAsync(a.Id, [newcomer], null, default);
        Assert.Equal(1, enrollment.ConflictCount);
        Assert.False(await db.ClassEnrollments.AnyAsync(x => x.ClassId == a.Id && x.StudentId == newcomer));
    }
}
