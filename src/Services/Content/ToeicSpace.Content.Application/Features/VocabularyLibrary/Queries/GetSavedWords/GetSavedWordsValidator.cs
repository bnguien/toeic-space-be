using ToeicSpace.Content.Application.Common.Validation;

namespace ToeicSpace.Content.Application.Features.VocabularyLibrary.Queries.GetSavedWords;

public sealed class GetSavedWordsValidator : AbstractValidator<GetSavedWordsQuery>
{
    public GetSavedWordsValidator()
    {
        RuleFor(x => x.LearnerId).NotEmpty();
        RuleFor(x => x).Must(x => !(x.FolderId.HasValue && x.Unfiled == true))
            .WithMessage("Use either folderId or unfiled=true, not both.");
        RuleFor(x => x.FolderId).NotEqual(Guid.Empty).When(x => x.FolderId.HasValue);
        RuleFor(x => x.Search).MaximumLength(100);
        RuleFor(x => x.Page).InclusiveBetween(1, 100_000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, GetSavedWordsQuery.MaxPageSize);
        RuleFor(x => x.TzOffset).ValidTzOffset();
    }
}
