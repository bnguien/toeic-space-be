using System.Text.Json.Serialization;
using ToeicSpace.Content.Application.DTOs.Dictation;

namespace ToeicSpace.Content.Application.Features.Dictation.Commands.CheckDictationAnswer;

/// <summary>
/// Body of <c>POST check</c>. <paramref name="Assisted"/> = the learner opened keywords / meaning before
/// submitting (self-declared, as in Studychill); any hint also counts. <paramref name="DryRun"/> = live colouring
/// while typing: only a correct dry run is saved. The learner id comes from the token (empty for guests).
/// </summary>
public sealed record CheckDictationAnswerCommand(int ItemId, string? UserText, string? Voice = null,
    int HintCount = 0, bool Assisted = false, bool DryRun = false, [property: JsonIgnore] Guid? LearnerId = null)
    : IRequest<DictationCheckDto>;
