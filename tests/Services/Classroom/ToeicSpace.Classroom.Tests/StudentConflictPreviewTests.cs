using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Claims;
using ToeicSpace.BuildingBlocks.Security.Jwt;
using ToeicSpace.Classroom.API.Controllers;
using ToeicSpace.Classroom.Application.DTOs.Scheduling;
using ToeicSpace.Classroom.Application.Interfaces.Services;
using ToeicSpace.Classroom.Application.Features.Enrollments.Commands.EnrollStudent;
using ToeicSpace.Classroom.Application.Services;
using ToeicSpace.Classroom.Domain.Entities;
using ToeicSpace.Classroom.Domain.Enums;
using ToeicSpace.Classroom.Domain.Exceptions;
using ToeicSpace.Classroom.Infrastructure.Persistence;
using ToeicSpace.Classroom.Infrastructure.Persistence.Repositories;
using ToeicSpace.Classroom.Infrastructure.Services.Scheduling;
using Xunit;

namespace ToeicSpace.Classroom.Tests;

public sealed class StudentConflictPreviewTests
{
    private static readonly DateTime Start = new(2026, 11, 25, 9, 0, 0, DateTimeKind.Utc);
    private static ClassroomDbContext NewDb() => new(new DbContextOptionsBuilder<ClassroomDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static Class AddClass(ClassroomDbContext db, Guid teacher)
    {
        var cls = new Class { Code = Guid.NewGuid().ToString("N"), Name = "Class", Status = ClassStatus.Active,
            StartDate = new DateOnly(2026, 11, 20), EndDate = new DateOnly(2026, 12, 31) };
        db.Classes.Add(cls);
        db.ClassInstructors.Add(new ClassInstructor { ClassId = cls.Id, TeacherId = teacher });
        db.Teachers.Add(new TeacherReadModel { TeacherId = teacher, Name = "Teacher", Role = "Teacher",
            Status = "Active", IsEligible = true });
        return cls;
    }

    private static void Enroll(ClassroomDbContext db, Class cls, Guid student, bool withdrawn = false)
    {
        db.ClassEnrollments.Add(new ClassEnrollment { ClassId = cls.Id, StudentId = student,
            Status = withdrawn ? EnrollmentStatus.Withdrawn : EnrollmentStatus.Active,
            WithdrawnAt = withdrawn ? Start : null });
    }

    [Fact]
    public async Task Counts_unique_students_and_pairs_without_double_counting_adjacent_cancelled_or_withdrawn()
    {
        await using var db = NewDb();
        var a = AddClass(db, Guid.NewGuid());
        var b = AddClass(db, Guid.NewGuid());
        var c = AddClass(db, Guid.NewGuid());
        var one = Guid.NewGuid();
        var two = Guid.NewGuid();
        var withdrawn = Guid.NewGuid();
        foreach (var cls in new[] { a, b, c }) Enroll(db, cls, one);
        Enroll(db, a, two);
        Enroll(db, b, two);
        Enroll(db, a, withdrawn);
        Enroll(db, b, withdrawn, true);
        var bSession = new ClassSession { ClassId = b.Id, TeacherId = Guid.NewGuid(), Title = "B",
            StartAt = Start.AddMinutes(20), EndAt = Start.AddHours(1) };
        var cSession = new ClassSession { ClassId = c.Id, TeacherId = Guid.NewGuid(), Title = "C",
            StartAt = Start.AddMinutes(40), EndAt = Start.AddHours(1) };
        db.ClassSessions.AddRange(bSession, cSession,
            new ClassSession { ClassId = b.Id, Title = "Adjacent", StartAt = Start.AddHours(1),
                EndAt = Start.AddHours(2) },
            new ClassSession { ClassId = b.Id, Title = "Cancelled", Status = SessionStatus.Cancelled,
                StartAt = Start, EndAt = Start.AddHours(1) });
        await db.SaveChangesAsync();

        var candidate = new StudentConflictSession(Guid.NewGuid(), a.Id, Start, Start.AddHours(1));
        var result = await new StudentConflictDetector(db).PreviewAsync([candidate], null, new(1, 10), default);

        Assert.Equal(2, result.AffectedStudentCount);
        Assert.Equal(3, result.ConflictCount); // student 1: B + C; student 2: B
        Assert.Equal(3, result.Details!.TotalCount);
        Assert.DoesNotContain(result.Details.Items, x => x.StudentId == withdrawn);
        Assert.All(result.Details.Items, x => Assert.True(x.OverlapStart < x.OverlapEnd));
        Assert.Null((await new StudentConflictDetector(db).PreviewAsync([candidate], null, null, default)).Details);
        Assert.Equal(3, (await new StudentConflictDetector(db).PreviewAsync([candidate], null, new(2, 2), default)).Details!.TotalCount);

        bSession.Status = SessionStatus.Cancelled;
        await db.SaveChangesAsync();
        Assert.Equal(1, (await new StudentConflictDetector(db).PreviewAsync([candidate], null, null, default)).ConflictCount);
        var enrollment = await db.ClassEnrollments.SingleAsync(x => x.ClassId == c.Id && x.StudentId == one);
        enrollment.Status = EnrollmentStatus.Withdrawn;
        enrollment.WithdrawnAt = Start;
        await db.SaveChangesAsync();
        Assert.Equal(0, (await new StudentConflictDetector(db).PreviewAsync([candidate], null, null, default)).ConflictCount);
    }

    [Fact]
    public async Task Deduplicates_pairs_when_both_sessions_are_proposed()
    {
        await using var db = NewDb();
        var a = AddClass(db, Guid.NewGuid());
        var b = AddClass(db, Guid.NewGuid());
        var student = Guid.NewGuid();
        Enroll(db, a, student);
        Enroll(db, b, student);
        await db.SaveChangesAsync();
        var first = new StudentConflictSession(Guid.NewGuid(), a.Id, Start, Start.AddHours(1));
        var second = new StudentConflictSession(Guid.NewGuid(), b.Id, Start.AddMinutes(30), Start.AddHours(2));
        var result = await new StudentConflictDetector(db).PreviewAsync([first, second], null, null, default);
        Assert.Equal(1, result.AffectedStudentCount);
        Assert.Equal(1, result.ConflictCount);
    }

    [Fact]
    public async Task Enrollment_preview_warns_without_writing_or_preventing_enrollment()
    {
        await using var db = NewDb();
        var a = AddClass(db, Guid.NewGuid());
        var b = AddClass(db, Guid.NewGuid());
        var student = Guid.NewGuid();
        db.Students.Add(new StudentReadModel { StudentId = student, Name = "Student", Email = "s@example.com",
            Role = "User", Status = "Active", IsVerified = true, IsEligible = true });
        Enroll(db, b, student);
        db.ClassSessions.AddRange(
            new ClassSession { ClassId = a.Id, Title = "Proposed", StartAt = Start, EndAt = Start.AddHours(1) },
            new ClassSession { ClassId = b.Id, Title = "Existing", StartAt = Start.AddMinutes(30), EndAt = Start.AddHours(2) });
        await db.SaveChangesAsync();
        var clock = new FixedClock(new DateTimeOffset(2026, 11, 20, 0, 0, 0, TimeSpan.Zero));

        var preview = await new EnrollmentConflictPreviewService(db, new StudentConflictDetector(db), clock)
            .PreviewAsync(a.Id, [student], null, default);
        Assert.Equal(1, preview.AffectedStudentCount);
        Assert.Equal(1, preview.ConflictCount);
        Assert.False(await db.ClassEnrollments.AnyAsync(x => x.ClassId == a.Id));
        var handler = new EnrollStudentHandler(new ClassRepository(db), new EnrollmentRepository(db),
            new StudentEligibilityService(new StudentReadRepository(db), NullLogger<StudentEligibilityService>.Instance),
            new UnitOfWork(db), clock, NullLogger<EnrollStudentHandler>.Instance);
        var enrolled = await handler.Handle(new EnrollStudentCommand(student)
            { ClassId = a.Id, AdminId = Guid.NewGuid() }, default);
        Assert.Equal(student, enrolled.StudentId);
    }

    [Fact]
    public async Task Session_creation_and_rescheduling_preview_do_not_write_and_student_conflicts_do_not_block()
    {
        await using var db = NewDb();
        var aTeacher = Guid.NewGuid();
        var a = AddClass(db, aTeacher);
        var b = AddClass(db, Guid.NewGuid());
        var student = Guid.NewGuid();
        Enroll(db, a, student);
        Enroll(db, b, student);
        var other = new ClassSession { ClassId = b.Id,
            TeacherId = db.ClassInstructors.Local.Single(x => x.ClassId == b.Id).TeacherId,
            Title = "Other", StartAt = Start.AddMinutes(30), EndAt = Start.AddHours(1) };
        db.ClassSessions.Add(other);
        await db.SaveChangesAsync();
        var clock = new FixedClock(new DateTimeOffset(2026, 11, 20, 0, 0, 0, TimeSpan.Zero));
        var service = new ClassSchedulingService(db, clock, TimeZoneInfo.Utc);
        var input = new SessionInput(a.Id, aTeacher, "New", null, SessionType.Makeup,
            Start, Start.AddHours(1), SessionMode.Online, null, null);
        var preview = await service.CreateSessionAsync(input, aTeacher, false, null, default);
        Assert.Equal(1, preview.ConflictCount);
        Assert.Single(await db.ClassSessions.ToListAsync());
        var created = await service.CreateAsync(input, aTeacher, false, default);
        Assert.NotEqual(Guid.Empty, created.Id);
        var moved = new SessionEdit(aTeacher, "New", null, Start.AddHours(3), Start.AddHours(4),
            SessionMode.Online, null, null, "Change time", created.Version);
        var clear = await service.UpdateSessionAsync(created.Id, moved, aTeacher, false, null, default);
        Assert.Equal(0, clear.ConflictCount);
        Assert.Equal(Start, (await db.ClassSessions.FindAsync(created.Id))!.StartAt);
        var saved = await service.UpdateAsync(created.Id, moved, aTeacher, false, default);
        Assert.Equal(moved.StartAt, saved.StartAt);
        var onlyRoomOrTeacher = moved with { ExpectedVersion = saved.Version, StartAt = saved.StartAt, EndAt = saved.EndAt };
        var noStudentScan = new ClassSchedulingService(db, clock, TimeZoneInfo.Utc,
            studentConflicts: new FailingDetector());
        Assert.Equal(0, (await noStudentScan.UpdateSessionAsync(created.Id, onlyRoomOrTeacher,
            aTeacher, false, null, default)).ConflictCount);
    }

    [Fact]
    public async Task Preview_rejects_teacher_conflict_and_unassigned_teacher()
    {
        await using var db = NewDb();
        var teacher = Guid.NewGuid();
        var cls = AddClass(db, teacher);
        db.ClassSessions.Add(new ClassSession { ClassId = cls.Id, TeacherId = teacher,
            Title = "Existing", StartAt = Start, EndAt = Start.AddHours(1) });
        await db.SaveChangesAsync();
        var service = new ClassSchedulingService(db, new FixedClock(new DateTimeOffset(2026, 11, 20, 0, 0, 0,
            TimeSpan.Zero)), TimeZoneInfo.Utc);
        var input = new SessionInput(cls.Id, teacher, "Conflict", null, SessionType.Regular,
            Start, Start.AddHours(1), SessionMode.Online, null, null);
        Assert.Equal("schedule_conflict", (await Assert.ThrowsAsync<ClassRuleException>(() =>
            service.CreateSessionAsync(input, teacher, false, null, default))).Code);
        Assert.Equal("forbidden_class", (await Assert.ThrowsAsync<ClassRuleException>(() =>
            service.CreateSessionAsync(input with { TeacherId = Guid.NewGuid() }, Guid.NewGuid(), false, null,
                default))).Code);
    }

    [Fact]
    public async Task Recurring_creation_generation_and_update_previews_warn_without_mutating_sessions()
    {
        await using var db = NewDb();
        var teacher = Guid.NewGuid();
        var cls = AddClass(db, teacher);
        var other = AddClass(db, Guid.NewGuid());
        var student = Guid.NewGuid();
        Enroll(db, cls, student);
        Enroll(db, other, student);
        db.ClassSessions.Add(new ClassSession { ClassId = other.Id, TeacherId = db.ClassInstructors.Local
            .Single(x => x.ClassId == other.Id).TeacherId, Title = "Other", StartAt = Start.AddMinutes(30),
            EndAt = Start.AddHours(1).AddMinutes(30) });
        await db.SaveChangesAsync();
        var service = new ClassSchedulingService(db,
            new FixedClock(new DateTimeOffset(2026, 11, 20, 0, 0, 0, TimeSpan.Zero)), TimeZoneInfo.Utc);
        var date = new DateOnly(2026, 11, 25);
        var input = new ScheduleInput(cls.Id, [DayOfWeek.Wednesday], new TimeOnly(9, 0), new TimeOnly(10, 0),
            teacher, SessionMode.Online, TimeZoneInfo.Utc.Id, date, date, null);

        Assert.Equal(1, (await service.CreateScheduleAsync(input, null, default)).ConflictCount);
        Assert.Empty(await db.ClassSchedules.ToListAsync());
        var earlier = await service.CreateScheduleAsync(input with { StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(9, 0) }, Guid.NewGuid(), default);
        Assert.Equal(0, (await service.GenerateSessionsAsync(cls.Id, new(date, date, null), null, default)).ConflictCount);
        Assert.Empty((await service.GenerateAsync(cls.Id, new(date, date, null), Guid.NewGuid(), default)).Affected);

        var later = await service.CreateScheduleAsync(input with { StartTime = new TimeOnly(10, 0),
            EndTime = new TimeOnly(11, 0) }, Guid.NewGuid(), default);
        var generation = await service.GenerateSessionsAsync(cls.Id, new(date, date, null), null, default);
        Assert.Equal(0, generation.ConflictCount);
        Assert.Equal(2, await db.ClassSessions.CountAsync(x => x.ClassId == cls.Id));
        Assert.Empty((await service.GenerateAsync(cls.Id, new(date, date, null), Guid.NewGuid(), default)).Affected);

        var update = await service.UpdateScheduleAsync(earlier.Id, input, earlier.Version, null, default);
        Assert.Equal(1, update.ConflictCount);
        Assert.Equal(new TimeOnly(8, 0), (await db.ClassSchedules.FindAsync(earlier.Id))!.StartTime);
        Assert.Equal(Start.AddHours(-1), (await db.ClassSessions.SingleAsync(x => x.ClassScheduleId == earlier.Id)).StartAt);
        var saved = await service.UpdateScheduleAsync(earlier.Id, input, earlier.Version, Guid.NewGuid(), default);
        Assert.Equal(new TimeOnly(9, 0), saved.Schedule.StartTime);
        Assert.NotEqual(Guid.Empty, later.Id);
    }

    [Fact]
    public async Task Room_conflict_remains_blocking_in_preview_and_save()
    {
        await using var db = NewDb();
        var first = AddClass(db, Guid.NewGuid());
        var secondTeacher = Guid.NewGuid();
        var second = AddClass(db, secondTeacher);
        var center = new Center { Name = "Center", Address = "Address", TimeZoneId = TimeZoneInfo.Utc.Id };
        var room = new Room { CenterId = center.Id, Center = center, Name = "Room", Capacity = 20 };
        db.Centers.Add(center);
        db.Rooms.Add(room);
        db.ClassSessions.Add(new ClassSession { ClassId = first.Id, TeacherId = db.ClassInstructors.Local
            .Single(x => x.ClassId == first.Id).TeacherId, Title = "Other", StartAt = Start,
            EndAt = Start.AddHours(1), Mode = SessionMode.Offline, RoomId = room.Id });
        await db.SaveChangesAsync();
        var service = new ClassSchedulingService(db,
            new FixedClock(new DateTimeOffset(2026, 11, 20, 0, 0, 0, TimeSpan.Zero)), TimeZoneInfo.Utc);
        var input = new SessionInput(second.Id, secondTeacher, "Room conflict", null, SessionType.Regular,
            Start, Start.AddHours(1), SessionMode.Offline, room.Id, null);
        Assert.Equal("schedule_conflict", (await Assert.ThrowsAsync<ClassRuleException>(() =>
            service.CreateSessionAsync(input, Guid.NewGuid(), true, null, default))).Code);
        Assert.Equal("schedule_conflict", (await Assert.ThrowsAsync<ClassRuleException>(() =>
            service.CreateAsync(input, Guid.NewGuid(), true, default))).Code);
    }

    [Fact]
    public async Task Student_explicit_read_is_scoped_and_calendar_read_never_invokes_detector()
    {
        await using var db = NewDb();
        var a = AddClass(db, Guid.NewGuid());
        var b = AddClass(db, Guid.NewGuid());
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        Enroll(db, a, first);
        Enroll(db, b, first);
        Enroll(db, a, second);
        db.ClassSessions.AddRange(
            new ClassSession { ClassId = a.Id, Title = "A", StartAt = Start, EndAt = Start.AddHours(1) },
            new ClassSession { ClassId = b.Id, Title = "B", StartAt = Start.AddMinutes(30), EndAt = Start.AddHours(2) });
        await db.SaveChangesAsync();
        var read = new StudentConflictReadService(db, new StudentConflictDetector(db));
        var mine = await read.GetMineAsync(first, Start.AddHours(-1), Start.AddHours(3), new(1, 10), default);
        Assert.Equal(1, mine.AffectedStudentCount);
        Assert.All(mine.Details!.Items, x => Assert.Equal(first, x.StudentId));
        Assert.Equal(0, (await read.GetMineAsync(second, Start.AddHours(-1), Start.AddHours(3),
            null, default)).ConflictCount);

        var scheduling = new ClassSchedulingService(db, TimeProvider.System, TimeZoneInfo.Utc,
            studentConflicts: new FailingDetector());
        var calendar = await scheduling.ListAsync(first, "User", null, null, null,
            Start.AddHours(-1), Start.AddHours(3), 1, 20, default);
        Assert.Equal(2, calendar.TotalCount);
    }

    [Fact]
    public async Task Teacher_cannot_request_student_conflict_details_or_preview_unrelated_class()
    {
        var teacher = Guid.NewGuid();
        var controller = new ClassSessionsController(null!)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.Role, "Teacher"),
            new Claim(AccessTokenDefaults.SubjectClaim, teacher.ToString())
        ], "Bearer"));
        var input = new SessionInput(Guid.NewGuid(), teacher, "Preview", null, SessionType.Regular,
            Start, Start.AddHours(1), SessionMode.Online, null, null);
        var ex = await Assert.ThrowsAsync<ClassRuleException>(() => controller.PreviewCreate(input, 1));
        Assert.Equal(403, ex.StatusCode);

        await using var db = NewDb();
        var classroom = AddClass(db, Guid.NewGuid());
        await db.SaveChangesAsync();
        var scheduling = new ClassSchedulingService(db, TimeProvider.System, TimeZoneInfo.Utc);
        var denied = await Assert.ThrowsAsync<ClassRuleException>(() => scheduling.CreateSessionAsync(
            input with { ClassId = classroom.Id }, teacher, false, null, default));
        Assert.Equal("forbidden_class", denied.Code);
        Assert.Equal(403, denied.StatusCode);
    }

    private sealed class FailingDetector : IStudentConflictDetector
    {
        public Task<StudentConflictPreview> PreviewAsync(IReadOnlyList<StudentConflictSession> proposed,
            IReadOnlyCollection<Guid>? enrollingStudents, ConflictDetailsRequest? details, CancellationToken ct) =>
            throw new Exception("Calendar must not perform conflict detection.");
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
