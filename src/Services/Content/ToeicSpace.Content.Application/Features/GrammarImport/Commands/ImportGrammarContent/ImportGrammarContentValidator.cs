namespace ToeicSpace.Content.Application.Features.GrammarImport.Commands.ImportGrammarContent;

public sealed class ImportGrammarContentValidator : AbstractValidator<ImportGrammarContentCommand>
{
    public ImportGrammarContentValidator()
    {
        RuleFor(x => x.Files).NotNull().Must(f => f.Count is >= 1 and <= ImportGrammarContentCommand.MaxFiles)
            .WithMessage($"Upload 1 to {ImportGrammarContentCommand.MaxFiles} grammar bundle files.");
        RuleForEach(x => x.Files).ChildRules(file =>
        {
            file.RuleFor(x => x.FileName).Must(n => n.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                    || n.EndsWith(".json.gz", StringComparison.OrdinalIgnoreCase))
                .WithMessage("Only .json or .json.gz bundle files are accepted.");
            file.RuleFor(x => x.SizeBytes).InclusiveBetween(1, ImportGrammarContentCommand.MaxFileBytes)
                .WithMessage("Each bundle file must be non-empty and at most 10 MB.");
        });
    }
}
