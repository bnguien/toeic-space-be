namespace ToeicSpace.Assessment.Domain.Enums;

/// <summary>
/// Which questions of a practice set a new session contains.
/// </summary>
public enum PracticeSelection
{
    /// <summary>Every question of the set, in set order.</summary>
    All = 0,

    /// <summary>Questions the learner has never answered, in set order.</summary>
    Continue = 1,

    /// <summary>Random questions, never-answered first.</summary>
    Random = 2,

    /// <summary>Questions whose latest graded answer is wrong.</summary>
    Mistakes = 3
}
