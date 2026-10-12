using ToeicSpace.BuildingBlocks.Messaging.Events;

namespace ToeicSpace.Assessment.Application.Questions.Common;

public static class QuestionIntegrationEvents
{
    public static QuestionCreatedIntegrationEvent Created(ToeicQuestion question)
        => new(
            question.Id,
            question.TestId,
            (int)question.Part,
            question.Section.ToString(),
            (int)question.DifficultyLevel,
            question.CreatedAt);

    public static QuestionUpdatedIntegrationEvent Updated(
        ToeicQuestion question,
        DateTime updatedAt)
        => new(
            question.Id,
            question.TestId,
            (int)question.Part,
            question.Section.ToString(),
            (int)question.DifficultyLevel,
            question.Status.ToString(),
            question.Version,
            updatedAt);

    public static QuestionDeletedIntegrationEvent Deleted(ToeicQuestion question)
        => new(
            question.Id,
            question.TestId,
            question.DeletedAt ?? DateTime.UtcNow);
}
