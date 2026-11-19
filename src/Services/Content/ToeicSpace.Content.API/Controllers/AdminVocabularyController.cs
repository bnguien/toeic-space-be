using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ToeicSpace.Content.Application.DTOs.VocabularyImport;
using ToeicSpace.Content.Application.Features.VocabularyImport.Commands.ImportVocabularyStages;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.API.Controllers;

/// <summary>Admin import of the roadmap stage files from <c>data/vocabulary</c>.</summary>
[ApiController]
[Route("api/v1/admin/vocabulary")]
[Authorize(Policy = "Admin")]
public sealed class AdminVocabularyController(ISender sender) : ControllerBase
{
    private const long MaxRequestBytes =
        ImportVocabularyStagesCommand.MaxFiles * ImportVocabularyStagesCommand.MaxFileBytes + 64 * 1024;

    /// <summary>multipart/form-data with one or more stage files (field <c>files</c> or <c>files[]</c>).</summary>
    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
    public async Task<VocabularyImportResultDto> Import(CancellationToken ct)
    {
        var form = await Request.ReadFormAsync(ct);
        if (form.Files.Count > ImportVocabularyStagesCommand.MaxFiles)
            throw new ContentRuleException("validation_error",
                $"Upload 1 to {ImportVocabularyStagesCommand.MaxFiles} stage files.");
        var files = new List<VocabularyImportFile>(form.Files.Count);
        foreach (var file in form.Files)
        {
            var content = string.Empty;
            if (file.Length is > 0 and <= ImportVocabularyStagesCommand.MaxFileBytes)
            {
                using var reader = new StreamReader(file.OpenReadStream());
                content = await reader.ReadToEndAsync(ct);
            }
            files.Add(new VocabularyImportFile(file.FileName, file.ContentType, file.Length, content));
        }
        return await sender.Send(new ImportVocabularyStagesCommand(files), ct);
    }
}
