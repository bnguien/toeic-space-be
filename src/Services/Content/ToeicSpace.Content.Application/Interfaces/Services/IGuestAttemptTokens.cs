namespace ToeicSpace.Content.Application.Interfaces.Services;

/// <summary>Where a guest attempt was made: a topic block or a stage test.</summary>
public sealed record GuestAttemptRef(string? Topic, string? Stage, string Block);

/// <summary>
/// Signed, stateless attempt ids for guests (<c>g.…</c>, valid 6 hours) so in-place fixes and reveals work without
/// storing anything.
/// </summary>
public interface IGuestAttemptTokens
{
    public const string Prefix = "g.";
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(6);

    string Issue(GuestAttemptRef attempt);

    /// <summary>The attempt of a valid, unexpired token; otherwise null.</summary>
    GuestAttemptRef? Read(string token);
}
