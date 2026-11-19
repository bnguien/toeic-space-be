namespace ToeicSpace.Classroom.Application.Features.Classes.Commands.RestoreDeletedClass;

public sealed class RestoreDeletedClassValidator : AbstractValidator<RestoreDeletedClassCommand>
{
    public RestoreDeletedClassValidator() => RuleFor(x => x.ClassId).NotEmpty();
}
