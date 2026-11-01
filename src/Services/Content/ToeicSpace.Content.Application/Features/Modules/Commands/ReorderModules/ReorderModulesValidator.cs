namespace ToeicSpace.Content.Application.Features.Modules.Commands.ReorderModules;

public sealed class ReorderModulesValidator : AbstractValidator<ReorderModulesCommand>
{
    public ReorderModulesValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.ModuleIds).NotNull();
        RuleFor(x => x.ModuleIds).Must(ids => ids is not null && ids.All(x => x != Guid.Empty) &&
            ids.Distinct().Count() == ids.Count).WithMessage("Module IDs must be non-empty and unique.");
    }
}
