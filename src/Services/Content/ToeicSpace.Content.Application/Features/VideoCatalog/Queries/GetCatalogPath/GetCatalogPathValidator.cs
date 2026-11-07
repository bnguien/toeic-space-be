using ToeicSpace.Content.Application.Mappings;

namespace ToeicSpace.Content.Application.Features.VideoCatalog.Queries.GetCatalogPath;

public sealed class GetCatalogPathValidator : AbstractValidator<GetCatalogPathQuery>
{
    public GetCatalogPathValidator()
    {
        RuleFor(x => x.Track)
            .Must(t => string.IsNullOrWhiteSpace(t) || VideoMappingExtensions.TrackKeys.Contains(t.Trim().ToLowerInvariant()))
            .WithMessage("track must be easy, medium or hard.");
    }
}
