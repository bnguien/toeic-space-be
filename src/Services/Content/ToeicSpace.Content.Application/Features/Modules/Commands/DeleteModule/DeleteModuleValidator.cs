namespace ToeicSpace.Content.Application.Features.Modules.Commands.DeleteModule;

public sealed class DeleteModuleValidator : AbstractValidator<DeleteModuleCommand>
{
    public DeleteModuleValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.ModuleId).NotEmpty();
    }
}
