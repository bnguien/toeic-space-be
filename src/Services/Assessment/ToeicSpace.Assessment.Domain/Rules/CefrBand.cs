namespace ToeicSpace.Assessment.Domain.Rules;

/// <param name="Level">CEFR level code (A1–C2).</param>
/// <param name="Title">Learner-facing Vietnamese name of the level.</param>
public sealed record CefrBand(string Level, string Title);
