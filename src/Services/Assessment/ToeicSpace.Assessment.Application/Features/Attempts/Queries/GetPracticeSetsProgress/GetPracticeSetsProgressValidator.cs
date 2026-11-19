namespace ToeicSpace.Assessment.Application.Features.Attempts.Queries.GetPracticeSetsProgress;

public sealed class GetPracticeSetsProgressValidator : AbstractValidator<GetPracticeSetsProgressQuery>
{
    public GetPracticeSetsProgressValidator()
    {
        RuleFor(query => query.Part).InclusiveBetween(1, 7).When(query => query.Part.HasValue);
    }
}
