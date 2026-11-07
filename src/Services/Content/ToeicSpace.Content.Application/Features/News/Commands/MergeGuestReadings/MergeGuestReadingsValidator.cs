using ToeicSpace.Content.Application.Common.Validation;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Application.Features.News.Commands.MergeGuestReadings;

public sealed class MergeGuestReadingsValidator : AbstractValidator<MergeGuestReadingsCommand>
{
    public MergeGuestReadingsValidator()
    {
        RuleFor(x => x.LearnerId).NotEmpty();
        RuleFor(x => x.Items).NotNull();
        RuleFor(x => x.Items.Count).InclusiveBetween(1, MergeGuestReadingsCommand.MaxItems)
            .WithMessage($"Send 1 to {MergeGuestReadingsCommand.MaxItems} articles.").When(x => x.Items is not null);
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(x => x.Slug).NewsSlug();
            item.RuleFor(x => x.ReadingSeconds).InclusiveBetween(0, NewsRules.MaxReadingSeconds);
            item.RuleFor(x => x.KnownWords).KnownWords();
            item.RuleFor(x => x.Notes).MaximumLength(NewsReading.NotesMaxLength);
            item.RuleFor(x => x.Dictation).Dictation();
        }).When(x => x.Items is not null);
    }
}
