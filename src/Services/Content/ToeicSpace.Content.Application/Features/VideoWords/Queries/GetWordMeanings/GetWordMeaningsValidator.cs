namespace ToeicSpace.Content.Application.Features.VideoWords.Queries.GetWordMeanings;

public sealed class GetWordMeaningsValidator : AbstractValidator<GetWordMeaningsQuery>
{
    public GetWordMeaningsValidator()
    {
        RuleFor(x => x.WordList).NotEmpty().WithMessage("Pass w=<word> or words=a,b,c.");
    }
}
