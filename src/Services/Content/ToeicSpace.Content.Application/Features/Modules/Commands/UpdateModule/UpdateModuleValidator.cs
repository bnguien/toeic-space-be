namespace ToeicSpace.Content.Application.Features.Modules.Commands.UpdateModule;

public sealed class UpdateModuleValidator : AbstractValidator<UpdateModuleCommand>
{
    public UpdateModuleValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.ModuleId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
    }
}
