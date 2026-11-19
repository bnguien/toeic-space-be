using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ToeicSpace.Content.Application.DTOs.News;
using ToeicSpace.Content.Application.Features.NewsImport.Commands.ImportNewsArticles;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.API.Controllers;

/// <summary>Admin import of <c>data/news</c>: articles documents and cover images.</summary>
[ApiController]
[Route("api/v1/news/admin")]
[Authorize(Policy = "Admin")]
public sealed class AdminNewsController(ISender sender) : ControllerBase
{
    /// <summary>
    /// multipart/form-data with any mix of <c>articles*.json</c>, <c>articles*.json.gz</c> and <c>&lt;hash&gt;.webp</c>
    /// files (any field name). Through the gateway keep each request under 2 MB (see docs/news.md).
    /// </summary>
    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(ImportNewsArticlesCommand.MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = ImportNewsArticlesCommand.MaxRequestBytes,
        ValueCountLimit = ImportNewsArticlesCommand.MaxFiles + 16)]
    public async Task<NewsImportResultDto> Import(CancellationToken ct)
    {
        var form = await Request.ReadFormAsync(ct);
        if (form.Files.Count > ImportNewsArticlesCommand.MaxFiles)
            throw new ContentRuleException("validation_error",
                $"Upload 1 to {ImportNewsArticlesCommand.MaxFiles} files.");
        var files = new List<NewsImportFile>(form.Files.Count);
        foreach (var file in form.Files)
        {
            var content = Array.Empty<byte>();
            if (file.Length is > 0 and <= ImportNewsArticlesCommand.MaxRequestBytes)
            {
                using var buffer = new MemoryStream((int)file.Length);
                await file.CopyToAsync(buffer, ct);
                content = buffer.ToArray();
            }
            files.Add(new NewsImportFile(Path.GetFileName(file.FileName), file.ContentType, file.Length, content));
        }
        return await sender.Send(new ImportNewsArticlesCommand(files), ct);
    }
}
