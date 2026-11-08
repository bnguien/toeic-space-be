using ToeicSpace.Content.Tests.Vocabulary;
using Xunit;

namespace ToeicSpace.Content.Tests;

/// <summary>
/// A test over a real game dataset (<c>data/news</c>, <c>data/grammar</c>, …). Those folders are gitignored
/// (content pulled from Studychill stays out of the repository), so the test is skipped where the file is absent —
/// CI — and runs on a machine that produced the data with <c>scripts/*_data_pipeline.py</c>.
/// </summary>
public sealed class RequiresDataFactAttribute : FactAttribute
{
    public RequiresDataFactAttribute(string relativePath)
    {
        if (!File.Exists(VocabularyTestContext.RepositoryFile(relativePath)))
            Skip = $"{relativePath} is not committed; run the data pipeline to enable this test.";
    }
}
