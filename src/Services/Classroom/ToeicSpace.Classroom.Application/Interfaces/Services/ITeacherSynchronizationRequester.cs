namespace ToeicSpace.Classroom.Application.Interfaces.Services;

public interface ITeacherSynchronizationRequester
{
    Task RequestAsync(CancellationToken cancellationToken);
}
