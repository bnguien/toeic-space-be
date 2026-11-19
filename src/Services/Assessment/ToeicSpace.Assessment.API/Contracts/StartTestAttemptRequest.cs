namespace ToeicSpace.Assessment.API.Contracts;

/// <param name="Parts">Practice only: the parts to include (1–7).</param>
/// <param name="Restart">Abandon the in-progress attempt on this test and start a new one.</param>
public sealed record StartTestAttemptRequest(
    AttemptMode? Mode,
    IReadOnlyList<int>? Parts,
    bool? Restart);
