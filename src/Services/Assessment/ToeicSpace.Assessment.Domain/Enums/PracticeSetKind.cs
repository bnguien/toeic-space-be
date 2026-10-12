namespace ToeicSpace.Assessment.Domain.Enums;

public enum PracticeSetKind
{
    /// <summary>
    /// Practice grouped by target score level (e.g. Part 1 Lv.1 450+).
    /// </summary>
    Level = 1,

    /// <summary>
    /// Practice grouped by question type or scenario (e.g. Part 2 "When" questions).
    /// </summary>
    Topic = 2,

    /// <summary>
    /// Mixed drill for a single part (e.g. Part 5 - 200 questions).
    /// </summary>
    PartDrill = 3
}
