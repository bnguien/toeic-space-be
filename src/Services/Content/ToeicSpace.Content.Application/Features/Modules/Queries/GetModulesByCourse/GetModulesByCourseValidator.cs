namespace ToeicSpace.Content.Application.Features.Modules.Queries.GetModulesByCourse;

public sealed class GetModulesByCourseValidator : AbstractValidator<GetModulesByCourseQuery>
{
    public GetModulesByCourseValidator() => RuleFor(x => x.CourseId).NotEmpty();
}
