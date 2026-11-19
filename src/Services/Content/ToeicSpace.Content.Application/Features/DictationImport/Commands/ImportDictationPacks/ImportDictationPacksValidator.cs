namespace ToeicSpace.Content.Application.Features.DictationImport.Commands.ImportDictationPacks;

public sealed class ImportDictationPacksValidator : AbstractValidator<ImportDictationPacksCommand>
{
    public ImportDictationPacksValidator()
    {
        RuleFor(x => x.Files).NotNull().WithMessage("At least one pack file is required.");
        RuleFor(x => x.Files.Count).InclusiveBetween(1, ImportDictationPacksCommand.MaxFiles)
            .WithMessage($"Upload 1 to {ImportDictationPacksCommand.MaxFiles} pack files.")
            .When(x => x.Files is not null);
        RuleForEach(x => x.Files).ChildRules(file =>
        {
            file.RuleFor(x => x.FileName).NotEmpty()
                .Must(x => x.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                    || x.EndsWith(".json.gz", StringComparison.OrdinalIgnoreCase))
                .WithMessage("Only .json and .json.gz pack files are accepted.");
            file.RuleFor(x => x.SizeBytes).InclusiveBetween(1, ImportDictationPacksCommand.MaxFileBytes)
                .WithMessage("Each pack file must be non-empty and at most 10 MB.");
            file.RuleFor(x => x.Content).NotEmpty().WithMessage("Pack file is empty or could not be decompressed.");
        }).When(x => x.Files is not null);
    }
}
