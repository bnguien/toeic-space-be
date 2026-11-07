using System.Text.Json;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Rules.Videos;
using ToeicSpace.Content.Domain.ValueObjects;
using Xunit;

namespace ToeicSpace.Content.Tests.Video;

public sealed class VideoRulesTests
{
    [Theory]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/watch?feature=share&v=dQw4w9WgXcQ#t=10", "dQw4w9WgXcQ")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/embed/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/shorts/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/live/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("-abc_DEF123", "-abc_DEF123")]
    [InlineData("https://example.com/page", "")]
    [InlineData("", "")]
    public void Video_id_is_extracted_from_every_link_form(string url, string expected) =>
        Assert.Equal(expected, VideoLinks.ExtractVideoId(url));

    [Fact]
    public void Original_language_maps_to_an_existing_code_preferring_orig()
    {
        var auto = new[] { new VideoLanguage("aa", "Afar"), new VideoLanguage("en", "English"), new VideoLanguage("en-orig", "English (Original)") };
        Assert.Equal("en-orig", SubtitleLanguageRules.PickOriginalLanguage("en-US", [], auto));
        // Unknown original language: an uploaded English track first, never the alphabetical first code ("aa").
        Assert.Equal("en-GB", SubtitleLanguageRules.PickOriginalLanguage("", [new VideoLanguage("en-GB", "English")], auto));
        Assert.Equal("en-orig", SubtitleLanguageRules.PickOriginalLanguage(null, [], auto));
    }

    [Fact]
    public void Has_english_ignores_machine_translated_english_of_other_languages()
    {
        Assert.True(SubtitleLanguageRules.HasEnglish([new VideoLanguage("en", "English")], [], "fr"));
        Assert.True(SubtitleLanguageRules.HasEnglish([], [new VideoLanguage("en-orig", "English")], ""));
        Assert.False(SubtitleLanguageRules.HasEnglish([], [new VideoLanguage("en", "English")], "fr"));
        Assert.True(SubtitleLanguageRules.HasEnglish([], [new VideoLanguage("en", "English")], "en"));
    }

    [Fact]
    public void Permanent_errors_are_told_apart_from_rate_limits()
    {
        Assert.True(SubtitleLanguageRules.IsPermanentError("Video unavailable (removed or private)"));
        Assert.False(SubtitleLanguageRules.IsPermanentError("429 Too Many Requests: video unavailable"));
        Assert.False(SubtitleLanguageRules.IsPermanentError("YouTube timed out"));
    }

    [Fact]
    public void Cues_are_cleaned_like_the_reader()
    {
        var cleaned = TranscriptText.CleanCues([
            new SubtitleCue(0, 1, ">> so we start"),
            new SubtitleCue(1, 1, "- ... right"),
            new SubtitleCue(2, 1, "♪ ♪"),
            new SubtitleCue(3, 1, "&gt;&gt; next one")
        ]);
        Assert.Equal(["so we start", "right", "next one"], cleaned.Select(c => c.Text));
    }

    [Fact]
    public void Cloze_grading_ignores_case_punctuation_and_skips_non_blanks()
    {
        var results = VideoExerciseGrader.GradeCloze("[Music] The Budget is approved, finally.",
            new Dictionary<string, string?> { ["0"] = "music", ["2"] = "budget", ["3"] = "is", ["4"] = "approve", ["5"] = "FINALLY" });
        Assert.False(results.ContainsKey("0"));   // sound token
        Assert.False(results.ContainsKey("3"));   // shorter than 3 letters
        Assert.True(results["2"]);
        Assert.False(results["4"]);
        Assert.True(results["5"]);
    }

    [Fact]
    public void Rewrite_grading_compares_spoken_words_only()
    {
        Assert.True(VideoExerciseGrader.GradeRewrite("(laughs) It's a NEW day!", ["its", "a", "new", "day"]));
        Assert.False(VideoExerciseGrader.GradeRewrite("It's a new day", ["it", "is", "a", "new", "day"]));
        Assert.False(VideoExerciseGrader.GradeRewrite("[Music]", []));
    }

    [Fact]
    public void Translation_blocks_never_shift_lines()
    {
        var lines = Enumerable.Range(1, 25).Select(i => $"line {i}").ToList();
        var blocks = SubtitleTranslationBlocks.Build(lines);
        Assert.Equal(2, blocks.Count);
        Assert.Equal(20, blocks[0].Split(SubtitleTranslationBlocks.Separator).Length);
        var good = string.Join(" ||| ", Enumerable.Range(21, 5).Select(i => $"dòng {i}"));
        var split = SubtitleTranslationBlocks.Split(blocks, ["chỉ một dòng", good]);
        Assert.Equal(25, split.Count);
        Assert.All(split.Take(20), line => Assert.Equal(string.Empty, line));
        Assert.Equal("dòng 21", split[20]);
        Assert.Equal("dòng 25", split[24]);
    }

    [Fact]
    public void Vietnamese_lines_left_in_english_are_detected()
    {
        var en = new[] { new SubtitleCue(0, 2, "we are going to the market today"), new SubtitleCue(2, 2, "OK") };
        var vi = new[] { new SubtitleCue(0, 2, "We are going to the market today"), new SubtitleCue(2, 2, "OK") };
        Assert.Equal([0], ViEnglishLineDetector.Find(vi, en));
        var translated = new[] { new SubtitleCue(0, 2, "hôm nay chúng ta đi chợ"), new SubtitleCue(2, 2, "OK") };
        Assert.Empty(ViEnglishLineDetector.Find(translated, en));
    }

