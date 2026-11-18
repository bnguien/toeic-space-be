using MediatR;

namespace ToeicSpace.Classroom.Application.Features.GoogleMeet;

public sealed record GoogleConnectionStatus(bool Connected, string? Email, DateTime? ConnectedAt);

public sealed record MeetStatus(string Status, string? ErrorCode, bool CanJoin);

public sealed record MeetLink(string Url);

public interface IGoogleMeetOperations
{
    Task<string> StartAsync(Guid teacherId, CancellationToken ct);
    Task CompleteAsync(string state, string? code, string? error, CancellationToken ct);
    Task<GoogleConnectionStatus> ConnectionAsync(Guid teacherId, CancellationToken ct);
    Task DisconnectAsync(Guid teacherId, CancellationToken ct);
    Task<MeetStatus> StatusAsync(Guid sessionId, Guid actorId, string role, CancellationToken ct);
    Task<MeetLink> LinkAsync(Guid sessionId, Guid actorId, string role, CancellationToken ct);
    Task RetryAsync(Guid sessionId, CancellationToken ct);
}

public sealed record StartGoogleConnection(Guid TeacherId) : IRequest<string>;

public sealed record CompleteGoogleConnection(string State, string? Code, string? Error) : IRequest;

public sealed record GetGoogleConnection(Guid TeacherId) : IRequest<GoogleConnectionStatus>;

public sealed record DisconnectGoogleConnection(Guid TeacherId) : IRequest;

public sealed record GetMeetStatus(Guid SessionId, Guid ActorId, string Role) : IRequest<MeetStatus>;

public sealed record GetMeetLink(Guid SessionId, Guid ActorId, string Role) : IRequest<MeetLink>;

public sealed record RetryMeetProvisioning(Guid SessionId) : IRequest;

public sealed class GoogleMeetHandlers(IGoogleMeetOperations operations) :
    IRequestHandler<StartGoogleConnection, string>, IRequestHandler<CompleteGoogleConnection>,
    IRequestHandler<GetGoogleConnection, GoogleConnectionStatus>, IRequestHandler<DisconnectGoogleConnection>,
    IRequestHandler<GetMeetStatus, MeetStatus>, IRequestHandler<GetMeetLink, MeetLink>,
    IRequestHandler<RetryMeetProvisioning>
{
    public Task<string> Handle(StartGoogleConnection r, CancellationToken ct) => operations.StartAsync(r.TeacherId, ct);

    public Task Handle(CompleteGoogleConnection r, CancellationToken ct) =>
        operations.CompleteAsync(r.State, r.Code, r.Error, ct);

    public Task<GoogleConnectionStatus> Handle(GetGoogleConnection r, CancellationToken ct) =>
        operations.ConnectionAsync(r.TeacherId, ct);

    public Task Handle(DisconnectGoogleConnection r, CancellationToken ct) =>
        operations.DisconnectAsync(r.TeacherId, ct);

    public Task<MeetStatus> Handle(GetMeetStatus r, CancellationToken ct) =>
        operations.StatusAsync(r.SessionId, r.ActorId, r.Role, ct);

    public Task<MeetLink> Handle(GetMeetLink r, CancellationToken ct) =>
        operations.LinkAsync(r.SessionId, r.ActorId, r.Role, ct);

    public Task Handle(RetryMeetProvisioning r, CancellationToken ct) => operations.RetryAsync(r.SessionId, ct);
}