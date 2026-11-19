namespace ToeicSpace.Assessment.API.Contracts;

/// <param name="Scope">Wrong (wrong and unanswered questions) or All.</param>
public sealed record RetakeAttemptRequest(RetakeScope? Scope);
