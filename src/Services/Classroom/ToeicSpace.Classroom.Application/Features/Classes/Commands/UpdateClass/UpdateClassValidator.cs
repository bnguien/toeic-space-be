namespace ToeicSpace.Classroom.Application.Features.Classes.Commands.UpdateClass;

public sealed class UpdateClassValidator : AbstractValidator<UpdateClassCommand>
{
    public UpdateClassValidator()
    {
        RuleFor(x => x.ClassId).NotEmpty();
        RuleFor(x => x)
            .Must(x => x.HasCode || x.HasName || x.HasDescription || x.HasStartDate || x.HasEndDate)
            .WithMessage("At least one field must be supplied.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200).When(x => x.HasName);
        RuleFor(x => x.Description).MaximumLength(2000).When(x => x.HasDescription && x.Description is not null);
    }
}