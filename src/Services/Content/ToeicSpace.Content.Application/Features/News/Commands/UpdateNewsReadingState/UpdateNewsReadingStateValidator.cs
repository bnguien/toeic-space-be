using ToeicSpace.Content.Application.Common.Validation;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Application.Features.News.Commands.UpdateNewsReadingState;

public sealed class UpdateNewsReadingStateValidator : AbstractValidator<UpdateNewsReadingStateCommand>
{
    public UpdateNewsReadingStateValidator()
    {
        RuleFor(x => x.LearnerId).NotEmpty();
        RuleFor(x => x.Slug).NewsSlug();
        RuleFor(x => x.ReadingSeconds).InclusiveBetween(0, NewsRules.MaxReadingSeconds).When(x => x.ReadingSeconds.HasValue);
        RuleFor(x => x.KnownWords).KnownWords();
        RuleFor(x => x.Notes).MaximumLength(NewsReading.NotesMaxLength);
        RuleFor(x => x.Dictation).Dictation();
        RuleFor(x => x.Settings!).SetValidator(new NewsReaderSettingsValidator()).When(x => x.Settings is not null);
    }
}
