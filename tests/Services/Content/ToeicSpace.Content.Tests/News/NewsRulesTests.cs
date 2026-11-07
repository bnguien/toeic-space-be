using ToeicSpace.Content.Application.Models.NewsImport;
using ToeicSpace.Content.Application.Services;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Exceptions;
using ToeicSpace.Content.Domain.Rules;
using Xunit;

namespace ToeicSpace.Content.Tests.News;

public sealed class DictationGraderTests
{
    [Fact]
    public void Exact_text_is_correct_ignoring_case_punctuation_and_curly_apostrophes()
    {
        var grade = NewsDictationGrader.Grade("its a four day week,  isn't it", "It’s a four-day week, isn’t it?");
        Assert.False(grade.Correct); // "its" ≠ "it's"
        var exact = NewsDictationGrader.Grade("it's a FOUR-DAY week isn't it", "It’s a four-day week, isn’t it?");
        Assert.True(exact.Correct);
        Assert.Equal((6, 6, 0), (exact.CorrectCount, exact.Total, exact.Extra));
        Assert.Equal(["It’s", "a", "four-day", "week,", "isn’t", "it?"], exact.Words.Select(x => x.Text));
    }

    [Fact]
    public void Missing_wrong_and_extra_words_are_marked_in_place()
    {
        var grade = NewsDictationGrader.Grade("the cat sat really on mat", "The cat sat on the mat.");
        Assert.False(grade.Correct);
        Assert.Equal([true, true, true, true, false, true], grade.Words.Select(x => x.Ok));
        Assert.Equal((5, 6, 1), (grade.CorrectCount, grade.Total, grade.Extra));
        Assert.Equal("mat.", grade.Words[^1].Text);
    }

    [Fact]
    public void Match_key_drops_punctuation_and_hyphens()
    {
        Assert.Equal("fourday", NewsDictationGrader.MatchKey("Four-day,"));
        Assert.Equal("it's", NewsDictationGrader.MatchKey("It’s"));
        Assert.Equal("quoted", NewsDictationGrader.MatchKey("'quoted'"));
        Assert.Equal(string.Empty, NewsDictationGrader.MatchKey("—"));
    }

    [Fact]
    public void Empty_answer_or_input_is_never_correct()
    {
        Assert.False(NewsDictationGrader.Grade("", "").Correct);
        var grade = NewsDictationGrader.Grade("", "Hello world");
        Assert.Equal((false, 0, 2, 0), (grade.Correct, grade.CorrectCount, grade.Total, grade.Extra));
    }
}

public sealed class NewsCatalogTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(100, 1)]
    [InlineData(400, 2)] // 2.5 rounds to even
    [InlineData(480, 3)]
    [InlineData(560, 4)] // 3.5 rounds to even
    [InlineData(972, 6)]
    public void Minutes_use_160_words_per_minute_rounded_to_even(int words, int minutes) =>
        Assert.Equal(minutes, NewsCatalog.MinutesFor(words));

    [Fact]
    public void Keys_are_english()
    {
        Assert.Equal(["c1", "c2", "c3", "c4", "c5"], NewsCatalog.Stages);
        Assert.Contains("business", NewsCatalog.Topics);
        Assert.Equal(["short", "medium", "long"], NewsCatalog.Lengths);
        Assert.Equal(3, NewsCatalog.CountWords("  one two\tthree "));
    }
}

public sealed class NewsImportParserTests
{
    private static NewsArticleFileItem Item(string status = "published") => new()
    {
        Slug = "voa-1",
        Status = status,
        Source = new NewsArticleFileSource
        {
            Name = "VOA Learning English", Url = "https://learningenglish.voanews.com/a/1.html", License = "public_domain",
            Attribution = "VOA", PublishedAt = new DateTimeOffset(2025, 1, 2, 3, 4, 5, TimeSpan.Zero)
        },
        TitleEn = "  A   title ",
        TitleVi = "Một tiêu đề",
        Stage = "c3",
        Topic = "education",
        Paragraphs =
        [
            new() { Sentences = [new() { Text = "First one.", Translation = "Một." }, new() { Text = " ", Translation = "" }, new() { Text = "Third.", Translation = "Ba." }] },
            new() { Sentences = [new() { Text = "", Translation = "" }] },
            new() { Sentences = [new() { Text = "Next paragraph here.", Translation = "Đoạn sau." }] }
        ],
        Vocabulary =
        [
            new() { Word = "title", PartOfSpeech = "n", Meaning = "tiêu đề", Level = "easy" },
            new() { Word = "Title", Meaning = "trùng" },
            new() { Word = "paragraph", Level = "de" }
        ],
        Cover = new NewsArticleFileCover
        {
            File = "0123456789abcdef0123456789abcdef.webp", License = "cc_by", Credit = "Ảnh: A · Wikimedia Commons · CC BY 4.0"
        }
    };

