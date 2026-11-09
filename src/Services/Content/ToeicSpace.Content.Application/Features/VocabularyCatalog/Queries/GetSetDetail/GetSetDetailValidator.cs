namespace ToeicSpace.Content.Application.Features.VocabularyCatalog.Queries.GetSetDetail;

public sealed class GetSetDetailValidator : AbstractValidator<GetSetDetailQuery>
{
    public GetSetDetailValidator() => RuleFor(x => x.SetId).NotEmpty();
}
