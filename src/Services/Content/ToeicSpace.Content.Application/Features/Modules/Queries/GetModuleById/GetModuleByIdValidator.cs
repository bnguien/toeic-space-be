namespace ToeicSpace.Content.Application.Features.Modules.Queries.GetModuleById;

public sealed class GetModuleByIdValidator : AbstractValidator<GetModuleByIdQuery>
{
    public GetModuleByIdValidator()
    {
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.ModuleId).NotEmpty();
    }
}
