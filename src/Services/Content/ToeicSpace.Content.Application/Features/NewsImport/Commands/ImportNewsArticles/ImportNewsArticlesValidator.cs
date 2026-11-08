using ToeicSpace.Content.Application.DTOs.News;
using ToeicSpace.Content.Application.Services;

namespace ToeicSpace.Content.Application.Features.NewsImport.Commands.ImportNewsArticles;

public sealed class ImportNewsArticlesValidator : AbstractValidator<ImportNewsArticlesCommand>
{
    public ImportNewsArticlesValidator()
    {
        RuleFor(x => x.Files).NotNull().WithMessage("At least one file is required.");
        RuleFor(x => x.Files.Count).InclusiveBetween(1, ImportNewsArticlesCommand.MaxFiles)
            .WithMessage($"Upload 1 to {ImportNewsArticlesCommand.MaxFiles} files.").When(x => x.Files is not null);
        RuleForEach(x => x.Files).ChildRules(file =>
        {
            file.RuleFor(x => x).Must(x => NewsImportParser.IsDocument(x) || NewsImportParser.IsCover(x))
                .WithMessage(x => $"{x.FileName}: only .json, .json.gz and .webp files are accepted.");
            file.RuleFor(x => x.SizeBytes).InclusiveBetween(1, NewsImportParser.MaxDocumentBytes)
                .When(NewsImportParser.IsDocument)
                .WithMessage(x => $"{x.FileName}: an articles file must be non-empty and at most 30 MB.");
            file.RuleFor(x => x.SizeBytes).InclusiveBetween(1, NewsImportParser.MaxCoverBytes)
                .When(NewsImportParser.IsCover)
                .WithMessage(x => $"{x.FileName}: a cover must be non-empty and at most 2 MB.");
        }).When(x => x.Files is not null);
    }
}
