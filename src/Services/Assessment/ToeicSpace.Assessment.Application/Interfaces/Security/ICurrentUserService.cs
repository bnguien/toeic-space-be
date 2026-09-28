namespace ToeicSpace.Assessment.Application.Interfaces.Security;

public interface ICurrentUserService
{
    Guid? UserId { get; }

    bool IsAuthenticated { get; }

    /// <summary>
    /// True for roles allowed to manage the question bank (Admin, Teacher).
    /// Content managers can see drafts and answer keys.
    /// </summary>
    bool CanManageContent { get; }
}
