using System.Text.Json.Serialization;
using ToeicSpace.Content.Application.DTOs.News;

namespace ToeicSpace.Content.Application.Features.News.Commands.CheckNewsDictation;

/// <summary>
/// Grades one dictation sentence ("Hack não · Nghe chép") on the server, where the answer is. Guests are graded
/// only; a signed-in learner's attempt is recorded. <c>Revealed</c> = the learner looked at the answer first.
/// </summary>
public sealed record CheckNewsDictationCommand(string Slug, string SentenceId, string Text, bool Revealed = false,
    [property: JsonIgnore] Guid LearnerId = default) : IRequest<DictationResultDto>;
