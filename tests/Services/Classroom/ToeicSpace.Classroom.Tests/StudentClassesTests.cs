using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ToeicSpace.Classroom.API.Controllers;
using ToeicSpace.Classroom.Application.Features.Classes.Queries.GetMyEnrolledClassById;
using ToeicSpace.Classroom.Application.Features.Classes.Queries.GetMyEnrolledClasses;
using ToeicSpace.Classroom.Domain.Entities;
using ToeicSpace.Classroom.Domain.Enums;
using ToeicSpace.Classroom.Domain.Exceptions;
using ToeicSpace.Classroom.Infrastructure.Persistence;
using ToeicSpace.Classroom.Infrastructure.Persistence.Repositories;
using Xunit;

namespace ToeicSpace.Classroom.Tests;

public sealed class StudentClassesTests
{
    [Fact]
    public async Task Lists_only_current_enrollments_with_search_status_pagination_and_current_teacher()
    {
        await using var db = NewDb();
        var studentId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        db.Teachers.Add(new TeacherReadModel { TeacherId = teacherId, Name = "Teacher A" });
        var archived = AddClass(db, "ARCH", "TOEIC Archived", ClassStatus.Archived, studentId, teacherId);
        var completed = AddClass(db, "DONE", "TOEIC Completed", ClassStatus.Completed, studentId, teacherId);
        AddClass(db, "OTHER", "Another class", ClassStatus.Active, Guid.NewGuid(), teacherId);
        AddClass(db, "DELETED", "TOEIC Deleted", ClassStatus.Deleted, studentId, teacherId);
        var withdrawn = AddClass(db, "LEFT", "TOEIC Withdrawn", ClassStatus.Active, studentId, teacherId, withdrawn: true);
        await db.SaveChangesAsync();

        var handler = new GetMyEnrolledClassesHandler(new ClassRepository(db));
        var all = await handler.Handle(new GetMyEnrolledClassesQuery(studentId, null, null), default);
        Assert.Equal(2, all.TotalCount);
        Assert.Contains(all.Items, x => x.Id == archived && x.TeacherId == teacherId && x.TeacherName == "Teacher A");
        Assert.Contains(all.Items, x => x.Id == completed);
        Assert.DoesNotContain(all.Items, x => x.Id == withdrawn);

        var filtered = await handler.Handle(new GetMyEnrolledClassesQuery(studentId, "  ARCH  ", ClassStatus.Archived, 1, 1), default);
        Assert.Equal(archived, Assert.Single(filtered.Items).Id);
        Assert.Equal(1, filtered.TotalCount);
        Assert.Empty((await handler.Handle(new GetMyEnrolledClassesQuery(studentId, "missing", null), default)).Items);
        var firstPage = await handler.Handle(new GetMyEnrolledClassesQuery(studentId, null, null, 1, 1), default);
        var secondPage = await handler.Handle(new GetMyEnrolledClassesQuery(studentId, null, null, 2, 1), default);
        Assert.Single(firstPage.Items);
        Assert.Equal(2, secondPage.TotalCount);
        Assert.NotEqual(firstPage.Items[0].Id, secondPage.Items[0].Id);
    }

    [Fact]
    public async Task Detail_requires_current_enrollment_and_hides_teacher_assignment_history()
    {
        await using var db = NewDb();
        var studentId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        db.Teachers.Add(new TeacherReadModel { TeacherId = teacherId, Name = "Teacher A" });
        var enrolledId = AddClass(db, "ARCH", "Archived", ClassStatus.Archived, studentId, teacherId);
        var completedId = AddClass(db, "DONE", "Completed", ClassStatus.Completed, studentId, teacherId);
        var withdrawnId = AddClass(db, "LEFT", "Withdrawn", ClassStatus.Active, studentId, teacherId, withdrawn: true);
        var deletedId = AddClass(db, "DELETED", "Deleted", ClassStatus.Deleted, studentId, teacherId);
        var otherId = AddClass(db, "OTHER", "Other", ClassStatus.Active, Guid.NewGuid(), teacherId);
        await db.SaveChangesAsync();

        var handler = new GetMyEnrolledClassByIdHandler(new ClassRepository(db));
        var detail = await handler.Handle(new GetMyEnrolledClassByIdQuery(studentId, enrolledId), default);
        Assert.Equal(ClassStatus.Archived, detail.Class.Status);
        Assert.Equal(teacherId, detail.Class.TeacherId);
        Assert.Equal("Teacher A", detail.Class.TeacherName);
        Assert.Empty(detail.InstructorHistory);
        Assert.Equal(ClassStatus.Completed, (await handler.Handle(
            new GetMyEnrolledClassByIdQuery(studentId, completedId), default)).Class.Status);

        foreach (var id in new[] { withdrawnId, deletedId, otherId, Guid.NewGuid() })
            Assert.Equal("class_not_found", (await Assert.ThrowsAsync<ClassRuleException>(() =>
                handler.Handle(new GetMyEnrolledClassByIdQuery(studentId, id), default))).Code);
    }

    [Fact]
    public void Student_queries_validate_filters_and_routes_require_user_role_without_student_id_parameter()
    {
        var listValidator = new GetMyEnrolledClassesValidator();
        Assert.False(listValidator.Validate(new GetMyEnrolledClassesQuery(Guid.Empty, null, null)).IsValid);
        Assert.False(listValidator.Validate(new GetMyEnrolledClassesQuery(Guid.NewGuid(), null, (ClassStatus)99)).IsValid);
        Assert.False(listValidator.Validate(new GetMyEnrolledClassesQuery(Guid.NewGuid(), null, null, int.MaxValue, 20)).IsValid);
        Assert.False(listValidator.Validate(new GetMyEnrolledClassesQuery(Guid.NewGuid(), null, null, 1, 101)).IsValid);
        Assert.False(new GetMyEnrolledClassByIdValidator().Validate(new GetMyEnrolledClassByIdQuery(Guid.NewGuid(), Guid.Empty)).IsValid);

        var controller = typeof(StudentClassesController);
        Assert.Contains(controller.GetCustomAttributes(typeof(AuthorizeAttribute), false)
            .Cast<AuthorizeAttribute>(), x => x.Roles == "User");
        Assert.All(new[] { "List", "Get" }, name =>
        {
            var action = controller.GetMethod(name)!;
            Assert.DoesNotContain(action.GetParameters(), p => p.Name == "studentId");
            Assert.NotNull(action.GetCustomAttributes(typeof(HttpGetAttribute), false).SingleOrDefault());
        });
    }

    private static ClassroomDbContext NewDb() => new(new DbContextOptionsBuilder<ClassroomDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static Guid AddClass(ClassroomDbContext db, string code, string name, ClassStatus status,
        Guid studentId, Guid teacherId, bool withdrawn = false)
    {
        var classroom = new Class { Code = code, Name = name, Status = status, UpdatedAt = DateTime.UtcNow };
        db.Classes.Add(classroom);
        db.ClassInstructors.Add(new ClassInstructor
        {
            ClassId = classroom.Id, TeacherId = teacherId, AssignedAt = DateTime.UtcNow
        });
        db.ClassEnrollments.Add(new ClassEnrollment
        {
            ClassId = classroom.Id, StudentId = studentId,
            Status = withdrawn ? EnrollmentStatus.Withdrawn : EnrollmentStatus.Active,
            WithdrawnAt = withdrawn ? DateTime.UtcNow : null,
            EnrolledAt = DateTime.UtcNow
        });
        return classroom.Id;
    }
}
