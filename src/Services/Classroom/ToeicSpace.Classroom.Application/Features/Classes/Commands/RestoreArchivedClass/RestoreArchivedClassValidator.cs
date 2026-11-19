namespace ToeicSpace.Classroom.Application.Features.Classes.Commands.RestoreArchivedClass;

public sealed class RestoreArchivedClassValidator : AbstractValidator<RestoreArchivedClassCommand>
{
    public RestoreArchivedClassValidator() => RuleFor(x => x.ClassId).NotEmpty();
}
