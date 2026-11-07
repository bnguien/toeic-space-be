namespace ToeicSpace.Content.Application.Features.VocabularyCatalog.Queries.GetStageSets;

public sealed class GetStageSetsValidator : AbstractValidator<GetStageSetsQuery>
{
    public GetStageSetsValidator() => RuleFor(x => x.StageId).NotEmpty();
}
