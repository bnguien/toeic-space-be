using System.Text.Json;
using ToeicSpace.Content.Application.Mappings;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Application.Features.VideoLearnerStates.Commands.UpdateVideoLearnerState;

public sealed class UpdateVideoLearnerStateValidator : AbstractValidator<UpdateVideoLearnerStateCommand>
{
    public UpdateVideoLearnerStateValidator()
    {
        RuleFor(x => x.LearnerId).NotEmpty();
        RuleFor(x => x.ReaderSettings)
            .Must(s => s is null || s.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Null)
            .WithMessage("readerSettings must be an object.")
            .Must(s => s is null || s.Value.GetRawText().Length <= VideoLearnerState.ReaderSettingsMaxLength)
            .WithMessage($"readerSettings must be at most {VideoLearnerState.ReaderSettingsMaxLength} characters.");
        RuleFor(x => x.ListenMode)
            .Must(m => m is null || m.Length == 0 || UpdateVideoLearnerStateCommand.ListenModes.Contains(m.Trim().ToLowerInvariant()))
            .WithMessage("listenMode must be sentence, flow, cloze or rewrite.");
        RuleFor(x => x.PathTrack)
            .Must(t => t is null || t.Length == 0 || VideoMappingExtensions.TrackKeys.Contains(t.Trim().ToLowerInvariant()))
            .WithMessage("pathTrack must be easy, medium or hard.");
    }
}
