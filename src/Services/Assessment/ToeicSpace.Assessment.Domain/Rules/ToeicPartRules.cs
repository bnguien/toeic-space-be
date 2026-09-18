using ToeicSpace.Assessment.Domain.Enums;

namespace ToeicSpace.Assessment.Domain.Rules;

/// <summary>
/// Business rules of the standard ETS TOEIC Listening &amp; Reading format.
/// </summary>
public static class ToeicPartRules
{
    public const int StandardTestQuestionCount = 200;

    public const int StandardSectionQuestionCount = 100;

    public const int StandardTestDurationMinutes = 120;

    private static readonly IReadOnlyDictionary<ToeicPart, (int First, int Last)> StandardQuestionNumberRanges =
        new Dictionary<ToeicPart, (int First, int Last)>
        {
            [ToeicPart.Part1] = (1, 6),
            [ToeicPart.Part2] = (7, 31),
            [ToeicPart.Part3] = (32, 70),
            [ToeicPart.Part4] = (71, 100),
            [ToeicPart.Part5] = (101, 130),
            [ToeicPart.Part6] = (131, 146),
            [ToeicPart.Part7] = (147, 200)
        };

    public static ToeicSection GetSection(ToeicPart part)
        => part <= ToeicPart.Part4 ? ToeicSection.Listening : ToeicSection.Reading;

    /// <summary>
    /// Part 2 (Question-Response) has only three answer choices; all other parts have four.
    /// </summary>
    public static int GetOptionCount(ToeicPart part)
        => part == ToeicPart.Part2 ? 3 : 4;

    public static bool HasOptionD(ToeicPart part)
        => GetOptionCount(part) == 4;

    public static bool IsValidAnswer(ToeicPart part, AnswerOption answer)
        => Enum.IsDefined(answer) && (int)answer <= GetOptionCount(part);

    /// <summary>
    /// Part 3, 4, 6 and 7 questions always belong to a group (conversation, talk or text).
    /// </summary>
    public static bool RequiresPassage(ToeicPart part)
        => part is ToeicPart.Part3 or ToeicPart.Part4 or ToeicPart.Part6 or ToeicPart.Part7;

    /// <summary>
    /// Part 1, 2 and 5 questions are standalone and never belong to a group.
    /// </summary>
    public static bool AllowsPassage(ToeicPart part)
        => RequiresPassage(part);

    /// <summary>
    /// Part 1 (Photographs) questions must have a photo.
    /// </summary>
    public static bool RequiresImage(ToeicPart part)
        => part == ToeicPart.Part1;

    public static (int First, int Last) GetStandardQuestionNumberRange(ToeicPart part)
        => StandardQuestionNumberRanges[part];

    public static bool IsInStandardQuestionNumberRange(ToeicPart part, int questionNumber)
    {
        var (first, last) = GetStandardQuestionNumberRange(part);

        return questionNumber >= first && questionNumber <= last;
    }

    public static int GetStandardQuestionCount(ToeicPart part)
    {
        var (first, last) = GetStandardQuestionNumberRange(part);

        return last - first + 1;
    }
}
