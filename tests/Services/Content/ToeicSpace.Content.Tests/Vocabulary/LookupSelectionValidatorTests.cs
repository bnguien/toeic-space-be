using ToeicSpace.Content.Application.Features.Dictionary.Queries.LookupSelection;
using Xunit;

namespace ToeicSpace.Content.Tests.Vocabulary;

/// <summary>"Select to look up" request rules: any latin text up to 600 characters, sentences included.</summary>
public sealed class LookupSelectionValidatorTests
{
    private readonly LookupSelectionValidator _validator = new();

    [Theory]
    [InlineData("negotiate")]
    [InlineData("take into account")]
    [InlineData("The company will review the contract before the meeting next Monday.")]
    public void Accepts_words_phrases_and_sentences(string text) =>
        Assert.True(_validator.Validate(new LookupSelectionQuery(text)).IsValid);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("12345 !!!")]
    [InlineData("東京 — 2026")]
    public void Rejects_empty_or_text_without_latin_letters(string text) =>
        Assert.False(_validator.Validate(new LookupSelectionQuery(text)).IsValid);

    [Fact]
    public void Rejects_text_over_the_selection_cap()
    {
        var text = new string('a', LookupSelectionQuery.MaxTextLength + 1);
        Assert.False(_validator.Validate(new LookupSelectionQuery(text)).IsValid);
    }

    [Fact]
    public void Rejects_a_context_over_the_cap()
    {
        var context = new string('a', LookupSelectionQuery.MaxContextLength + 1);
        Assert.False(_validator.Validate(new LookupSelectionQuery("word", context)).IsValid);
    }
}
