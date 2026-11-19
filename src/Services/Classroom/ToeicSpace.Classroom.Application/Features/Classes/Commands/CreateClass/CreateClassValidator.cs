namespace ToeicSpace.Classroom.Application.Features.Classes.Commands.CreateClass;

public sealed class CreateClassValidator : AbstractValidator<CreateClassCommand>
{
    public CreateClassValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x).Must(x => x.StartDate is null || x.EndDate is null || x.StartDate <= x.EndDate)
            .WithMessage("StartDate cannot exceed EndDate.");
    }
}
