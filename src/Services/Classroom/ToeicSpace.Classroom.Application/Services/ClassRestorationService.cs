using ToeicSpace.Classroom.Domain.Entities;
using ToeicSpace.Classroom.Domain.Enums;
using ToeicSpace.Classroom.Domain.Exceptions;

namespace ToeicSpace.Classroom.Application.Services;

public sealed class ClassRestorationService(ClassActivationService activation)
{
    public async Task<ClassStatus> GetOriginalStatusAsync(Class entity, CancellationToken ct)
    {
        if (entity.ArchivedFromStatus is not (ClassStatus.Draft or ClassStatus.Active or ClassStatus.Completed))
            throw new ClassRuleException("restore_state_unknown", "The status before archiving is unknown; restore requires manual resolution.", 409);

        var original = entity.ArchivedFromStatus.Value;
        if (original == ClassStatus.Active)
            await activation.EnsureCanActivateAsync(entity, ct);
        return original;
    }
}
