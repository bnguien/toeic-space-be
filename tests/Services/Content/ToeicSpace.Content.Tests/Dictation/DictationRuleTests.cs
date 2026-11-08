using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Rules;
using ToeicSpace.Content.Infrastructure.Services.Speech;
using Xunit;

namespace ToeicSpace.Content.Tests.Dictation;

public sealed class DictationGraderTests
{
    [Theory]
    [InlineData("the cat sat on the mat", "the cat sit on mat", "Equal:0-2:0-2|Replace:2-3:2-3|Equal:3-4:3-4|Delete:4-5:4-4|Equal:5-6:4-5")]
    [InlineData("a b c d", "b c d e", "Delete:0-1:0-0|Equal:1-4:0-3|Insert:4-4:3-4")]
    [InlineData("one two three four five", "one three two four six seven",
        "Equal:0-1:0-1|Insert:1-1:1-2|Equal:1-2:2-3|Delete:2-3:3-3|Equal:3-4:3-4|Replace:4-5:4-6")]
    [InlineData("x y", "", "Delete:0-2:0-0")]
    public void Opcodes_match_python_difflib(string a, string b, string expected)
    {
        var opcodes = SequenceMatcher.Opcodes(a.Split(' ', StringSplitOptions.RemoveEmptyEntries),
            b.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        Assert.Equal(expected, string.Join('|', opcodes.Select(o => $"{o.Tag}:{o.I1}-{o.I2}:{o.J1}-{o.J2}")));
    }

    [Fact]
    public void Tokens_skip_words_without_letters_so_every_index_matches_the_skeleton()
    {
        const string text = "Hello – world, \"don't\" go!";
        Assert.Equal(["Hello", "world,", "\"don't\"", "go!"], DictationGrader.Tokens(text));
        var skeleton = DictationGrader.Skeleton(text);
        Assert.Equal([new SkeletonWord("", 5, ""), new SkeletonWord("", 5, ","), new SkeletonWord("\"", 5, "\""),
            new SkeletonWord("", 2, "!")], skeleton);
        Assert.Equal(4, WordProof.Build(text, new byte[16], 1000).Digests.Count);
    }

    [Theory]
    [InlineData("Café,", "cafe")]
    [InlineData("\"Don't", "don't")]
    [InlineData("well-known.", "well-known")]
    [InlineData("–", "")]
    public void Normalize_strips_outer_punctuation_case_and_diacritics(string token, string expected) =>
        Assert.Equal(expected, DictationGrader.Normalize(token));

    [Fact]
    public void A_sentence_passes_only_when_every_word_is_correct()
    {
        var target = string.Join(' ', Enumerable.Range(1, 20).Select(i => $"word{i}"));
        var almost = string.Join(' ', Enumerable.Range(1, 19).Select(i => $"word{i}")) + " wrong";
        var result = DictationGrader.Grade(almost, target);
        Assert.Equal((95.0, false, 19, 20), (result.Accuracy, result.Passed, result.CorrectCount, result.TotalCount));
        var exact = DictationGrader.Grade("THE bananas, are sold", "The bananas are sold.");
        Assert.True(exact.Passed);
        Assert.Equal(100, exact.Accuracy);
    }

    [Fact]
    public void Grade_reports_wrong_missing_and_extra_words_like_studychill()
    {
        var result = DictationGrader.Grade("the cat sit on mat now", "The cat sat on the mat");
        Assert.Equal(
            [("The", DictationWordStatus.Correct, null), ("cat", DictationWordStatus.Correct, null),
                ("sat", DictationWordStatus.Wrong, "sit"), ("on", DictationWordStatus.Correct, null),
                ("the", DictationWordStatus.Missing, null), ("mat", DictationWordStatus.Correct, null),
                ("now", DictationWordStatus.Extra, (string?)null)],
            result.Words.Select(w => (w.Text, w.Status, w.Typed)));
        Assert.Equal(66.7, result.Accuracy);
    }

    [Fact]
    public void Clean_user_text_trims_and_caps_the_input()
    {
        Assert.Equal("abc", DictationGrader.CleanUserText("  abc  "));
        Assert.Equal(DictationGrader.MaxUserText, DictationGrader.CleanUserText(new string('a', 5000)).Length);
        Assert.Equal(string.Empty, DictationGrader.CleanUserText(null));
    }
}

public sealed class DictationHintTests
{
    [Fact]
    public void Hint_points_at_the_first_wrong_word_in_skeleton_indexes()
    {
        const string text = "Hello – big world.";
        var first = DictationHints.For("hello", text, DictationHintMode.FirstLetter)!;
        Assert.Equal((1, 3, "b", 3), (first.Index, first.Total, first.Letter, first.Length));
        var whole = DictationHints.For("hello big", text, DictationHintMode.RevealWord)!;
        Assert.Equal((2, "world.", 5), (whole.Index, whole.Word, whole.Length));
    }

