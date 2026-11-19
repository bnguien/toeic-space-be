using System.Text.Json.Serialization;
using ToeicSpace.Content.Application.DTOs.Videos;

namespace ToeicSpace.Content.Application.Features.VideoExercises.Queries.CheckCloze;

/// <summary>Grades the blanks of one sentence: <c>answers</c> = token index → typed text ("" = revealed).</summary>
public sealed record CheckClozeQuery(string Lang, int Cue, IReadOnlyDictionary<string, string?> Answers,
    [property: JsonIgnore] string VideoId = "", [property: JsonIgnore] Guid LearnerId = default)
    : IRequest<ClozeCheckResultDto>;
