namespace ToeicSpace.Classroom.Application.Features.Classes.Commands.SetDefaultRoom;

public sealed class SetDefaultRoomValidator : AbstractValidator<SetDefaultRoomCommand>
{
    public SetDefaultRoomValidator()
    {
        RuleFor(x => x.ClassId).NotEmpty();
        RuleFor(x => x.AdminId).NotEmpty();
        RuleFor(x => x.RoomId).Must(id => id != Guid.Empty);
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}
