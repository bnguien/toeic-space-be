using System.Text.Json;
using System.Text.RegularExpressions;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Application.Features.DictationLearner.Commands.UpdateDictationLearnerState;

public sealed partial class UpdateDictationLearnerStateValidator : AbstractValidator<UpdateDictationLearnerStateCommand>
{
    [GeneratedRegex("^[A-Za-z0-9_-]{1,64}$")]
    private static partial Regex StageKey();

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}$")]
    private static partial Regex Day();

    public UpdateDictationLearnerStateValidator()
    {
        RuleFor(x => x.LearnerId).NotEmpty();
        RuleFor(x => x.Stars).Must(x => x!.Count <= DictationLearnerState.MaxStarKeys && x.All(p => StageKey().IsMatch(p.Key)))
            .When(x => x.Stars is not null).WithMessage("Invalid stars map.");
        RuleFor(x => x.DailySeconds).Must(x => x!.Count <= 60 && x.All(p => Day().IsMatch(p.Key)))
            .When(x => x.DailySeconds is not null).WithMessage("Invalid dailySeconds map.");
        RuleFor(x => x.Settings).Must(x => Fits(x!.Basic) && Fits(x.Pro))
            .When(x => x.Settings is not null).WithMessage("Settings must be JSON objects of at most 4000 characters.");
        RuleFor(x => x.SettingsUpdatedAt).NotNull().When(x => x.Settings is not null);
        RuleFor(x => x.Voice).MaximumLength(30);
        RuleFor(x => x.VoiceUpdatedAt).NotNull().When(x => x.Voice is not null);
        RuleFor(x => x.SoundUpdatedAt).NotNull().When(x => x.Sound is not null);
        RuleFor(x => x.Sound!.Tick).NotNull().MaximumLength(20).When(x => x.Sound is not null);
        RuleFor(x => x.Sound!.Chime).NotNull().MaximumLength(20).When(x => x.Sound is not null);
    }

    private static bool Fits(JsonElement? element) =>
        element is null || element.Value.ValueKind == JsonValueKind.Null
        || (element.Value.ValueKind == JsonValueKind.Object
            && element.Value.GetRawText().Length <= DictationLearnerState.MaxSettingsLength);
}
