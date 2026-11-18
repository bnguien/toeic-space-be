using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ToeicSpace.Classroom.Application.Features.GoogleMeet;
using ToeicSpace.Classroom.Domain.Entities;
using ToeicSpace.Classroom.Domain.Enums;
using ToeicSpace.Classroom.Domain.Exceptions;
using ToeicSpace.Classroom.Infrastructure.Persistence;

namespace ToeicSpace.Classroom.Infrastructure.Services.GoogleMeet;

public sealed partial class GoogleMeetOperations(ClassroomDbContext db, IConfiguration config, IHttpClientFactory clients,
    IBackgroundJobClient jobs, TimeProvider clock, ILogger<GoogleMeetOperations> logger) : IGoogleMeetOperations
{
    private const string MeetScope = "https://www.googleapis.com/auth/meetings.space.created";
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private string ClientId => Required("GoogleMeet:ClientId");
    private string ClientSecret => Required("GoogleMeet:ClientSecret");
    private string RedirectUri => Required("GoogleMeet:RedirectUri");
    private string Required(string key) => config[key] is { Length: > 0 } value ? value :
        throw new ClassRuleException("google_not_configured", $"Configure {key}.", 503);

    private byte[] EncryptionKey()
    {
        byte[] key;
        try { key = Convert.FromBase64String(Required("GoogleMeet:EncryptionKey")); }
        catch (FormatException) { throw new InvalidOperationException("GoogleMeet:EncryptionKey must be Base64."); }
        if (key.Length != 32) throw new InvalidOperationException("GoogleMeet:EncryptionKey must contain 32 bytes.");
        return key;
    }

    private string Encrypt(string token)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var data = Encoding.UTF8.GetBytes(token);
        var cipher = new byte[data.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(EncryptionKey(), 16);
        aes.Encrypt(nonce, data, cipher, tag);
        return Convert.ToBase64String(nonce.Concat(tag).Concat(cipher).ToArray());
    }

    private string Decrypt(string encrypted)
    {
        var bytes = Convert.FromBase64String(encrypted);
        var data = new byte[bytes.Length - 28];
        using var aes = new AesGcm(EncryptionKey(), 16);
        aes.Decrypt(bytes[..12], bytes[28..], bytes[12..28], data);
        return Encoding.UTF8.GetString(data);
    }

    private static string Hash(string state) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(state)));
    private static ClassRuleException NotFound() => new("session_not_found", "Session not found.", 404);

    public async Task<string> StartAsync(Guid teacherId, CancellationToken ct)
    {
        var clientId = ClientId;
        var redirectUri = RedirectUri;
        _ = ClientSecret;
        _ = EncryptionKey();
        if (!await db.Teachers.AnyAsync(x => x.TeacherId == teacherId && x.IsEligible, ct))
            throw new ClassRuleException("teacher_not_eligible", "An active Teacher account is required.", 403);
        var state = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        db.GoogleOAuthAttempts.Add(new GoogleOAuthAttempt { StateHash = Hash(state), TeacherId = teacherId,
            ExpiresAt = Now.AddMinutes(10) });
        await db.SaveChangesAsync(ct);
        var query = new Dictionary<string, string>
        {
            ["client_id"] = clientId, ["redirect_uri"] = redirectUri, ["response_type"] = "code",
            ["scope"] = $"openid email {MeetScope}", ["access_type"] = "offline", ["prompt"] = "consent",
            ["state"] = state
        };
        return "https://accounts.google.com/o/oauth2/v2/auth?" + string.Join("&",
            query.Select(x => $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value)}"));
    }

    public async Task CompleteAsync(string state, string? code, string? error, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(state)) throw new ClassRuleException("invalid_oauth_state", "Invalid OAuth state.");
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        var hash = Hash(state);
        var attempt = db.Database.IsRelational()
            ? await db.GoogleOAuthAttempts.FromSqlInterpolated(
                $"SELECT * FROM GoogleOAuthAttempts WHERE StateHash = {hash} FOR UPDATE").SingleOrDefaultAsync(ct)
            : await db.GoogleOAuthAttempts.SingleOrDefaultAsync(x => x.StateHash == hash, ct);
        if (attempt is null || attempt.ExpiresAt <= Now)
            throw new ClassRuleException("invalid_oauth_state", "OAuth state expired or already used.");
        db.GoogleOAuthAttempts.Remove(attempt);
        await db.SaveChangesAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);
        if (!string.IsNullOrEmpty(error) || string.IsNullOrWhiteSpace(code))
            throw new ClassRuleException("google_oauth_denied", "Google authorization was not completed.");
        if (!await db.Teachers.AnyAsync(x => x.TeacherId == attempt.TeacherId && x.IsEligible, ct))
            throw new ClassRuleException("teacher_not_eligible", "Teacher is no longer eligible.", 403);
        using var response = await clients.CreateClient("GoogleMeet").PostAsync("https://oauth2.googleapis.com/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = code, ["client_id"] = ClientId, ["client_secret"] = ClientSecret,
                ["redirect_uri"] = RedirectUri, ["grant_type"] = "authorization_code"
            }), ct);
        if (!response.IsSuccessStatusCode) throw new ClassRuleException("google_oauth_exchange_failed", "Google token exchange failed.", 502);
        using var tokenJson = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var token = tokenJson.RootElement;
        if (!token.TryGetProperty("refresh_token", out var refresh) || string.IsNullOrEmpty(refresh.GetString()))
            throw new ClassRuleException("google_refresh_token_missing", "Google did not return offline access. Reconnect with consent.", 409);
        if (!token.TryGetProperty("scope", out var scopes) ||
            !scopes.GetString()!.Split(' ').Contains(MeetScope))
            throw new ClassRuleException("google_meet_scope_missing", "Google Meet permission was not granted.", 403);
        using var profileRequest = new HttpRequestMessage(HttpMethod.Get, "https://openidconnect.googleapis.com/v1/userinfo");
        profileRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.GetProperty("access_token").GetString());
        using var profileResponse = await clients.CreateClient("GoogleMeet").SendAsync(profileRequest, ct);
        if (!profileResponse.IsSuccessStatusCode) throw new ClassRuleException("google_profile_failed", "Could not verify Google account.", 502);
        using var profile = JsonDocument.Parse(await profileResponse.Content.ReadAsStringAsync(ct));
        if (!profile.RootElement.TryGetProperty("email_verified", out var verified) || !verified.GetBoolean())
            throw new ClassRuleException("google_email_unverified", "Google email must be verified.", 403);
        var email = profile.RootElement.GetProperty("email").GetString() ?? "";
        var connection = await db.TeacherGoogleConnections.SingleOrDefaultAsync(x => x.TeacherId == attempt.TeacherId, ct);
        if (connection is null)
        {
            connection = new TeacherGoogleConnection { TeacherId = attempt.TeacherId };
            db.TeacherGoogleConnections.Add(connection);
        }
        connection.ConnectionId = Guid.NewGuid();
        connection.Email = email;
        connection.EncryptedRefreshToken = Encrypt(refresh.GetString()!);
        connection.ConnectedAt = Now;
        connection.RevokedAt = null;
        connection.Version++;
        await db.SaveChangesAsync(ct);
        await InvalidateTeacherSessionsAsync(attempt.TeacherId, ct);
    }

    public async Task<GoogleConnectionStatus> ConnectionAsync(Guid teacherId, CancellationToken ct)
    {
        var connection = await db.TeacherGoogleConnections.AsNoTracking().SingleOrDefaultAsync(x => x.TeacherId == teacherId, ct);
        return connection is { RevokedAt: null } && connection.EncryptedRefreshToken.Length > 0
            ? new(true, connection.Email, connection.ConnectedAt) : new(false, null, null);
    }

    public async Task DisconnectAsync(Guid teacherId, CancellationToken ct)
    {
        var connection = await db.TeacherGoogleConnections.SingleOrDefaultAsync(x => x.TeacherId == teacherId, ct);
        if (connection is null) return;
        connection.EncryptedRefreshToken = "";
        connection.RevokedAt = Now;
        connection.Version++;
        await db.SaveChangesAsync(ct);
        await InvalidateTeacherSessionsAsync(teacherId, ct);
    }

    private async Task InvalidateTeacherSessionsAsync(Guid teacherId, CancellationToken ct)
    {
        var rows = await db.SessionMeetProvisionings.Where(x => x.TeacherId == teacherId).ToListAsync(ct);
        foreach (var row in rows)
        {
            row.ConnectionId = null;
            row.SpaceName = null;
            row.MeetingUrl = null;
            row.Status = "NeedsConnection";
            row.LeaseId = null;
            row.LeaseUntil = null;
            row.UpdatedAt = Now;
            row.Version++;
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task<(ClassSession Session, SessionMeetProvisioning? State)> AuthorizeAsync(
        Guid sessionId, Guid actorId, string role, CancellationToken ct)
    {
        var session = await db.ClassSessions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == sessionId, ct) ?? throw NotFound();
        if (role == "Teacher" && !await db.ClassInstructors.AnyAsync(x => x.ClassId == session.ClassId &&
            x.TeacherId == actorId && x.UnassignedAt == null, ct))
            throw new ClassRuleException("forbidden_class", "No access to this class.", 403);
        if (role == "User" && !await db.ClassEnrollments.AnyAsync(x => x.ClassId == session.ClassId &&
            x.StudentId == actorId && x.Status == EnrollmentStatus.Active && x.WithdrawnAt == null, ct))
            throw new ClassRuleException("forbidden_class", "No access to this class.", 403);
        if (role is not ("Admin" or "Teacher" or "User"))
            throw new ClassRuleException("forbidden_class", "No access to this class.", 403);
        var state = await db.SessionMeetProvisionings.AsNoTracking().SingleOrDefaultAsync(x => x.ClassSessionId == sessionId, ct);
        return (session, state);
    }

    public async Task<MeetStatus> StatusAsync(Guid sessionId, Guid actorId, string role, CancellationToken ct)
    {
        var (session, state) = await AuthorizeAsync(sessionId, actorId, role, ct);
        if (session.Mode != SessionMode.Online || session.Status != SessionStatus.Scheduled)
            return new("Inactive", null, false);
        if (!await db.Teachers.AnyAsync(x => x.TeacherId == session.TeacherId && x.IsEligible, ct))
            return new("Failed", "teacher_ineligible", false);
        var connection = await db.TeacherGoogleConnections.AsNoTracking().SingleOrDefaultAsync(x => x.TeacherId == session.TeacherId, ct);
        var valid = connection is { RevokedAt: null } && connection.EncryptedRefreshToken.Length > 0 &&
            state?.ConnectionId == connection.ConnectionId &&
            state.TeacherId == session.TeacherId && state.SessionVersion == session.Version;
        var window = role != "User" || StudentWindow(session);
        var ready = valid && state!.Status == "Ready" && state.MeetingUrl is not null;
        var shown = ready ? "Ready" : connection is null || connection.RevokedAt is not null ? "NeedsConnection" :
            state?.Status == "Ready" ? "Pending" : state?.Status ?? "Pending";
        return new(shown, state?.ErrorCode, ready && window);
    }

    private bool StudentWindow(ClassSession session)
    {
        var before = Math.Clamp(config.GetValue("GoogleMeet:StudentJoinMinutesBefore", 15), 0, 1440);
        var after = Math.Clamp(config.GetValue("GoogleMeet:StudentJoinMinutesAfter", 30), 0, 1440);
        return Now >= session.StartAt.AddMinutes(-before) && Now <= session.EndAt.AddMinutes(after);
    }

    public async Task<MeetLink> LinkAsync(Guid sessionId, Guid actorId, string role, CancellationToken ct)
    {
        var status = await StatusAsync(sessionId, actorId, role, ct);
        if (!status.CanJoin) throw new ClassRuleException("meet_link_unavailable", "Meet link is not available.", 403);
        var state = await db.SessionMeetProvisionings.AsNoTracking().SingleAsync(x => x.ClassSessionId == sessionId, ct);
        return new(state.MeetingUrl!);
    }

    public async Task RetryAsync(Guid sessionId, CancellationToken ct)
    {
        var session = await db.ClassSessions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == sessionId, ct)
            ?? throw NotFound();
        if (session.Mode != SessionMode.Online || session.Status != SessionStatus.Scheduled)
            throw new ClassRuleException("meet_retry_unavailable", "Only active Online Sessions can be retried.", 409);
        var state = await db.SessionMeetProvisionings.SingleOrDefaultAsync(x => x.ClassSessionId == sessionId, ct);
        if (state is null || state.Status != "Failed")
            throw new ClassRuleException("meet_retry_unavailable", "Only failed provisioning can be retried.", 409);
        state.Status = "Pending";
        state.ErrorCode = null;
        state.NextAttemptAt = null;
        state.Version++;
        await db.SaveChangesAsync(ct);
        jobs.Enqueue<GoogleMeetProvisioningJob>(x => x.ProvisionAsync(sessionId, CancellationToken.None));
    }
}
