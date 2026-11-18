using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ToeicSpace.Classroom.Domain.Entities;
using ToeicSpace.Classroom.Domain.Enums;
using ToeicSpace.Classroom.Infrastructure.Persistence;

namespace ToeicSpace.Classroom.Infrastructure.Services.GoogleMeet;

public sealed class GoogleMeetProvisioningJob(GoogleMeetOperations operations, ClassroomDbContext db,
    IBackgroundJobClient jobs, TimeProvider clock)
{
    public Task ProvisionAsync(Guid sessionId, CancellationToken ct) => operations.ProvisionAsync(sessionId, ct);

    public async Task ScanAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var sessions = await db.ClassSessions.AsNoTracking()
            .Where(x => x.Mode == SessionMode.Online && x.Status == SessionStatus.Scheduled &&
                x.EndAt >= now && x.StartAt <= now.AddDays(90))
            .OrderBy(x => x.StartAt).Take(500).ToListAsync(ct);
        var ids = sessions.Select(x => x.Id).ToArray();
        var teacherIds = sessions.Select(x => x.TeacherId).Distinct().ToArray();
        var connected = await db.TeacherGoogleConnections.AsNoTracking()
            .Where(x => teacherIds.Contains(x.TeacherId) && x.RevokedAt == null && x.EncryptedRefreshToken != "")
            .Select(x => x.TeacherId).ToHashSetAsync(ct);
        var states = await db.SessionMeetProvisionings.AsNoTracking().Where(x => ids.Contains(x.ClassSessionId))
            .ToDictionaryAsync(x => x.ClassSessionId, ct);
        foreach (var session in sessions)
        {
            states.TryGetValue(session.Id, out var state);
            if (state is null || state.TeacherId != session.TeacherId || state.SessionVersion != session.Version ||
                state.Status == "Pending" || state.Status == "Failed" && state.NextAttemptAt <= now ||
                state.Status == "NeedsConnection" && connected.Contains(session.TeacherId))
                jobs.Enqueue<GoogleMeetProvisioningJob>(x => x.ProvisionAsync(session.Id, CancellationToken.None));
            else if (state.Status == "Provisioning" && state.LeaseUntil <= now)
                jobs.Enqueue<GoogleMeetProvisioningJob>(x => x.ProvisionAsync(session.Id, CancellationToken.None));
        }
    }
}

