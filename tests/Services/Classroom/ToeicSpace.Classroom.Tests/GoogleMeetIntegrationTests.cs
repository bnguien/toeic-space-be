using System.Net;
using System.Text;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ToeicSpace.Classroom.Domain.Entities;
using ToeicSpace.Classroom.Domain.Enums;
using ToeicSpace.Classroom.Domain.Exceptions;
using ToeicSpace.Classroom.Infrastructure.Persistence;
using ToeicSpace.Classroom.Infrastructure.Services.GoogleMeet;
using Xunit;

namespace ToeicSpace.Classroom.Tests;

public sealed class GoogleMeetIntegrationTests
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Value = new(2026, 11, 15, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Value;
    }

    private sealed class GoogleHandler : HttpMessageHandler
    {
        public int Created;
        public bool FailCreate;
        public bool RevokeRefresh;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var url = request.RequestUri!.AbsoluteUri;
            if (url.Contains("oauth2.googleapis.com/token"))
            {
                var body = await request.Content!.ReadAsStringAsync(ct);
                if (RevokeRefresh && body.Contains("refresh_token"))
                    return Json("{\"error\":\"invalid_grant\"}", HttpStatusCode.BadRequest);
                return Json(body.Contains("refresh_token") ? "{\"access_token\":\"access\"}" :
                    "{\"access_token\":\"access\",\"refresh_token\":\"refresh-secret\",\"scope\":\"openid email https://www.googleapis.com/auth/meetings.space.created\"}");
            }
            if (url.Contains("userinfo")) return Json("{\"email\":\"teacher@gmail.com\",\"email_verified\":true}");
            if (url.Contains("meet.googleapis.com"))
            {
                Created++;
                return FailCreate ? Json("{}", HttpStatusCode.ServiceUnavailable) :
                    Json($"{{\"name\":\"spaces/{Created}\",\"meetingUri\":\"https://meet.google.com/demo-{Created}\"}}");
            }
            throw new InvalidOperationException("Unexpected Google URL.");
        }
        private static HttpResponseMessage Json(string value, HttpStatusCode status = HttpStatusCode.OK) =>
            new(status) { Content = new StringContent(value, Encoding.UTF8, "application/json") };
    }

    private sealed class Factory(GoogleHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class Jobs : IBackgroundJobClient
    {
        public int Created;
        public string Create(Job job, IState state) { Created++; return Created.ToString(); }
        public bool ChangeState(string jobId, IState state, string expectedState) => true;
    }

    private static (ClassroomDbContext Db, GoogleMeetOperations Operations, GoogleMeetProvisioningJob Job,
        GoogleHandler Google, Clock Clock, Jobs Jobs, Guid Teacher, Guid Student, ClassSession Session) Setup()
    {
        var db = new ClassroomDbContext(new DbContextOptionsBuilder<ClassroomDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var teacher = Guid.NewGuid();
        var student = Guid.NewGuid();
        var cls = new Class { Code = "TOEIC-MEET", Name = "TOEIC Meet", Status = ClassStatus.Active,
            StartDate = new DateOnly(2026, 11, 15), EndDate = new DateOnly(2026, 12, 31) };
        var session = new ClassSession { ClassId = cls.Id, TeacherId = teacher, Mode = SessionMode.Online,
            Status = SessionStatus.Scheduled, Title = "Online TOEIC", StartAt = new DateTime(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc),
            EndAt = new DateTime(2026, 11, 16, 10, 0, 0, DateTimeKind.Utc) };
        db.Classes.Add(cls);
        db.ClassSessions.Add(session);
        db.ClassInstructors.Add(new ClassInstructor { ClassId = cls.Id, TeacherId = teacher });
        db.ClassEnrollments.Add(new ClassEnrollment { ClassId = cls.Id, StudentId = student, Status = EnrollmentStatus.Active });
        db.Teachers.Add(new TeacherReadModel { TeacherId = teacher, Role = "Teacher", Status = "Active", IsEligible = true });
        db.SaveChanges();
        var google = new GoogleHandler();
        var jobs = new Jobs();
        var clock = new Clock();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["GoogleMeet:ClientId"] = "test-client", ["GoogleMeet:ClientSecret"] = "test-secret",
            ["GoogleMeet:RedirectUri"] = "https://localhost/callback",
            ["GoogleMeet:EncryptionKey"] = Convert.ToBase64String(Enumerable.Repeat((byte)7, 32).ToArray())
        }).Build();
        var operations = new GoogleMeetOperations(db, config, new Factory(google), jobs, clock,
            NullLogger<GoogleMeetOperations>.Instance);
        return (db, operations, new GoogleMeetProvisioningJob(operations, db, jobs, clock), google, clock, jobs,
            teacher, student, session);
    }

    private static async Task<string> Connect(GoogleMeetOperations operations, Guid teacher)
    {
        var url = await operations.StartAsync(teacher, default);
        var state = Uri.UnescapeDataString(new Uri(url).Query.TrimStart('?').Split('&')
            .Single(x => x.StartsWith("state=")).Split('=')[1]);
        await operations.CompleteAsync(state, "test-code", null, default);
        return state;
    }

    [Fact]
    public async Task Teacher_change_and_mode_change_invalidate_the_previous_link()
    {
        var (db, operations, job, google, _, _, teacher, _, session) = Setup();
        await using (db)
        {
            await Connect(operations, teacher);
            await job.ProvisionAsync(session.Id, default);
            var previous = (await operations.LinkAsync(session.Id, teacher, "Teacher", default)).Url;
            var nextTeacher = Guid.NewGuid();
            db.Teachers.Add(new TeacherReadModel { TeacherId = nextTeacher, Role = "Teacher", Status = "Active", IsEligible = true });
            db.ClassInstructors.Single().UnassignedAt = DateTime.UtcNow;
            db.ClassInstructors.Add(new ClassInstructor { ClassId = session.ClassId, TeacherId = nextTeacher });
            session.TeacherId = nextTeacher;
            session.Version++;
            await db.SaveChangesAsync();
            Assert.False((await operations.StatusAsync(session.Id, nextTeacher, "Teacher", default)).CanJoin);
            await Connect(operations, nextTeacher);
            await job.ProvisionAsync(session.Id, default);
            Assert.NotEqual(previous, (await operations.LinkAsync(session.Id, nextTeacher, "Teacher", default)).Url);
            Assert.Equal(2, google.Created);
            session.Mode = SessionMode.Offline;
            session.Version++;
            await db.SaveChangesAsync();
            Assert.False((await operations.StatusAsync(session.Id, nextTeacher, "Teacher", default)).CanJoin);
            await job.ProvisionAsync(session.Id, default);
            Assert.Equal("Inactive", db.SessionMeetProvisionings.Single().Status);
        }
    }

    [Fact]
    public async Task OAuth_state_is_single_use_and_refresh_token_is_encrypted()
    {
        var (db, operations, _, _, _, _, teacher, _, _) = Setup();
        await using (db)
        {
            await Assert.ThrowsAsync<ClassRuleException>(() => operations.CompleteAsync("invalid", "code", null, default));
            var state = await Connect(operations, teacher);
            Assert.Equal("invalid_oauth_state", (await Assert.ThrowsAsync<ClassRuleException>(() =>
                operations.CompleteAsync(state, "replay-code", null, default))).Code);
            Assert.True((await operations.ConnectionAsync(teacher, default)).Connected);
            Assert.DoesNotContain("refresh-secret", db.TeacherGoogleConnections.Single().EncryptedRefreshToken);
            await operations.DisconnectAsync(teacher, default);
            Assert.False((await operations.ConnectionAsync(teacher, default)).Connected);
        }
    }

    [Fact]
    public async Task Online_space_is_reused_and_student_join_requires_active_enrollment_and_time_window()
    {
        var (db, operations, job, google, clock, _, teacher, student, session) = Setup();
        await using (db)
        {
            await job.ProvisionAsync(session.Id, default);
            Assert.Equal("NeedsConnection", (await operations.StatusAsync(session.Id, teacher, "Teacher", default)).Status);
            await Connect(operations, teacher);
            await job.ProvisionAsync(session.Id, default);
            await job.ProvisionAsync(session.Id, default);
            Assert.Equal(1, google.Created);
            Assert.Equal("Ready", (await operations.StatusAsync(session.Id, teacher, "Teacher", default)).Status);
            Assert.Equal("forbidden_class", (await Assert.ThrowsAsync<ClassRuleException>(() =>
                operations.LinkAsync(session.Id, Guid.NewGuid(), "User", default))).Code);
            Assert.Equal("meet_link_unavailable", (await Assert.ThrowsAsync<ClassRuleException>(() =>
                operations.LinkAsync(session.Id, student, "User", default))).Code);
            clock.Value = new DateTimeOffset(2026, 11, 16, 8, 50, 0, TimeSpan.Zero);
            Assert.StartsWith("https://meet.google.com/", (await operations.LinkAsync(session.Id, student, "User", default)).Url);
            session.Status = SessionStatus.Cancelled;
            session.Version++;
            await db.SaveChangesAsync();
            Assert.False((await operations.StatusAsync(session.Id, student, "User", default)).CanJoin);
        }
    }

    [Fact]
    public async Task Offline_and_failed_provisioning_do_not_publish_a_join_link()
    {
        var (db, operations, job, google, _, jobs, teacher, _, session) = Setup();
        await using (db)
        {
            session.Mode = SessionMode.Offline;
            await db.SaveChangesAsync();
            await job.ProvisionAsync(session.Id, default);
            Assert.Equal(0, google.Created);
            session.Mode = SessionMode.Online;
            session.Version++;
            await db.SaveChangesAsync();
            await Connect(operations, teacher);
            google.FailCreate = true;
            await job.ProvisionAsync(session.Id, default);
            Assert.Equal("Failed", (await operations.StatusAsync(session.Id, teacher, "Teacher", default)).Status);
            Assert.Equal("uncertain_google_result", db.SessionMeetProvisionings.Single().ErrorCode);
            await operations.RetryAsync(session.Id, default);
            Assert.Equal(1, jobs.Created);
            Assert.Equal("Pending", db.SessionMeetProvisionings.Single().Status);
            google.FailCreate = false;
            await job.ProvisionAsync(session.Id, default);
            Assert.Equal("Ready", (await operations.StatusAsync(session.Id, teacher, "Teacher", default)).Status);
        }
    }

    [Fact]
    public async Task Revoked_refresh_token_requires_a_new_connection()
    {
        var (db, operations, job, google, _, _, teacher, _, session) = Setup();
        await using (db)
        {
            await Connect(operations, teacher);
            google.RevokeRefresh = true;
            await job.ProvisionAsync(session.Id, default);
            Assert.Equal(0, google.Created);
            Assert.False((await operations.ConnectionAsync(teacher, default)).Connected);
            Assert.Equal("NeedsConnection", (await operations.StatusAsync(session.Id, teacher, "Teacher", default)).Status);
        }
    }

    [Fact]
    public async Task Active_lease_prevents_duplicate_create_and_expired_lease_requires_review()
    {
        var (db, operations, job, google, clock, _, teacher, _, session) = Setup();
        await using (db)
        {
            await Connect(operations, teacher);
            db.SessionMeetProvisionings.Add(new SessionMeetProvisioning
            {
                ClassSessionId = session.Id, TeacherId = teacher,
                ConnectionId = db.TeacherGoogleConnections.Single().ConnectionId,
                SessionVersion = session.Version, Status = "Provisioning", LeaseId = Guid.NewGuid(),
                LeaseUntil = clock.GetUtcNow().UtcDateTime.AddMinutes(5)
            });
            await db.SaveChangesAsync();
            await job.ProvisionAsync(session.Id, default);
            Assert.Equal(0, google.Created);
            clock.Value = clock.Value.AddMinutes(6);
            await job.ProvisionAsync(session.Id, default);
            Assert.Equal("Failed", db.SessionMeetProvisionings.Single().Status);
            Assert.Equal("uncertain_google_result", db.SessionMeetProvisionings.Single().ErrorCode);
            Assert.Equal(0, google.Created);
        }
    }
}
