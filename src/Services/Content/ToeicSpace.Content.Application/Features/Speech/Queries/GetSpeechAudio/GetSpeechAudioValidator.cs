using ToeicSpace.Content.Domain.Rules;

namespace ToeicSpace.Content.Application.Features.Speech.Queries.GetSpeechAudio;

public sealed class GetSpeechAudioValidator : AbstractValidator<GetSpeechAudioQuery>
{
    public GetSpeechAudioValidator()
    {
        RuleFor(x => SpeechVoices.CleanText(x.Text)).NotEmpty().WithMessage("text is required.")
            .MaximumLength(SpeechVoices.MaxTextLength)
            .WithMessage($"text must be at most {SpeechVoices.MaxTextLength} characters.");
        RuleFor(x => x.Voice).MaximumLength(30);
    }
}
