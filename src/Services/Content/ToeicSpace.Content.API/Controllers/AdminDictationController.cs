using System.IO.Compression;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ToeicSpace.Content.Application.DTOs.DictationImport;
using ToeicSpace.Content.Application.Features.DictationImport.Commands.ImportDictationPacks;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.API.Controllers;

/// <summary>Admin import of the pack files from <c>data/dictation</c> (.json or .json.gz).</summary>
[ApiController]
[Route("api/v1/dictation/admin")]
[Authorize(Policy = "Admin")]
public sealed class AdminDictationController(ISender sender) : ControllerBase
{
    private const long MaxRequestBytes =
        ImportDictationPacksCommand.MaxFiles * ImportDictationPacksCommand.MaxFileBytes + 64 * 1024;

    /// <summary>multipart/form-data with one or more pack files (any field name).</summary>
    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
    public async Task<DictationImportResultDto> Import(CancellationToken ct)
    {
        var form = await Request.ReadFormAsync(ct);
        if (form.Files.Count > ImportDictationPacksCommand.MaxFiles)
            throw new ContentRuleException("validation_error", $"Upload 1 to {ImportDictationPacksCommand.MaxFiles} pack files.");
        var files = new List<DictationImportFile>(form.Files.Count);
        foreach (var file in form.Files)
        {
            var content = string.Empty;
            if (file.Length is > 0 and <= ImportDictationPacksCommand.MaxFileBytes)
                content = await ReadAsync(file, ct);
            files.Add(new DictationImportFile(file.FileName, file.Length, content));
        }
        return await sender.Send(new ImportDictationPacksCommand(files), ct);
    }

    /// <summary>Plain or gzip content (detected by the gzip magic bytes), capped once decompressed.</summary>
    private static async Task<string> ReadAsync(IFormFile file, CancellationToken ct)
    {
        await using var input = file.OpenReadStream();
        var head = new byte[2];
        var read = await input.ReadAsync(head, ct);
        input.Position = 0;
        Stream source = read == 2 && head[0] == 0x1f && head[1] == 0x8b
            ? new GZipStream(input, CompressionMode.Decompress)
            : input;
        await using (source)
        {
            using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            int count;
            while ((count = await source.ReadAsync(chunk, ct)) > 0)
            {
                if (buffer.Length + count > ImportDictationPacksCommand.MaxContentBytes)
                    throw DictationErrors.InvalidImportFile(file.FileName, "the decompressed file is larger than 24 MB.");
                buffer.Write(chunk, 0, count);
            }
            return System.Text.Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
        }
    }
}
