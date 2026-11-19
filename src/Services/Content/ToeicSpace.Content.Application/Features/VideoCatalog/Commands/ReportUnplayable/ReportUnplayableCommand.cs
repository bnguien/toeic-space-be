using ToeicSpace.Content.Application.DTOs.Videos;

namespace ToeicSpace.Content.Application.Features.VideoCatalog.Commands.ReportUnplayable;

/// <summary>The embedded player failed with an IFrame error code; the server re-checks with YouTube before hiding.</summary>
public sealed record ReportUnplayableCommand(string VideoId, int Code) : IRequest<UnplayableResultDto>;