    [Fact]
    public void A_complete_answer_hints_the_last_word() =>
        Assert.Equal(1, DictationHints.For("good day", "Good day.", DictationHintMode.FirstLetter)!.Index);
}

public sealed class DictationRulesTests
{
    [Theory]
    [InlineData(1, true, false, true)]
    [InlineData(2, true, false, true)]
    [InlineData(3, true, false, false)]
    [InlineData(3, true, true, true)]
    [InlineData(5, false, false, false)]
    public void Unlock_needs_a_clean_pass_from_level_three(int level, bool passed, bool clean, bool expected) =>
        Assert.Equal(expected, DictationRules.PassesUnlock(level, passed, clean));

    [Theory]
    [InlineData(true, 100, 10)]
    [InlineData(false, 85, 8)]
    [InlineData(false, 95, 10)]
    [InlineData(false, 66.7, 7)]
    public void Score_is_ten_on_a_pass_else_accuracy_over_ten_half_even(bool passed, double accuracy, int expected) =>
        Assert.Equal(expected, DictationRules.Score(passed, accuracy));

    [Fact]
    public void Stage_stars_follow_the_average_best_accuracy()
    {
        Assert.Equal(3, DictationRules.StageStars([100, 90]));
        Assert.Equal(2, DictationRules.StageStars([90, 80]));
        Assert.Equal(1, DictationRules.StageStars([50]));
        Assert.Equal(1, DictationRules.StageStars([]));
    }

    [Fact]
    public void Progress_keeps_its_best_values()
    {
        var now = new DateTime(2026, 11, 7, 0, 0, 0, DateTimeKind.Utc);
        var progress = new DictationProgress();
        progress.Record(100, 10, passed: true, assisted: true, now);
        progress.Record(40, 4, passed: false, assisted: false, now);
        Assert.Equal((100.0, 10, true, false, 2), (progress.BestAccuracy, progress.BestScore, progress.Passed,
            progress.PassedClean, progress.AttemptCount));
        progress.Record(100, 10, passed: true, assisted: false, now);
        Assert.True(progress.PassedClean);
    }
}

public sealed class WordProofTests
{
    // Expected values from Python: base64(hashlib.pbkdf2_hmac("sha256", word, salt + index.to_bytes(2, "big"), 20000, 16)).
    [Theory]
    [InlineData("hello", 0, "pYl/djWZHIhGMIgKVP14lQ==")]
    [InlineData("don't", 3, "La4ykZi87g49/Flssu885w==")]
    public void Digest_matches_studychill_pbkdf2(string word, int index, string expected)
    {
        var salt = Enumerable.Range(0, 16).Select(i => (byte)i).ToArray();
        Assert.Equal(expected, Convert.ToBase64String(WordProof.Digest(word, salt, index, 20_000)));
    }

