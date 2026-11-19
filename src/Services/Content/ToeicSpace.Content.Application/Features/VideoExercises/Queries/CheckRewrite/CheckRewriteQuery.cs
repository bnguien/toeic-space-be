using System.Text.Json.Serialization;
using ToeicSpace.Content.Application.DTOs.Videos;

namespace ToeicSpace.Content.Application.Features.VideoExercises.Queries.CheckRewrite;

/// <summary>Checks a rewritten sentence: the typed words against the spoken words of the cue.</summary>
public sealed record CheckRewriteQuery(string Lang, int Cue, IReadOnlyList<string?> Words,
    [property: JsonIgnore] string VideoId = "", [property: JsonIgnore] Guid LearnerId = default)
    : IRequest<RewriteCheckResultDto>;
