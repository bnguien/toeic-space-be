using ToeicSpace.Content.Application.Common.Validation;

namespace ToeicSpace.Content.Application.Features.VocabularyLibrary.Queries.GetLibrary;

public sealed class GetLibraryValidator : AbstractValidator<GetLibraryQuery>
{
    public GetLibraryValidator()
    {
        RuleFor(x => x.LearnerId).NotEmpty();
        RuleFor(x => x.TzOffset).ValidTzOffset();
    }
}
