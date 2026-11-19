using ToeicSpace.Content.Application.Services.Import;

namespace ToeicSpace.Content.Application.Features.LibraryImport.Commands.ParseImport;

public sealed class ParseImportValidator : AbstractValidator<ParseImportCommand>
{
    public ParseImportValidator()
    {
        RuleFor(x => x.LearnerId).NotEmpty();
        RuleFor(x => x).Must(x => string.IsNullOrWhiteSpace(x.Text) != string.IsNullOrWhiteSpace(x.Json))
            .WithMessage("Send either text or json.");
        RuleFor(x => x.Text).MaximumLength(VocabularyTextParser.MaxTextLength);
        RuleFor(x => x.Json).MaximumLength(ParseImportCommand.MaxJsonLength);
    }
}
