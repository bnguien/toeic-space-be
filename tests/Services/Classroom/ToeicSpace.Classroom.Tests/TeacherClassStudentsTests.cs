using Microsoft.EntityFrameworkCore;
using ToeicSpace.Classroom.Application.Features.Enrollments.Queries.GetTeacherClassStudents;
using ToeicSpace.Classroom.Domain.Entities;
using ToeicSpace.Classroom.Domain.Enums;
using ToeicSpace.Classroom.Domain.Exceptions;
using ToeicSpace.Classroom.Infrastructure.Persistence;
using ToeicSpace.Classroom.Infrastructure.Persistence.Repositories;
using Xunit;

namespace ToeicSpace.Classroom.Tests;

public sealed class TeacherClassStudentsTests
{
    [Fact]
    public async Task Assigned_teacher_sees_only_active_students_with_pagination()
    {
        await using var db = NewDb();
        var teacherId = Guid.NewGuid();
        var classroom = new Class { Code = "TEACHER-STUDENTS", Name = "Teacher students" };
        classroom.Instructors.Add(new ClassInstructor { ClassId = classroom.Id, TeacherId = teacherId });
        db.Classes.Add(classroom);

        var students = Enumerable.Range(1, 3).Select(i => new StudentReadModel
        {
            StudentId = Guid.NewGuid(), Name = $"Student {i}", Email = $"student{i}@example.com"
        }).ToArray();
        db.Students.AddRange(students);
        db.ClassEnrollments.AddRange(
            new ClassEnrollment { ClassId = classroom.Id, StudentId = students[0].StudentId, EnrolledAt = DateTime.UtcNow.AddDays(-2) },
            new ClassEnrollment { ClassId = classroom.Id, StudentId = students[1].StudentId, EnrolledAt = DateTime.UtcNow.AddDays(-1) },
            new ClassEnrollment { ClassId = classroom.Id, StudentId = students[2].StudentId,
                Status = EnrollmentStatus.Withdrawn, WithdrawnAt = DateTime.UtcNow, EnrolledAt = DateTime.UtcNow.AddDays(-3) });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var handler = Handler(db);
        var first = await handler.Handle(new GetTeacherClassStudentsQuery(classroom.Id, teacherId, 1, 1), default);
        var second = await handler.Handle(new GetTeacherClassStudentsQuery(classroom.Id, teacherId, 2, 1), default);

        Assert.Equal(2, first.TotalCount);
        Assert.Equal(students[1].StudentId, Assert.Single(first.Items).StudentId);
        Assert.Equal(students[1].Name, first.Items[0].StudentName);
        Assert.Equal(students[0].StudentId, Assert.Single(second.Items).StudentId);
    }

    [Fact]
    public async Task Other_or_unassigned_teacher_cannot_view_students()
    {
        await using var db = NewDb();
        var teacherId = Guid.NewGuid();
        var classroom = new Class { Code = "TEACHER-ACCESS", Name = "Teacher access" };
        classroom.Instructors.Add(new ClassInstructor
        {
            ClassId = classroom.Id, TeacherId = teacherId, UnassignedAt = DateTime.UtcNow
        });
        db.Classes.Add(classroom);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        foreach (var id in new[] { teacherId, Guid.NewGuid() })
        {
            var ex = await Assert.ThrowsAsync<ClassRuleException>(() =>
                Handler(db).Handle(new GetTeacherClassStudentsQuery(classroom.Id, id), default));
            Assert.Equal("forbidden_class", ex.Code);
            Assert.Equal(403, ex.StatusCode);
        }
    }

    [Fact]
    public async Task Missing_or_deleted_class_is_not_visible()
    {
        await using var db = NewDb();
        var teacherId = Guid.NewGuid();
        var classroom = new Class { Code = "TEACHER-DELETED", Name = "Deleted", Status = ClassStatus.Deleted };
        classroom.Instructors.Add(new ClassInstructor { ClassId = classroom.Id, TeacherId = teacherId });
        db.Classes.Add(classroom);
        await db.SaveChangesAsync();

        foreach (var id in new[] { classroom.Id, Guid.NewGuid() })
        {
            var ex = await Assert.ThrowsAsync<ClassRuleException>(() =>
                Handler(db).Handle(new GetTeacherClassStudentsQuery(id, teacherId), default));
            Assert.Equal("class_not_found", ex.Code);
            Assert.Equal(404, ex.StatusCode);
        }
    }

    private static ClassroomDbContext NewDb() => new(
        new DbContextOptionsBuilder<ClassroomDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static GetTeacherClassStudentsHandler Handler(ClassroomDbContext db) =>
        new(new ClassRepository(db), new EnrollmentRepository(db));
}
