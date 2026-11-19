namespace ToeicSpace.Assessment.API.Contracts;

/// <param name="ExpectedGeneration">From GET practice-sets/{id}/attempts/active.</param>
/// <param name="Selection">All (default), Continue, Random or Mistakes.</param>
/// <param name="Count">Soft target (5–200) for Continue/Random/Mistakes.</param>
/// <param name="Restart">Abandon the in-progress session on this set and start a new one.</param>
public sealed record StartPracticeSetAttemptRequest(
    long ExpectedGeneration,
    PracticeSelection? Selection,
    int? Count,
    bool? Restart);