    [Fact]
    public void Word_levels_use_base_forms_and_skip_basic_levels()
    {
        var table = new WordLevelTable(
            new Dictionary<string, string> { ["plan"] = "B2", ["budget"] = "", ["apple"] = "A1", ["negotiate"] = "C1" },
            new HashSet<string>());
        var levels = WordLevelRules.Levels(["planned", "budgets", "apples", "negotiating", "it", "café"], table);
        Assert.Equal("B2", levels["planned"]);
        Assert.Equal("TOEIC", levels["budgets"]);
        Assert.Equal("C1", levels["negotiating"]);
        Assert.False(levels.ContainsKey("apples"));
        Assert.False(levels.ContainsKey("it"));
        Assert.False(levels.ContainsKey("café"));
    }

    [Fact]
    public void Level_estimate_follows_the_channel_and_duration()
    {
        var estimate = VideoLevelGrader.Estimate("a2", 300);
        Assert.Equal(("A2", VideoTrack.Easy, 22), (estimate.Cefr, estimate.Track, estimate.Difficulty));
        Assert.Equal(VideoTrack.Medium, VideoLevelGrader.Estimate("", 60).Track);
        Assert.Equal("C1", VideoLevelGrader.CefrForScore(70));
        Assert.Equal(VideoTrack.Hard, VideoLevelGrader.TrackOf("C2"));
    }

    [Fact]
    public void Subtitle_measurements_count_words_and_speech_rate()
    {
        var cues = Enumerable.Range(0, 60)
            .Select(i => new SubtitleCue(i * 3, 3, "we negotiate the budget every single quarter"))
            .Append(new SubtitleCue(200, 2, "[Music]")).ToList();
        var table = new WordLevelTable(new Dictionary<string, string> { ["negotiate"] = "C1", ["budget"] = "B2" },
            new HashSet<string> { "every", "single", "quarter" });
        var m = VideoLevelGrader.Measure(cues, table);
        Assert.Equal(420, m.Words);
        Assert.Equal(180, m.SpeechSeconds);
        Assert.Equal(140, m.WordsPerMinute);
        Assert.True(m.LexicalScore > 0);
        var level = VideoLevelGrader.Grade(m, 180, "B1");
        Assert.InRange(level.Difficulty, 0, 100);
    }

    [Fact]
    public void Path_interleaves_topics_inside_a_band_shortest_first()
    {
        // Band of 6 points: news (first video 300 s) and talk (200 s) interleave, the shorter topic first.
        var rows = new[]
        {
            (Id: "x1", Topic: "news", Difficulty: 6, Duration: 300),
            (Id: "x2", Topic: "news", Difficulty: 7, Duration: 100),
            (Id: "y1", Topic: "talk", Difficulty: 8, Duration: 200),
            (Id: "z1", Topic: "talk", Difficulty: 40, Duration: 50)
        };
        var ordered = CatalogPathOrdering.Order(rows, r => r.Difficulty, r => r.Duration, r => r.Id, r => r.Topic);
        Assert.Equal(["y1", "x1", "x2", "z1"], ordered.Select(r => r.Id));
    }

    [Fact]
    public void Watch_progress_percent_only_grows_and_completes_at_90()
    {
        var watch = new VideoWatch { Duration = 200 };
        var now = new DateTime(2026, 11, 7, 0, 0, 0, DateTimeKind.Utc);
        WatchProgressRules.Apply(watch, 120, 200, now);
        Assert.Equal((120, 60), (watch.LastPosition, watch.WatchedPercent));
        WatchProgressRules.Apply(watch, 30, 0, now);
        Assert.Equal((30, 60), (watch.LastPosition, watch.WatchedPercent));
        Assert.Null(watch.CompletedAt);
        WatchProgressRules.Apply(watch, 500, 200, now);
        Assert.Equal((200, 100), (watch.LastPosition, watch.WatchedPercent));
        Assert.Equal(now, watch.CompletedAt);
    }

    [Fact]
    public void Cue_json_is_a_compact_array_with_optional_offsets()
    {
        var cues = JsonSerializer.Deserialize<List<SubtitleCue>>("[[1.5,2,\"hi there\",[0,0.4]],[4,1,\"bye\"]]")!;
        Assert.Equal([0, 0.4], cues[0].Offsets!);
        Assert.Null(cues[1].Offsets);
        Assert.Equal("[[1.5,2,\"hi there\",[0,0.4]],[4,1,\"bye\"]]", JsonSerializer.Serialize(cues));
    }

    [Fact]
    public void Word_card_helpers_filter_related_words()
    {
        Assert.Equal("approve", WordDetailRules.NormalizeWord(" Approve "));
        Assert.Equal(string.Empty, WordDetailRules.NormalizeWord("two words"));
        var list = new List<string>();
        WordDetailRules.AddUnique(list, "ER", "emergency");
        WordDetailRules.AddUnique(list, "crisis", "emergency");
        WordDetailRules.AddUnique(list, "crisis", "emergency");
        WordDetailRules.AddUnique(list, "emergency", "emergency");
        Assert.Equal(["crisis"], list);
        Assert.Equal("develop", WordDetailRules.Stem("development"));
        Assert.Equal("make", WordDetailRules.Stem("make"));
    }
}
