using ToeicSpace.Assessment.Application.Interfaces.Security;

namespace ToeicSpace.Assessment.Application.Practice.Common;

public static class PracticeSetAuthorization
{
    public static void EnsureCanCreate(ICurrentUserService user)
    {
        if (!user.IsAuthenticated || !user.CanManageContent || !user.UserId.HasValue)
        {
            throw AppException.Forbidden("Only authenticated Teachers and Admins can manage practice sets.");
        }
    }

    public static void EnsureCanManage(ToeicPracticeSet practiceSet, ICurrentUserService user)
    {
        EnsureCanCreate(user);

        if (!user.IsAdmin && practiceSet.CreatedByUserId != user.UserId)
        {
            throw AppException.Forbidden("Teachers can only manage practice sets they created.");
        }
    }
}
