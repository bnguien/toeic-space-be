using ToeicSpace.Content.Application.Common.Validation;

namespace ToeicSpace.Content.Application.Features.VocabularyProgress.Queries.GetStudyQueue;

public sealed class GetStudyQueueValidator : AbstractValidator<GetStudyQueueQuery>
{
    public GetStudyQueueValidator()
    {
        RuleFor(x => x.LearnerId).NotEmpty();
        RuleFor(x => x.SourceCount).Equal(1)
            .WithMessage("Specify exactly one of setId, due=true, folderId, library=true or mistakes=true.");
        RuleFor(x => x.SetId).NotEqual(Guid.Empty).When(x => x.SetId.HasValue);
        RuleFor(x => x.FolderId).NotEqual(Guid.Empty).When(x => x.FolderId.HasValue);
        RuleFor(x => x.Limit).InclusiveBetween(1, GetStudyQueueQuery.MaxLimit);
        RuleFor(x => x.TzOffset).ValidTzOffset();
    }
}
