namespace ToeicSpace.Content.Application.Features.VocabularyImport.Commands.ImportVocabularyStages;

public sealed class ImportVocabularyStagesValidator : AbstractValidator<ImportVocabularyStagesCommand>
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
        { "application/json", "text/json", "text/plain", "application/octet-stream" };

    public ImportVocabularyStagesValidator()
    {
        RuleFor(x => x.Files).NotNull().WithMessage("At least one stage file is required.");
        RuleFor(x => x.Files.Count).InclusiveBetween(1, ImportVocabularyStagesCommand.MaxFiles)
            .WithMessage($"Upload 1 to {ImportVocabularyStagesCommand.MaxFiles} stage files.")
            .When(x => x.Files is not null);
        RuleForEach(x => x.Files).ChildRules(file =>
        {
            file.RuleFor(x => x.FileName).NotEmpty()
                .Must(x => x.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                .WithMessage("Only .json stage files are accepted.");
            file.RuleFor(x => x.ContentType)
                .Must(x => string.IsNullOrEmpty(x) || AllowedContentTypes.Contains(x.Split(';')[0].Trim()))
                .WithMessage("Stage files must be JSON.");
            file.RuleFor(x => x.SizeBytes).InclusiveBetween(1, ImportVocabularyStagesCommand.MaxFileBytes)
                .WithMessage("Each stage file must be non-empty and at most 5 MB.");
            file.RuleFor(x => x.Content).NotEmpty().WithMessage("Stage file is empty.");
        }).When(x => x.Files is not null);
    }
}
