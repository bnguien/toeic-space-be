namespace ToeicSpace.Identity.Application.Features.Teachers.Queries;

public sealed class GetTeachersValidator : AbstractValidator<GetTeachersQuery>
{
    public GetTeachersValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
