using ToeicSpace.Content.Application.DTOs.Videos;

namespace ToeicSpace.Content.Application.Features.VideoCatalog.Queries.GetCatalogPath;

/// <summary>Listening path of one track (easy | medium | hard) ordered easy → hard, plus totals per track.</summary>
public sealed record GetCatalogPathQuery(string? Track) : IRequest<CatalogPathDto>;
