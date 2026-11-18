using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using ToeicSpace.Classroom.Application.DTOs.Scheduling;
using ToeicSpace.Classroom.Application.Features.Centers.Commands.CreateCenter;
using ToeicSpace.Classroom.Application.Features.Classes.Commands.AssignTeacher;
using ToeicSpace.Classroom.Application.Features.Classes.Commands.ChangeClassStatus;
using ToeicSpace.Classroom.Application.Features.Classes.Commands.CreateClass;
using ToeicSpace.Classroom.Application.Features.Classes.Commands.SetDefaultRoom;
using ToeicSpace.Classroom.Application.Features.Enrollments.Commands.EnrollStudent;
using ToeicSpace.Classroom.Application.Features.Rooms.Commands.CreateRoom;
using ToeicSpace.Classroom.Application.Features.Schedules.Commands.CreateSchedule;
using ToeicSpace.Classroom.Application.Features.Schedules.Commands.GenerateSessions;
using ToeicSpace.Classroom.Application.Features.Schedules.Queries.ListSchedules;
using ToeicSpace.Classroom.Application.Features.Sessions.Commands.CancelSession;
using ToeicSpace.Classroom.Application.Features.Sessions.Commands.UpdateSession;
using ToeicSpace.Classroom.Application.Features.Classes.Queries.GetClassById;
using ToeicSpace.Classroom.Application.Features.Sessions.Queries.ListSessions;
using ToeicSpace.Classroom.Domain.Entities;
using ToeicSpace.Classroom.Domain.Enums;
using ToeicSpace.Classroom.Infrastructure.Persistence;

namespace ToeicSpace.Classroom.Infrastructure.Persistence.Seeding;

public sealed record ClassroomDemoSeedReport(int Centers, int Rooms, int Classes, int Enrollments,
    int Schedules, int Sessions, int CreatedPrimaryRecords, int ReusedRecords, int SkippedOccurrences);