    [Fact]
    public void Salt_depends_on_the_sentence_and_its_last_update()
    {
        var secret = "secret"u8.ToArray();
        var at = new DateTime(2026, 11, 7, 1, 2, 3, DateTimeKind.Utc);
        Assert.Equal(16, WordProof.Salt(secret, 1, at).Length);
        Assert.NotEqual(WordProof.Salt(secret, 1, at), WordProof.Salt(secret, 2, at));
        Assert.NotEqual(WordProof.Salt(secret, 1, at), WordProof.Salt(secret, 1, at.AddSeconds(1)));
    }
}

public sealed class SpeechTests
{
    // Expected values from edge-tts 7.3.1 drm.generate_sec_ms_gec_v2(unix_timestamp).
    [Theory]
    [InlineData(0, "52BC7A17B50DFEC9-28C4394775C8751CD2F6859C030C06DB1489ACAB3B3A7651831FC26895177ED6")]
    [InlineData(1700000000, "2CD23A2CA0B2595A-F3C846C370E05F9CC83ACC5F995C5F5D87A4D0F14CE8936A0F902E64099E38A2")]
    [InlineData(1762500000.5, "713C2BF431375CEC-2322C6B3E513D50276E09641CE873C8914A93295C8DDA6121A6CBE99EDD41784")]
    [InlineData(1799999999, "8DD724B1AF1AEBDA-7EAB32704E1EBDC4C7D8109CC130E2B31826441D7C07CA6E8118FBCE8902837E")]
    public void Sec_ms_gec_token_matches_edge_tts(double unixSeconds, string expected) =>
        Assert.Equal(expected, SecMsGecToken.Generate(unixSeconds));

    [Theory]
    [InlineData(null, 0)]
    [InlineData(-100.0, -40)]
    [InlineData(12.4, 10)]
    [InlineData(12.5, 15)]
    [InlineData(-12.5, -10)]
    [InlineData(double.NaN, 0)]
    public void Rate_is_clamped_and_snapped_to_five(double? rate, int expected) =>
        Assert.Equal(expected, SpeechVoices.ClampRate(rate));

    [Fact]
    public void Voices_resolve_to_the_catalog_or_the_default()
    {
        Assert.Equal("en-GB-SoniaNeural", SpeechVoices.Resolve("sonia").VoiceName);
        Assert.Equal("andrew", SpeechVoices.Resolve("nobody").Key);
        Assert.Equal("+0%", SpeechVoices.RateString(0));
        Assert.Equal("-25%", SpeechVoices.RateString(-25));
        Assert.Equal("Microsoft Server Speech Text to Speech Voice (en-US, AndrewNeural)",
            EdgeTtsClient.FullVoiceName("en-US-AndrewNeural"));
        Assert.Equal("a &amp; b &lt;c&gt; don't", EdgeTtsClient.EscapeXml("a & b <c> don't"));
        Assert.Equal("a b", EdgeTtsClient.RemoveIncompatibleCharacters("a\vb"));
    }
}

public sealed class DictationLearnerStateTests
{
    [Fact]
    public void Stars_and_seconds_merge_by_max_and_old_days_are_dropped()
    {
        var state = new DictationLearnerState();
        Assert.True(state.MergeStars(new Dictionary<string, int> { ["a"] = 2, ["b"] = 0 }));
        Assert.False(state.MergeStars(new Dictionary<string, int> { ["a"] = 1 }));
        Assert.True(state.MergeStars(new Dictionary<string, int> { ["a"] = 5 }));
        Assert.Equal(new Dictionary<string, int> { ["a"] = 3 }, state.Stars);

        var days = Enumerable.Range(1, 20).ToDictionary(d => $"2026-10-{d:00}", d => d * 10);
        Assert.True(state.MergeDailySeconds(days));
        Assert.Equal(DictationLearnerState.KeepDays, state.DailySeconds.Count);
        Assert.Equal("2026-10-07", state.DailySeconds.Keys.Order(StringComparer.Ordinal).First());
        Assert.False(state.MergeDailySeconds(new Dictionary<string, int> { ["2026-10-20"] = 5 }));
    }

    [Fact]
    public void Settings_voice_and_sound_keep_the_newer_write()
    {
        var state = new DictationLearnerState();
        Assert.True(state.ApplyVoice("sonia", 200));
        Assert.False(state.ApplyVoice("ryan", 100));
        Assert.Equal("sonia", state.Voice);
        Assert.True(state.ApplySettings("{\"pace\":\"chill\"}", null, 50));
        Assert.False(state.ApplySettings(null, null, 50));
        Assert.True(state.ApplySound(new DictationSoundPreferences(true, 0.5, "wood", "bell"), 1));
    }
}
