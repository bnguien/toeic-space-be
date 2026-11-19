using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ToeicSpace.Content.Application.DTOs.Videos;
using ToeicSpace.Content.Application.Features.VideoImport.Commands.ImportVideoData;
using ToeicSpace.Content.Application.Models.Videos;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.API.Controllers;

/// <summary>Admin import of <c>data/videos</c> (catalog.json, subtitles-NN.json.gz).</summary>
[ApiController]
[Route("api/v1/videos/admin")]
[Authorize(Policy = "Admin")]
public sealed class AdminVideosController(ISender sender) : ControllerBase
{
    private const long MaxRequestBytes = (long)ImportVideoDataCommand.MaxFiles * ImportVideoDataCommand.MaxFileBytes + 64 * 1024;

    /// <summary>multipart/form-data with one or more data files (any field name; .json or .json.gz).</summary>
    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
    public async Task<VideoImportResultDto> Import(CancellationToken ct)
    {
        var form = await Request.ReadFormAsync(ct);
        if (form.Files.Count is 0 or > ImportVideoDataCommand.MaxFiles)
            throw new ContentRuleException("validation_error", $"Upload 1 to {ImportVideoDataCommand.MaxFiles} data files.");
        var files = new List<VideoImportFile>(form.Files.Count);
        foreach (var file in form.Files)
        {
            if (file.Length is <= 0 or > ImportVideoDataCommand.MaxFileBytes)
                throw VideoErrors.InvalidImportFile(file.FileName, "each file must be 1 byte to 5 MB.");
            using var buffer = new MemoryStream();
            await file.CopyToAsync(buffer, ct);
            files.Add(new VideoImportFile(file.FileName, buffer.ToArray()));
        }
        return await sender.Send(new ImportVideoDataCommand(files), ct);
    }
}
