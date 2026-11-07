namespace ToeicSpace.Content.Application.Features.VideoCatalog.Queries.GetCatalogTopicPage;

public sealed class GetCatalogTopicPageValidator : AbstractValidator<GetCatalogTopicPageQuery>
{
    public GetCatalogTopicPageValidator()
    {
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(40);
    }
}
