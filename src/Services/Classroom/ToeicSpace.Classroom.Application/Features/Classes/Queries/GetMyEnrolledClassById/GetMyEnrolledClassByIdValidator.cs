namespace ToeicSpace.Classroom.Application.Features.Classes.Queries.GetMyEnrolledClassById;

public sealed class GetMyEnrolledClassByIdValidator : AbstractValidator<GetMyEnrolledClassByIdQuery>
{
    public GetMyEnrolledClassByIdValidator()
    {
        RuleFor(x => x.StudentId).NotEmpty();
        RuleFor(x => x.ClassId).NotEmpty();
    }
}
