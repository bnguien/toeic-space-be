namespace ToeicSpace.Content.Application.Features.Modules.Commands.CreateModule;

public sealed class CreateModuleValidator : AbstractValidator<CreateModuleCommand>
{
    public CreateModuleValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.SortOrder).GreaterThan(0).When(x => x.SortOrder.HasValue);
    }
}
