namespace ToeicSpace.Classroom.Application.Features.Classes.Queries.GetClassById;

public sealed class GetClassByIdValidator : AbstractValidator<GetClassByIdQuery>
{
    public GetClassByIdValidator() => RuleFor(x => x.ClassId).NotEmpty();
}
