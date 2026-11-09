using ToeicSpace.Content.Application.DTOs.Videos;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Interfaces.Services.Video;
using ToeicSpace.Content.Application.Services.Video;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Rules.Videos;

namespace ToeicSpace.Content.Application.Features.VideoCatalog.Commands.ReportUnplayable;

/// <summary>
/// Studychill <c>catalog_unplayable_report</c>: not a catalog video or a browser-side code (2, 5, 153) → not hidden
/// without asking YouTube; codes 100/101/150 → oEmbed + watch page probe (cached 6 h, 10 min when inconclusive);
/// inconclusive never hides.
/// </summary>
public sealed class ReportUnplayableHandler(IVideoCatalogRepository catalog, IVideoEmbedProbe probe, IVideoCache cache,
    CatalogVideoStatusService status) : IRequestHandler<ReportUnplayableCommand, UnplayableResultDto>
{
    public static readonly TimeSpan ProbeTtl = TimeSpan.FromHours(6);
    public static readonly TimeSpan UnknownProbeTtl = TimeSpan.FromMinutes(10);

    public async Task<UnplayableResultDto> Handle(ReportUnplayableCommand request, CancellationToken ct)
    {
        var item = await catalog.GetForUpdateAsync(request.VideoId, ct);
        if (item is null) return new UnplayableResultDto(false);
        if (item.EmbedStatus == VideoEmbedStatus.Blocked) return new UnplayableResultDto(true);
        if (!EmbedPlayabilityRules.UnplayableCodes.Contains(request.Code)) return new UnplayableResultDto(false);

        var key = $"video:embed:{request.VideoId}";
        if (!cache.TryGet<EmbedCheck>(key, out var check) || check is null)
        {
            check = await probe.ProbeAsync(request.VideoId, ct);
            cache.Set(key, check, check.Status == VideoEmbedStatus.Unknown ? UnknownProbeTtl : ProbeTtl);
        }
        if (check.Status == VideoEmbedStatus.Unknown) return new UnplayableResultDto(false);
        await status.MarkEmbedAsync(request.VideoId, check, ct);
        return new UnplayableResultDto(check.Status == VideoEmbedStatus.Blocked);
    }
}
