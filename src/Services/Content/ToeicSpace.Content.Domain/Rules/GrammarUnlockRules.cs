namespace ToeicSpace.Content.Domain.Rules;

/// <summary>Stage-test unlock and roadmap statistics rules of the grammar roadmap (pure).</summary>
public static class GrammarUnlockRules
{
    /// <summary>Parts per topic counted by the progress percentage: theory, vocabulary, mini test passed.</summary>
    public const int PartsPerTopic = 3;

    /// <summary>Topics to complete to unlock a stage test: 80% of the stage, rounded up (9 topics → 8).</summary>
    public static int RequiredTopics(int total) => (total * 4 + 4) / 5;

    /// <summary>Unlocked when the stage has topics and completed × 5 ≥ total × 4.</summary>
    public static bool IsUnlocked(int completed, int total) => total > 0 && completed * 5 >= total * 4;

    /// <summary>Progress percentage (floor) of confirmed parts over topics × 3.</summary>
    public static int ProgressPercent(int partsDone, int topicCount) =>
        topicCount > 0 ? partsDone * 100 / (topicCount * PartsPerTopic) : 0;

    /// <summary>A topic is completed once theory and vocabulary are confirmed and the mini test is passed.</summary>
    public static bool IsTopicCompleted(bool theoryDone, bool vocabDone, bool miniTestPassed) =>
        theoryDone && vocabDone && miniTestPassed;
}
