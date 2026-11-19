namespace ToeicSpace.Content.Application.Features.ReelBackgrounds.Queries.GetReelBackgrounds;

public sealed class GetReelBackgroundsValidator : AbstractValidator<GetReelBackgroundsQuery>
{
    public GetReelBackgroundsValidator() =>
        RuleFor(x => x.Count).InclusiveBetween(1, GetReelBackgroundsQuery.MaxCount);
}
