namespace ToeicSpace.Assessment.Domain.Entities;

public class ToeicPracticeSetItem
{
    public Guid PracticeSetId { get; set; }

    public ToeicPracticeSet PracticeSet { get; set; } = null!;

    public Guid QuestionId { get; set; }

    public ToeicQuestion Question { get; set; } = null!;

    public int OrderIndex { get; set; }
}