    [Fact]
    public void Normalizes_text_ids_counts_and_vocabulary()
    {
        var article = NewsImportParser.Normalize(Item(), "articles.json");
        Assert.Equal("A title", article.TitleEn);
        Assert.Equal(["p1", "p3"], article.Paragraphs.Select(x => x.Id));
        Assert.Equal(["p1s1", "p1s3", "p3s1"], article.Sentences.Select(x => x.Id));
        Assert.Equal((6, 1), (article.WordCount, article.Minutes));
        Assert.Equal(["title", "paragraph"], article.Vocabulary.Select(x => x.Word));
        Assert.Equal(["easy", "medium"], article.Vocabulary.Select(x => x.Level));
        Assert.Equal(NewsArticleStatus.Published, article.Status);
        Assert.Equal("0123456789abcdef0123456789abcdef.webp", article.CoverFile);
        Assert.Equal(new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc), article.PublishedAt);
    }

    [Fact]
    public void News_agency_cover_is_dropped_and_then_the_article_cannot_be_published()
    {
        var item = Item("review");
        item.Cover!.Credit = "Photo: AP";
        var article = NewsImportParser.Normalize(item, "articles.json");
        Assert.Null(article.CoverFile);
        Assert.Equal(NewsArticleStatus.Review, article.Status);

        var published = Item();
        published.Cover!.Credit = "Reuters";
        var error = Assert.Throws<ContentRuleException>(() => NewsImportParser.Normalize(published, "articles.json"));
        Assert.Equal("invalid_import_file", error.Code);
    }

    [Fact]
    public void Untranslated_article_is_never_published_and_a_draft_becomes_review_when_translated()
    {
        var item = Item();
        item.Paragraphs![0].Sentences![0].Translation = "";
        Assert.Throws<ContentRuleException>(() => NewsImportParser.Normalize(item, "a.json"));
        Assert.Equal(NewsArticleStatus.Review, NewsImportParser.Normalize(Item("draft"), "a.json").Status);
        Assert.Equal(NewsArticleStatus.Hidden, NewsImportParser.Normalize(Item("hidden"), "a.json").Status);
    }

    [Theory]
    [InlineData("license")]
    [InlineData("topic")]
    [InlineData("url")]
    [InlineData("slug")]
    [InlineData("status")]
    public void Invalid_fields_are_rejected(string field)
    {
        var item = Item();
        switch (field)
        {
            case "license": item.Source!.License = "all_rights_reserved"; break;
            case "topic": item.Topic = "kinh-doanh"; break;
            case "url": item.Source!.Url = "http://insecure"; break;
            case "slug": item.Slug = "Bad Slug"; break;
            case "status": item.Status = "7"; break;
        }
        Assert.Equal("invalid_import_file",
            Assert.Throws<ContentRuleException>(() => NewsImportParser.Normalize(item, "a.json")).Code);
    }

    [Fact]
    public void Cover_files_must_be_webp_with_a_hash_name()
    {
        Assert.True(NewsImportParser.IsCoverName("0123456789abcdef.webp"));
        Assert.False(NewsImportParser.IsCoverName("../x.webp"));
        Assert.False(NewsImportParser.IsCoverName("ABCDEF0123456789.webp"));
        Assert.True(NewsImportParser.IsWebp(System.Text.Encoding.ASCII.GetBytes("RIFF\0\0\0\0WEBPVP8 ")));
        Assert.False(NewsImportParser.IsWebp(System.Text.Encoding.ASCII.GetBytes("PNG\r\n\0\0\0\0\0\0\0\0")));
    }
}

public sealed class NewsReadingTests
{
    private static readonly DateTime T0 = new(2026, 11, 7, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Reading_clock_only_grows_and_completion_keeps_the_first_time()
    {
        var reading = NewsReading.Open(Guid.NewGuid(), Guid.NewGuid(), T0);
        reading.AddReadingTime(90, T0);
        reading.AddReadingTime(30, T0);
        Assert.Equal(90, reading.ReadingSeconds);
        reading.Complete(T0.AddMinutes(1));
        reading.Complete(T0.AddMinutes(5));
        Assert.Equal(T0.AddMinutes(1), reading.CompletedAt);
    }

    [Fact]
    public void Dictation_passed_stays_passed_and_unaided_means_no_reveal_first()
    {
        var reading = NewsReading.Open(Guid.NewGuid(), Guid.NewGuid(), T0);
        reading.RecordDictationAttempt("p1s1", "wrong", correct: false, revealed: false, T0);
        reading.RecordDictationAttempt("p1s1", "right", correct: true, revealed: false, T0);
        reading.RecordDictationAttempt("p1s1", "again", correct: false, revealed: false, T0);
        var first = reading.Dictation["p1s1"];
        Assert.Equal((true, true, 3, "again"), (first.Passed, first.Unaided, first.Attempts, first.Input));

        reading.RecordDictationAttempt("p1s2", "x", correct: false, revealed: true, T0);
        reading.RecordDictationAttempt("p1s2", "y", correct: true, revealed: false, T0);
        Assert.Equal((true, false, true), (reading.Dictation["p1s2"].Passed, reading.Dictation["p1s2"].Unaided,
            reading.Dictation["p1s2"].Revealed));
    }

    [Fact]
    public void Merged_dictation_only_moves_forward()
    {
        var reading = NewsReading.Open(Guid.NewGuid(), Guid.NewGuid(), T0);
        reading.RecordDictationAttempt("p1s1", "right", correct: true, revealed: false, T0);
        reading.MergeDictation("p1s1", new NewsDictationEntry { Passed = false, Attempts = 0 }, T0);
        Assert.True(reading.Dictation["p1s1"].Passed);
        reading.MergeDictation("p2s1", new NewsDictationEntry { Passed = true, Unaided = true, Attempts = 2, Input = "x" }, T0);
        Assert.Equal((true, true, 2), (reading.Dictation["p2s1"].Passed, reading.Dictation["p2s1"].Unaided,
            reading.Dictation["p2s1"].Attempts));
        reading.ResetDictation(T0);
        Assert.Empty(reading.Dictation);
    }

    [Fact]
    public void Known_words_are_distinct_and_capped()
    {
        var reading = NewsReading.Open(Guid.NewGuid(), Guid.NewGuid(), T0);
        reading.SetKnownWords(Enumerable.Range(0, 50).Select(i => $"w{i % 45}"), T0);
        Assert.Equal(NewsReading.MaxKnownWords, reading.KnownWords.Count);
        reading.SetNotes("", T0);
        Assert.Null(reading.Notes);
    }
}
