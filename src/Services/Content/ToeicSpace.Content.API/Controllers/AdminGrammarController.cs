using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ToeicSpace.Content.Application.DTOs.Grammar;
using ToeicSpace.Content.Application.Features.GrammarImport.Commands.ImportGrammarContent;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.API.Controllers;

/// <summary>Admin import of the grammar bundle built by scripts/grammar_data_pipeline.py (data/grammar).</summary>
[ApiController]
[Route("api/v1/grammar/admin")]
[Authorize(Policy = "Admin")]
public sealed class AdminGrammarController(ISender sender) : ControllerBase
{
    private const long MaxRequestBytes =
        ImportGrammarContentCommand.MaxFiles * ImportGrammarContentCommand.MaxFileBytes + 64 * 1024;

    /// <summary>multipart/form-data with one or more bundle files (.json or .json.gz, any field name).</summary>
    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
    public async Task<GrammarImportResultDto> Import(CancellationToken ct)
    {
        var form = await Request.ReadFormAsync(ct);
        if (form.Files.Count > ImportGrammarContentCommand.MaxFiles)
            throw new ContentRuleException("validation_error",
                $"Upload 1 to {ImportGrammarContentCommand.MaxFiles} grammar bundle files.");
        var files = new List<GrammarImportFile>(form.Files.Count);
        foreach (var file in form.Files)
        {
            var content = Array.Empty<byte>();
            if (file.Length is > 0 and <= ImportGrammarContentCommand.MaxFileBytes)
            {
                using var buffer = new MemoryStream();
                await file.CopyToAsync(buffer, ct);
                content = buffer.ToArray();
            }
            files.Add(new GrammarImportFile(file.FileName, file.ContentType, file.Length, content));
        }
        return await sender.Send(new ImportGrammarContentCommand(files), ct);
    }
}