/// <summary>Creates the stable TOEIC demo dataset through the existing application commands.</summary>
public sealed class ClassroomDemoSeeder(ClassroomDbContext db, ISender sender, IHostEnvironment environment,
    TimeProvider clock, TimeZoneInfo classZone)
{
    private const string Marker = "[TOEICSPACE-DEMO]";
    private static readonly Guid SeedActor = Guid.Parse("00000000-0000-0000-0000-00000000D3A0");
    private static readonly string[] ClassCodes = ["DEMO-TOEIC-800-MW", "DEMO-TOEIC-750-MW", "DEMO-TOEIC-SPEAKING", "DEMO-TOEIC-INTENSIVE"];
    private int _inserted;
    private int _reused;
    private int _skipped;

    public async Task<ClassroomDemoSeedReport> SeedAsync(CancellationToken ct = default)
    {
        _inserted = 0;
        _reused = 0;
        _skipped = 0;
        if (!environment.IsDevelopment() && !environment.IsEnvironment("Testing"))
            throw new InvalidOperationException("Classroom demo seeding is available only in Development or Testing.");

        // Refuse to leave a half-created local dataset when its service-owned references are absent.
        var teachers = await db.Teachers.AsNoTracking().Where(x => x.IsEligible && x.Role == "Teacher" && x.Status == "Active")
            .OrderBy(x => x.TeacherId).Take(3).ToListAsync(ct);
        var students = await db.Students.AsNoTracking().Where(x => x.IsEligible && x.IsVerified && x.Role == "User" && x.Status == "Active")
            .OrderBy(x => x.StudentId).Take(12).ToListAsync(ct);
        if (teachers.Count < 3)
            throw new InvalidOperationException("Need 3 active, eligible TeacherReadModels. Start Identity and Classroom with RabbitMQ enabled, wait for teacher synchronization, then rerun the seeder.");
        if (students.Count < 10)
            throw new InvalidOperationException("Need at least 10 active, verified, eligible StudentReadModels. Create/verify students in Identity, wait for Classroom synchronization, then rerun the seeder.");

        var now = clock.GetUtcNow().UtcDateTime;
        var existingAnchorClass = await db.Classes.AsNoTracking().SingleOrDefaultAsync(x => x.Code == ClassCodes[0], ct);
        var existingAnchorSchedule = existingAnchorClass is null ? null : await db.ClassSchedules.AsNoTracking()
            .Where(x => x.ClassId == existingAnchorClass.Id).OrderBy(x => x.EffectiveFrom).FirstOrDefaultAsync(ct);
        var anchor = existingAnchorSchedule?.EffectiveFrom ?? existingAnchorClass?.StartDate?.AddDays(7) ??
            NextMonday(DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(now, classZone)));
        var endDate = anchor.AddDays(41);
        if (existingAnchorClass is not null &&
            (existingAnchorClass.StartDate != anchor.AddDays(-7) || existingAnchorClass.EndDate != endDate))
            throw new InvalidOperationException("The existing demo class date range was changed; restore its seeded date range before rerunning.");
        var centers = await EnsureCentersAsync(ct);
        var rooms = await EnsureRoomsAsync(centers, ct);
        var classRooms = new[] { rooms[0], rooms[1], rooms[2], rooms[0] };
        var classTeachers = new[] { teachers[0].TeacherId, teachers[1].TeacherId, teachers[2].TeacherId, teachers[0].TeacherId };
        var names = new[]
        {
            ("TOEIC MASTER 800+ | Buổi tối", "Khóa luyện đề mục tiêu 800+, học tối thứ Hai và thứ Tư."),
            ("TOEIC BOOST 750+ | Buổi tối", "Củng cố Listening và Reading; học song song với lớp 800+."),
            ("TOEIC SPEAKING & LISTENING", "Luyện phản xạ nghe nói trực tuyến, kèm buổi thực hành tại trung tâm."),
            ("TOEIC INTENSIVE 600+", "Lớp tăng cường, dùng chung phòng với lớp 800+ vào ngày khác.")
        };
        var classes = new Class[4];
        for (var i = 0; i < classes.Length; i++)
        {
            var cls = await db.Classes.SingleOrDefaultAsync(x => x.Code == ClassCodes[i], ct);
            if (cls is null)
            {
                var created = await sender.Send(new CreateClassCommand(ClassCodes[i], names[i].Item1,
                    $"{Marker} {names[i].Item2}", anchor.AddDays(-7), endDate) { AdminId = SeedActor }, ct);
                cls = await db.Classes.SingleAsync(x => x.Id == created.Class.Id, ct);
                _inserted++;
            }
            else _reused++;
            if (cls.StartDate != anchor.AddDays(-7) || cls.EndDate != endDate)
                throw new InvalidOperationException($"The existing demo class {cls.Code} date range was changed; restore its seeded date range before rerunning.");
            if (cls.Status is ClassStatus.Deleted or ClassStatus.Archived or ClassStatus.Completed)
                throw new InvalidOperationException($"Seed class {cls.Code} is {cls.Status}; restore it manually before rerunning.");
            var currentTeacher = await db.ClassInstructors.Where(x => x.ClassId == cls.Id && x.UnassignedAt == null)
                .Select(x => (Guid?)x.TeacherId).SingleOrDefaultAsync(ct);
            if (currentTeacher != classTeachers[i])
                await sender.Send(new AssignTeacherCommand(classTeachers[i]) { ClassId = cls.Id, AdminId = SeedActor }, ct);
            var roomId = classRooms[i];
            if (cls.DefaultRoomId != roomId)
                await sender.Send(new SetDefaultRoomCommand(roomId, "Demo dataset setup") { ClassId = cls.Id, AdminId = SeedActor }, ct);
            if (cls.Status == ClassStatus.Draft)
                await sender.Send(new ChangeClassStatusCommand(ClassStatus.Active) { ClassId = cls.Id, AdminId = SeedActor }, ct);
            classes[i] = await db.Classes.SingleAsync(x => x.Id == cls.Id, ct);
        }

        var enrollmentTarget = Math.Min(12, students.Count);
        var enrolled = 0;
        for (var i = 0; i < enrollmentTarget; i++)
        {
            var cls = classes[i % classes.Length];
            var studentId = students[i].StudentId;
            if (await db.ClassEnrollments.AnyAsync(x => x.ClassId == cls.Id && x.StudentId == studentId &&
                    x.Status == EnrollmentStatus.Active && x.WithdrawnAt == null, ct))
            {
                _reused++;
                continue;
            }
            await sender.Send(new EnrollStudentCommand(studentId) { ClassId = cls.Id, AdminId = SeedActor }, ct);
            _inserted++;
            enrolled++;
        }
        // If projection changes removed previously selected users, do not overfill a class or silently accept invalid rooms.
        foreach (var cls in classes)
        {
            var count = await db.ClassEnrollments.CountAsync(x => x.ClassId == cls.Id && x.Status == EnrollmentStatus.Active && x.WithdrawnAt == null, ct);
            var capacity = await db.Rooms.Where(x => x.Id == cls.DefaultRoomId).Select(x => x.Capacity).SingleAsync(ct);
            if (count > capacity) throw new InvalidOperationException($"Class {cls.Code} has {count} active enrollments but its default room holds {capacity}.");
        }

        var specs = ScheduleSpecs(classes, classTeachers, rooms, anchor, endDate);
        var scheduleCount = 0;
        foreach (var spec in specs)
        {
            var classSchedules = await db.ClassSchedules.AsNoTracking().Where(x => x.ClassId == spec.Input.ClassId).ToListAsync(ct);
            var existing = classSchedules.Any(x => x.TeacherId == spec.Input.TeacherId && x.Mode == spec.Input.Mode &&
                x.DaysOfWeek.SequenceEqual(spec.Input.DaysOfWeek) && x.StartTime == spec.Input.StartTime &&
                x.EndTime == spec.Input.EndTime && x.EffectiveFrom == spec.Input.EffectiveFrom && x.EffectiveTo == spec.Input.EffectiveTo);
            if (existing) { _reused++; continue; }
            await sender.Send(new CreateScheduleCommand(spec.Input, SeedActor), ct);
            _inserted++;
        }
        scheduleCount = await db.ClassSchedules.CountAsync(x => classes.Select(c => c.Id).Contains(x.ClassId), ct);

        // Generate through the scheduling service: it enforces UTC conversion, date ranges, uniqueness and conflicts.
        foreach (var cls in classes)
        {
            var generated = await sender.Send(new GenerateSessionsCommand(cls.Id,
                new GenerateSessionsInput(anchor, endDate, null), SeedActor), ct);
            _inserted += generated.Affected.Count;
            _skipped += generated.Skipped.Count;
        }

        // Use the regular edit/cancel commands to demonstrate audited one-off overrides and cancellation.
        await ApplySessionScenariosAsync(classes, rooms[4], ct);
        var report = await VerifyAsync(classes, rooms, centers, scheduleCount, ct);
        return report with { ReusedRecords = _reused, CreatedPrimaryRecords = _inserted, SkippedOccurrences = _skipped };
    }

    private async Task<Center[]> EnsureCentersAsync(CancellationToken ct)
    {
        var specs = new[]
        {
            ($"{Marker} TOEIC MASTER Nguyễn Trãi", "147 Nguyễn Trãi, Quận 1, TP. Hồ Chí Minh"),
            ($"{Marker} TOEIC MASTER Thảo Điền", "12 Quốc Hương, TP. Thủ Đức, TP. Hồ Chí Minh")
        };
        var result = new Center[2];
        for (var i = 0; i < specs.Length; i++)
        {
            var center = await db.Centers.SingleOrDefaultAsync(x => x.Name == specs[i].Item1, ct);
            if (center is null)
            {
                var created = await sender.Send(new CreateCenterCommand(specs[i].Item1, specs[i].Item2, "Asia/Ho_Chi_Minh"), ct);
                center = await db.Centers.SingleAsync(x => x.Id == created.Id, ct);
                _inserted++;
            }
            else _reused++;
            result[i] = center;
        }
        return result;
    }

    private async Task<Guid[]> EnsureRoomsAsync(Center[] centers, CancellationToken ct)
    {
        var specs = new[] { (0, "DEMO-R101 · Nguyễn Trãi", 24), (0, "DEMO-R102 · Nguyễn Trãi", 20),
            (0, "DEMO-R103 · Nguyễn Trãi", 18), (1, "DEMO-R201 · Thảo Điền", 24), (1, "DEMO-R202 · Thảo Điền", 20) };
        var result = new Guid[specs.Length];
        for (var i = 0; i < specs.Length; i++)
        {
            var (centerIndex, name, capacity) = specs[i];
            var room = await db.Rooms.SingleOrDefaultAsync(x => x.CenterId == centers[centerIndex].Id && x.Name == name, ct);
            if (room is null)
            {
                var created = await sender.Send(new CreateRoomCommand(centers[centerIndex].Id, name, capacity), ct);
                result[i] = created.Id;
                _inserted++;
            }
            else { result[i] = room.Id; _reused++; }
        }
        return result;
    }

    private static (int ClassIndex, ScheduleInput Input)[] ScheduleSpecs(Class[] classes, Guid[] teachers,
        Guid[] rooms, DateOnly from, DateOnly to)
    {
        _ = rooms;
        var specs = new (int, IReadOnlyList<DayOfWeek>, TimeOnly, SessionMode)[]
        {
            (0, [DayOfWeek.Monday], new TimeOnly(18, 0), SessionMode.Offline),
            (0, [DayOfWeek.Wednesday], new TimeOnly(18, 0), SessionMode.Offline),
            (1, [DayOfWeek.Monday], new TimeOnly(18, 0), SessionMode.Offline),
            (1, [DayOfWeek.Wednesday], new TimeOnly(18, 0), SessionMode.Offline),
            (2, [DayOfWeek.Tuesday], new TimeOnly(19, 0), SessionMode.Online),
            (3, [DayOfWeek.Thursday], new TimeOnly(18, 30), SessionMode.Offline)
        };
        return specs.Select(s => (s.Item1, new ScheduleInput(classes[s.Item1].Id, s.Item2, s.Item3,
            s.Item3.AddMinutes(90), teachers[s.Item1], s.Item4, "Asia/Ho_Chi_Minh", from, to, null))).ToArray();
    }

    private async Task ApplySessionScenariosAsync(Class[] classes, Guid overrideRoomId, CancellationToken ct)
    {
        var sessions = await db.ClassSessions.Where(x => classes.Select(c => c.Id).Contains(x.ClassId) &&
                x.ClassScheduleId != null).OrderBy(x => x.StartAt).ToListAsync(ct);
        var overrideTarget = sessions.FirstOrDefault(x => x.ClassId == classes[3].Id &&
            x.OriginalLocalDate == classes[3].StartDate!.Value.AddDays(10));
        if (overrideTarget is { Status: SessionStatus.Scheduled, ModeOverridden: false })
        {
            await sender.Send(new UpdateSessionCommand(overrideTarget.Id,
                new SessionEdit(overrideTarget.TeacherId, overrideTarget.Title, overrideTarget.Description, overrideTarget.StartAt,
                    overrideTarget.EndAt, SessionMode.Online, null, "Demo session online override", "Demonstrate an online session override", overrideTarget.Version),
                SeedActor, true), ct);
        }
        var roomTarget = sessions.FirstOrDefault(x => x.ClassId == classes[3].Id &&
            x.OriginalLocalDate == classes[3].StartDate!.Value.AddDays(17));
        if (roomTarget is { Status: SessionStatus.Scheduled, RoomOverridden: false })
        {
            await sender.Send(new UpdateSessionCommand(roomTarget.Id,
                new SessionEdit(roomTarget.TeacherId, roomTarget.Title, roomTarget.Description, roomTarget.StartAt,
                    roomTarget.EndAt, SessionMode.Offline, overrideRoomId, "Demo room override", "Demonstrate a session room override", roomTarget.Version),
                SeedActor, true), ct);
        }
        var cancelTarget = sessions.FirstOrDefault(x => x.ClassId == classes[3].Id &&
            x.OriginalLocalDate == classes[3].StartDate!.Value.AddDays(24));
        if (cancelTarget is { Status: SessionStatus.Scheduled })
        {
            await sender.Send(new CancelSessionCommand(cancelTarget.Id, "Demo scenario: instructor unavailable", SeedActor, true), ct);
        }
    }

    private async Task<ClassroomDemoSeedReport> VerifyAsync(Class[] classes, Guid[] rooms, Center[] centers,
        int expectedSchedules, CancellationToken ct)
    {
        var classIds = classes.Select(x => x.Id).ToArray();
        var sessions = await db.ClassSessions.AsNoTracking().Where(x => classIds.Contains(x.ClassId)).ToListAsync(ct);
        var schedules = await db.ClassSchedules.AsNoTracking().Where(x => classIds.Contains(x.ClassId)).ToListAsync(ct);
        var enrollments = await db.ClassEnrollments.AsNoTracking().Where(x => classIds.Contains(x.ClassId) &&
            x.Status == EnrollmentStatus.Active && x.WithdrawnAt == null).ToListAsync(ct);
        if (classes.Length != 4 || centers.Length != 2 || rooms.Length != 5 || schedules.Count != expectedSchedules || schedules.Count < 4 || schedules.Count > 6)
            throw new InvalidOperationException($"Seed counts invalid: centers={centers.Length}, rooms={rooms.Length}, classes={classes.Length}, schedules={schedules.Count}.");
        // Six weekly patterns create about 36 occurrences.
        if (sessions.Count is < 30 or > 50 || enrollments.Count is < 10 or > 15)
            throw new InvalidOperationException($"Seed counts invalid: enrollments={enrollments.Count}, sessions={sessions.Count}. Ensure the start date has enough future lessons.");
        var assignments = await db.ClassInstructors.AsNoTracking().Where(x => classIds.Contains(x.ClassId) && x.UnassignedAt == null).ToListAsync(ct);
        if (assignments.Count != 4)
            throw new InvalidOperationException("Not every demo class has exactly one current teacher assignment.");
        var teacherIds = assignments.Select(x => x.TeacherId).Distinct().ToArray();
        var synchronizedTeachers = await db.Teachers.AsNoTracking().Where(x => teacherIds.Contains(x.TeacherId) && x.IsEligible &&
            x.Role == "Teacher" && x.Status == "Active").CountAsync(ct);
        if (teacherIds.Length != 3 || synchronizedTeachers != 3)
            throw new InvalidOperationException("Demo class assignments must reference the 3 eligible synchronized Identity teachers.");
        var studentIds = enrollments.Select(x => x.StudentId).Distinct().ToArray();
        var synchronizedStudents = await db.Students.AsNoTracking().Where(x => studentIds.Contains(x.StudentId) && x.IsEligible &&
            x.IsVerified && x.Role == "User" && x.Status == "Active").CountAsync(ct);
        if (synchronizedStudents != studentIds.Length)
            throw new InvalidOperationException("An enrollment references a student missing or ineligible in the local Identity projection.");
        if (sessions.GroupBy(x => (x.ClassScheduleId, x.OriginalLocalDate)).Any(g => g.Key.ClassScheduleId is not null && g.Count() > 1))
            throw new InvalidOperationException("Duplicate schedule occurrence keys were found.");
        var schedulesById = schedules.ToDictionary(x => x.Id);
        var classById = classes.ToDictionary(x => x.Id);
        var roomsById = await db.Rooms.AsNoTracking().Where(x => rooms.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var centerIds = centers.Select(x => x.Id).ToHashSet();
        if (roomsById.Count != 5 || roomsById.Values.Any(x => !centerIds.Contains(x.CenterId)))
            throw new InvalidOperationException("A demo room references a missing or unrelated center.");
        foreach (var session in sessions)
        {
            if (!classById.TryGetValue(session.ClassId, out var cls))
                throw new InvalidOperationException($"Session {session.Id} references a missing class.");
            // MySQL DATETIME stores the UTC clock value without DateTime.Kind metadata.
            var utcStart = DateTime.SpecifyKind(session.StartAt, DateTimeKind.Utc);
            var utcEnd = DateTime.SpecifyKind(session.EndAt, DateTimeKind.Utc);
            var localStart = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utcStart, classZone));
            var localEnd = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utcEnd.AddTicks(-1), classZone));
            if (localStart < cls.StartDate || localEnd > cls.EndDate)
                throw new InvalidOperationException($"Session {session.Id} falls outside class date range.");
            if (session.ClassScheduleId is { } scheduleId && (!schedulesById.TryGetValue(scheduleId, out var schedule) || schedule.ClassId != session.ClassId))
                throw new InvalidOperationException($"Session {session.Id} references a missing or unrelated schedule.");
            if (session.RoomId is { } roomId && (!roomsById.ContainsKey(roomId) || session.Mode == SessionMode.Online))
                throw new InvalidOperationException($"Session {session.Id} references an invalid room.");
        }
        var scheduled = sessions.Where(x => x.Status == SessionStatus.Scheduled).ToArray();
        if (!sessions.Any(x => x.Status == SessionStatus.Cancelled) || !sessions.Any(x => x.ModeOverridden) ||
            !sessions.Any(x => x.RoomOverridden) || !scheduled.Any(x => x.Mode == SessionMode.Online) ||
            !scheduled.Any(x => x.Mode == SessionMode.Offline))
            throw new InvalidOperationException("The demo sessions are missing a scheduled/cancelled, online/offline, or override scenario.");
        if (!scheduled.Any(a => scheduled.Any(b => a.Id != b.Id && a.ClassId != b.ClassId && a.StartAt == b.StartAt &&
                a.EndAt == b.EndAt && a.TeacherId != b.TeacherId && a.RoomId != b.RoomId)))
            throw new InvalidOperationException("The demo data is missing the simultaneous classes scenario.");
        if (classes.GroupBy(x => x.DefaultRoomId).Any(g => g.Key is not null && g.Count() > 1) == false)
            throw new InvalidOperationException("The demo data is missing a room reused by classes on non-overlapping days.");
        for (var i = 0; i < scheduled.Length; i++)
        for (var j = i + 1; j < scheduled.Length; j++)
            if (SchedulingRules.Conflicts(scheduled[i], scheduled[j]))
                throw new InvalidOperationException($"Overlapping scheduled sessions found: {scheduled[i].Id} and {scheduled[j].Id}.");
        if (await db.ClassSessionAudits.CountAsync(x => sessions.Select(s => s.Id).Contains(x.ClassSessionId), ct) < sessions.Count ||
            await db.ClassScheduleAudits.CountAsync(x => schedules.Select(s => s.Id).Contains(x.ClassScheduleId), ct) < schedules.Count)
            throw new InvalidOperationException("A seeded session or schedule is missing its audit history.");
        foreach (var room in roomsById.Values)
        foreach (var cls in classes.Where(x => x.DefaultRoomId == room.Id))
            if (enrollments.Count(x => x.ClassId == cls.Id) > room.Capacity)
                throw new InvalidOperationException($"Room {room.Name} is too small for {cls.Code}.");
        // Exercise the same query handlers used by the REST endpoints as a final retrieval check.
        foreach (var cls in classes)
        {
            _ = await sender.Send(new GetClassByIdQuery(cls.Id, null), ct);
            _ = await sender.Send(new ListSchedulesQuery(cls.Id), ct);
        }
        var from = DateTime.SpecifyKind(sessions.Min(x => x.StartAt).AddDays(-1), DateTimeKind.Utc);
        var to = DateTime.SpecifyKind(sessions.Max(x => x.EndAt).AddDays(1), DateTimeKind.Utc);
        _ = await sender.Send(new ListSessionsQuery(SeedActor, "Admin", null, null, null, from, to, 1, 100), ct);
        return new ClassroomDemoSeedReport(centers.Length, rooms.Length, classes.Length, enrollments.Count,
            schedules.Count, sessions.Count, 0, 0, 0);
    }

    private static DateOnly NextMonday(DateOnly date)
    {
        var daysUntilMonday = ((int)DayOfWeek.Monday - (int)date.DayOfWeek + 7) % 7;
        if (daysUntilMonday == 0) daysUntilMonday = 7;
        return date.AddDays(daysUntilMonday);
    }
}
