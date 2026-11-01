using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Application.Features.VocabularyPreferences.Commands.UpdatePreferences;

public sealed class UpdatePreferencesValidator : AbstractValidator<UpdatePreferencesCommand>
{
    public UpdatePreferencesValidator()
    {
        RuleFor(x => x.LearnerId).NotEmpty();
        RuleFor(x => x.FontScale.Value).Must(x => LearnerVocabularyPreferences.FontScales.Contains(x))
            .WithName("fontScale").WithMessage("fontScale must be 0.875, 1, 1.125 or 1.25.").When(x => x.FontScale.IsSet);
        RuleFor(x => x.DailyGoal.Value)
            .InclusiveBetween(LearnerVocabularyPreferences.MinDailyGoal, LearnerVocabularyPreferences.MaxDailyGoal)
            .WithName("dailyGoal").When(x => x.DailyGoal.IsSet);
        RuleFor(x => x.SidebarWidth.Value)
            .InclusiveBetween(LearnerVocabularyPreferences.MinSidebarWidth, LearnerVocabularyPreferences.MaxSidebarWidth)
            .WithName("sidebarWidth").When(x => x.SidebarWidth.IsSet && x.SidebarWidth.Value.HasValue);
        RuleFor(x => x.DefaultMode.Value).IsInEnum().WithName("defaultMode").When(x => x.DefaultMode.IsSet);
        RuleFor(x => x.LastView.Value).IsInEnum().WithName("lastView").When(x => x.LastView.IsSet);
        RuleFor(x => x.LastStageId.Value).NotEqual(Guid.Empty).WithName("lastStageId")
            .When(x => x.LastStageId.IsSet && x.LastStageId.Value.HasValue);
        RuleFor(x => x.LastFolderId.Value).NotEqual(Guid.Empty).WithName("lastFolderId")
            .When(x => x.LastFolderId.IsSet && x.LastFolderId.Value.HasValue);
        RuleFor(x => x.LastFolderPickerId.Value).NotEqual(Guid.Empty).WithName("lastFolderPickerId")
            .When(x => x.LastFolderPickerId.IsSet && x.LastFolderPickerId.Value.HasValue);
    }
}
