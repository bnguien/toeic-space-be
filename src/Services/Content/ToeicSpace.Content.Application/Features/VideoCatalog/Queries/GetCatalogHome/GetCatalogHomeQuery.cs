using ToeicSpace.Content.Application.DTOs.Videos;

namespace ToeicSpace.Content.Application.Features.VideoCatalog.Queries.GetCatalogHome;

/// <summary>Every topic with its first page and per-track counts (public, cached 5 minutes).</summary>
public sealed record GetCatalogHomeQuery : IRequest<CatalogHomeDto>;