public sealed partial class GoogleMeetOperations
{
    internal async Task ProvisionAsync(Guid sessionId, CancellationToken ct)
    {
        Guid lease;
        Guid teacherId;
        Guid connectionId;
        int sessionVersion;
        string encryptedToken;
        await using (var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null)
        {
            var session = db.Database.IsRelational()
                ? await db.ClassSessions.FromSqlInterpolated(
                    $"SELECT * FROM ClassSessions WHERE Id = {sessionId} FOR UPDATE").SingleOrDefaultAsync(ct)
                : await db.ClassSessions.SingleOrDefaultAsync(x => x.Id == sessionId, ct);
            if (session is null || session.Mode != SessionMode.Online || session.Status != SessionStatus.Scheduled)
            {
                var inactive = await db.SessionMeetProvisionings.SingleOrDefaultAsync(x => x.ClassSessionId == sessionId, ct);
                if (inactive is not null)
                {
                    inactive.Status = "Inactive"; inactive.MeetingUrl = null; inactive.SpaceName = null;
                    inactive.LeaseId = null; inactive.LeaseUntil = null; inactive.Version++;
                    await db.SaveChangesAsync(ct);
                }
                if (tx is not null) await tx.CommitAsync(ct);
                return;
            }
            var state = await db.SessionMeetProvisionings.SingleOrDefaultAsync(x => x.ClassSessionId == sessionId, ct);
            if (state is null)
            {
                state = new SessionMeetProvisioning { ClassSessionId = sessionId };
                db.SessionMeetProvisionings.Add(state);
            }
            var connection = await db.TeacherGoogleConnections.AsNoTracking()
                .SingleOrDefaultAsync(x => x.TeacherId == session.TeacherId, ct);
            var ownerChanged = state.TeacherId != session.TeacherId ||
                state.ConnectionId != connection?.ConnectionId;
            if (ownerChanged || state.SessionVersion != session.Version && state.Status != "Ready")
            {
                state.SpaceName = null; state.MeetingUrl = null; state.ErrorCode = null;
                state.Attempts = 0; state.LeaseId = null; state.LeaseUntil = null;
                state.Status = "Pending"; state.NextAttemptAt = null;
            }
            state.TeacherId = session.TeacherId;
            state.SessionVersion = session.Version;
            state.ConnectionId = connection?.ConnectionId;
            state.UpdatedAt = Now;
            if (!await db.Teachers.AnyAsync(x => x.TeacherId == session.TeacherId && x.IsEligible, ct))
            {
                state.Status = "Failed"; state.ErrorCode = "teacher_ineligible";
                state.MeetingUrl = null; state.SpaceName = null; state.NextAttemptAt = null;
                state.Version++;
                await db.SaveChangesAsync(ct); if (tx is not null) await tx.CommitAsync(ct); return;
            }
            if (connection is null || connection.RevokedAt is not null || connection.EncryptedRefreshToken.Length == 0)
            {
                state.Status = "NeedsConnection"; state.Version++;
                await db.SaveChangesAsync(ct); if (tx is not null) await tx.CommitAsync(ct); return;
            }
            if (state.Status == "Ready" && state.MeetingUrl is not null)
            {
                state.Version++;
                await db.SaveChangesAsync(ct);
                if (tx is not null) await tx.CommitAsync(ct);
                return;
            }
            if (
                state.Status == "Provisioning" && state.LeaseUntil > Now ||
                state.Status == "Failed" && (state.NextAttemptAt is null || state.NextAttemptAt > Now))
            {
                if (tx is not null) await tx.CommitAsync(ct); return;
            }
            if (state.Status == "Provisioning")
            {
                // A worker could have created a space just before it died. Automatic retry would duplicate it.
                state.Status = "Failed"; state.ErrorCode = "uncertain_google_result";
                state.LeaseId = null; state.LeaseUntil = null; state.NextAttemptAt = null;
                state.Version++;
                await db.SaveChangesAsync(ct); if (tx is not null) await tx.CommitAsync(ct); return;
            }
            lease = Guid.NewGuid();
            teacherId = session.TeacherId;
            sessionVersion = session.Version;
            connectionId = connection.ConnectionId;
            encryptedToken = connection.EncryptedRefreshToken;
            state.Status = "Provisioning"; state.LeaseId = lease; state.LeaseUntil = Now.AddMinutes(5);
            state.Attempts++; state.Version++; state.UpdatedAt = Now;
            await db.SaveChangesAsync(ct); if (tx is not null) await tx.CommitAsync(ct);
        }

        string accessToken;
        try
        {
            using var tokenResponse = await clients.CreateClient("GoogleMeet").PostAsync("https://oauth2.googleapis.com/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["refresh_token"] = Decrypt(encryptedToken), ["client_id"] = ClientId,
                    ["client_secret"] = ClientSecret, ["grant_type"] = "refresh_token"
                }), ct);
            if (tokenResponse.StatusCode == HttpStatusCode.BadRequest || tokenResponse.StatusCode == HttpStatusCode.Unauthorized)
            {
                var body = await tokenResponse.Content.ReadAsStringAsync(ct);
                var revoked = body.Contains("invalid_grant", StringComparison.Ordinal);
                await FailAsync(sessionId, lease, revoked ? "credential_revoked" : "google_token_rejected", null, ct);
                if (revoked)
                {
                    var connection = await db.TeacherGoogleConnections.SingleOrDefaultAsync(x => x.TeacherId == teacherId && x.ConnectionId == connectionId, ct);
                    if (connection is not null)
                    {
                        connection.RevokedAt = Now; connection.EncryptedRefreshToken = ""; connection.Version++;
                        await db.SaveChangesAsync(ct);
                    }
                }
                return;
            }
            if (!tokenResponse.IsSuccessStatusCode)
            {
                await FailAsync(sessionId, lease, "google_token_unavailable", Now.AddMinutes(2), ct);
                return;
            }
            using var token = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync(ct));
            accessToken = token.RootElement.GetProperty("access_token").GetString()!;
            if (token.RootElement.TryGetProperty("refresh_token", out var rotated) &&
                !string.IsNullOrWhiteSpace(rotated.GetString()))
            {
                var connection = await db.TeacherGoogleConnections.SingleOrDefaultAsync(x =>
                    x.TeacherId == teacherId && x.ConnectionId == connectionId && x.RevokedAt == null, ct);
                if (connection is not null)
                {
                    connection.EncryptedRefreshToken = Encrypt(rotated.GetString()!);
                    connection.Version++;
                    await db.SaveChangesAsync(ct);
                }
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            await FailAsync(sessionId, lease, "google_token_unavailable", Now.AddMinutes(2), CancellationToken.None);
            return;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://meet.googleapis.com/v2/spaces")
            { Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json") };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await clients.CreateClient("GoogleMeet").SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                await FailAsync(sessionId, lease, response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                    ? "google_meet_permission_denied" : "uncertain_google_result", null, CancellationToken.None);
                return;
            }
            using var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var name = result.RootElement.GetProperty("name").GetString();
            var url = result.RootElement.GetProperty("meetingUri").GetString();
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url))
            {
                await FailAsync(sessionId, lease, "uncertain_google_result", null, CancellationToken.None);
                return;
            }
            await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
            var session = db.Database.IsRelational()
                ? await db.ClassSessions.FromSqlInterpolated(
                    $"SELECT * FROM ClassSessions WHERE Id = {sessionId} FOR UPDATE").SingleAsync(ct)
                : await db.ClassSessions.SingleAsync(x => x.Id == sessionId, ct);
            var state = await db.SessionMeetProvisionings.SingleAsync(x => x.ClassSessionId == sessionId, ct);
            if (state.LeaseId == lease && state.ConnectionId == connectionId && state.TeacherId == teacherId &&
                state.SessionVersion == sessionVersion && session.TeacherId == teacherId && session.Version == sessionVersion &&
                session.Mode == SessionMode.Online && session.Status == SessionStatus.Scheduled)
            {
                state.SpaceName = name; state.MeetingUrl = url; state.Status = "Ready";
                state.ErrorCode = null; state.NextAttemptAt = null; state.LeaseId = null;
                state.LeaseUntil = null; state.UpdatedAt = Now; state.Version++;
                await db.SaveChangesAsync(ct);
            }
            if (tx is not null) await tx.CommitAsync(ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning("Google Meet provisioning outcome is uncertain for session {SessionId}", sessionId);
            await FailAsync(sessionId, lease, "uncertain_google_result", null, CancellationToken.None);
        }
    }

    private async Task FailAsync(Guid sessionId, Guid lease, string code, DateTime? retryAt, CancellationToken ct)
    {
        var state = await db.SessionMeetProvisionings.SingleOrDefaultAsync(x => x.ClassSessionId == sessionId, ct);
        if (state?.LeaseId != lease) return;
        state.Status = code == "credential_revoked" ? "NeedsConnection" : "Failed";
        state.ErrorCode = code;
        state.NextAttemptAt = state.Attempts < 5 && retryAt is not null
            ? Now.AddMinutes(Math.Min(30, 1 << Math.Min(state.Attempts, 5))) : null;
        state.LeaseId = null; state.LeaseUntil = null; state.UpdatedAt = Now; state.Version++;
        await db.SaveChangesAsync(ct);
    }
}
