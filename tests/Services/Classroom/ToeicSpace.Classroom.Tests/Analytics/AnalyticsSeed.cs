using Microsoft.EntityFrameworkCore;
using ToeicSpace.Classroom.Application.Analytics.Queries.QueryAnalytics;
using ToeicSpace.Classroom.Application.DTOs.Analytics;
using ToeicSpace.Classroom.Domain.Entities;
using ToeicSpace.Classroom.Domain.Enums;
using ToeicSpace.Classroom.Infrastructure.Persistence;
using ToeicSpace.Classroom.Infrastructure.Persistence.Repositories;

namespace ToeicSpace.Classroom.Tests.Analytics;

/// <summary>A clock fixed at 2026-09-14 08:00 UTC, so the default range is 2026-06-17 .. 2026-09-14.</summary>
internal sealed class AnalyticsClock : TimeProvider
{
    public static readonly DateTimeOffset Now = new(2026, 9, 14, 8, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => Now;
}

/// <summary>An isolated in-memory Classroom database with helpers to seed classes, enrollments and teachers.</summary>
internal sealed class AnalyticsDatabase : IDisposable
{
    public static readonly DateTime Recent = new(2026, 9, 10, 9, 0, 0, DateTimeKind.Utc);

    public AnalyticsDatabase()
    {
        Context = new ClassroomDbContext(new DbContextOptionsBuilder<ClassroomDbContext>()
            .UseInMemoryDatabase($"classroom-analytics-{Guid.NewGuid():N}")
            .Options);
    }

    public ClassroomDbContext Context { get; }

    public static List<Guid> Learners(int count) => Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToList();

    public Class AddClass(
        string code,
        ClassStatus status = ClassStatus.Active,
        DateTime? createdAt = null,
        DateOnly? startDate = null,
        Guid? teacherId = null)
    {
        var entity = new Class
        {
            Code = code,
            Name = $"Class {code}",
            Status = status,
            CreatedAt = createdAt ?? Recent,
            UpdatedAt = createdAt ?? Recent,
            StartDate = startDate,
            EndDate = startDate?.AddDays(60)
        };

        if (teacherId is { } teacher)
        {
            entity.Instructors.Add(new ClassInstructor { TeacherId = teacher, AssignedAt = entity.CreatedAt });
        }

        Context.Classes.Add(entity);
        Context.SaveChanges();
        return entity;
    }

    public void Enroll(Guid classId, IEnumerable<Guid> students, DateTime? enrolledAt = null, bool withdrawn = false)
    {
        foreach (var student in students)
        {
            var at = enrolledAt ?? Recent;
            Context.ClassEnrollments.Add(new ClassEnrollment
            {
                ClassId = classId,
                StudentId = student,
                EnrolledAt = at,
                Status = withdrawn ? EnrollmentStatus.Withdrawn : EnrollmentStatus.Active,
                WithdrawnAt = withdrawn ? at.AddDays(1) : null
            });
        }

        Context.SaveChanges();
    }

    public void Unassign(Guid classId)
    {
        foreach (var instructor in Context.ClassInstructors.Where(instructor => instructor.ClassId == classId))
        {
            instructor.UnassignedAt = Recent;
        }

        Context.SaveChanges();
    }

    public Task<AnalyticsResultDto> QueryAsync(QueryAnalyticsQuery query) =>
        new QueryAnalyticsHandler(new ClassroomAnalyticsReader(Context), new AnalyticsClock())
            .Handle(query, CancellationToken.None);

    public void Dispose() => Context.Dispose();
}

internal static class AnalyticsResultExtensions
{
    public static object? Cell(this AnalyticsResultDto result, int row, string column) => result.Rows[row][column];

    public static IReadOnlyDictionary<string, object?> RowWhere(this AnalyticsResultDto result, string column, string value) =>
        result.Rows.Single(row => (string?)row[column] == value);
}
