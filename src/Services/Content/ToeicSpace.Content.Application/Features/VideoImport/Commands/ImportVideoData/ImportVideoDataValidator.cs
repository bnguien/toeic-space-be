namespace ToeicSpace.Content.Application.Features.VideoImport.Commands.ImportVideoData;

public sealed class ImportVideoDataValidator : AbstractValidator<ImportVideoDataCommand>
{
    public ImportVideoDataValidator()
    {
        RuleFor(x => x.Files).NotEmpty().Must(f => f.Count <= ImportVideoDataCommand.MaxFiles)
            .WithMessage($"Send 1..{ImportVideoDataCommand.MaxFiles} files.");
        RuleForEach(x => x.Files).Must(f => f.Content.Length is > 0 and <= ImportVideoDataCommand.MaxFileBytes)
            .WithMessage("Each file must be 1 byte to 5 MB.");
    }
}
