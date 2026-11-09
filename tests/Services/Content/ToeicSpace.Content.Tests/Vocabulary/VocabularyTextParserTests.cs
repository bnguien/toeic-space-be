using ToeicSpace.Content.Application.Services.Import;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Exceptions;
using Xunit;

namespace ToeicSpace.Content.Tests.Vocabulary;

public sealed class VocabularyTextParserTests
{
    [Fact]
    public void Lines_split_on_the_first_separator_in_contract_order()
    {
        var parsed = VocabularyTextParser.ParseText(string.Join("\n",
            "apple\tquả táo",
            "1. budget => ngân sách",
            "2) invoice -> hóa đơn",
            "- deadline = hạn chót",
            "* agenda : chương trình",
            "• memo - bản ghi nhớ",
            "✓ meeting – cuộc họp",
            "> client — khách hàng",
            "co-worker - đồng nghiệp",
            "a = b => c"));
        Assert.Equal(
            [("apple", "quả táo"), ("budget", "ngân sách"), ("invoice", "hóa đơn"), ("deadline", "hạn chót"),
             ("agenda", "chương trình"), ("memo", "bản ghi nhớ"), ("meeting", "cuộc họp"), ("client", "khách hàng"),
             ("co-worker", "đồng nghiệp"), ("a = b", "c")],
            parsed.Items.Select(x => (x.Headword, x.MeaningVi!)));
        Assert.Equal([1, 2, 3, 4, 5, 6, 7, 8, 9, 10], parsed.Items.Select(x => x.Line));
        Assert.Empty(parsed.Rejected);
    }

    [Fact]
    public void Lines_without_separator_are_split_into_bare_headwords()
    {
        var parsed = VocabularyTextParser.ParseText("budget, invoice; memo/agenda|client + staff，report、fee\n\n  \r\nrefund");
        Assert.Equal(["budget", "invoice", "memo", "agenda", "client", "staff", "report", "fee", "refund"],
            parsed.Items.Select(x => x.Headword));
        Assert.All(parsed.Items, x => Assert.Null(x.MeaningVi));
        Assert.Equal(4, parsed.Items[^1].Line);
    }

    [Fact]
    public void Pos_markers_edge_punctuation_rejects_and_duplicates()
    {
        var parsed = VocabularyTextParser.ParseText(string.Join("\n",
            "apply (v) - nộp đơn",
            "\"reliable\" [adj] - đáng tin cậy",
            "budget - (n) ngân sách",
            "look forward to (informal) - mong đợi",
            "12345 - số",
            "một hai ba",
            "one two three four five six - quá dài",
            "Budget - ngân sách lần hai",
            new string('x', 101)));
        Assert.Equal(
            [("apply", PartOfSpeech.Verb, "nộp đơn"), ("reliable", PartOfSpeech.Adjective, "đáng tin cậy"),
             ("budget", PartOfSpeech.Noun, "ngân sách")],
            parsed.Items.Take(3).Select(x => (x.Headword, x.PartOfSpeech!.Value, x.MeaningVi!)));
        Assert.Equal(("look forward to (informal)", (PartOfSpeech?)null),
            (parsed.Items[3].Headword, parsed.Items[3].PartOfSpeech));
        var again = parsed.Items.Single(x => x.Headword == "Budget");
        Assert.Equal((8, true, false), (again.Line, again.Duplicate, parsed.Items[2].Duplicate));
        Assert.Equal([(5, "no_latin_letter"), (7, "too_many_words"), (9, "too_long")],
            parsed.Rejected.Select(x => (x.Line, x.Code)));
        // A Vietnamese-only line still has latin letters, so it becomes a 3-word phrase (the AI decides later).
        Assert.Contains(parsed.Items, x => x.Headword == "một hai ba");
    }

    [Fact]
    public void Json_category_groups_with_field_aliases_inside_a_fence()
    {
        var json = """
            Here is your list:
            ```json
            [
              { "category": "Office", "words": [
                  { "word": "stapler", "meaning": "cái dập ghim", "ipa": "/ˈsteɪplər/", "type": "n",
                    "ex": "Pass me the stapler.", "ex_vi": "Đưa tôi cái dập ghim." },
                  "printer" ] },
              { "topic": "Travel", "words": [ { "tu": "luggage", "nghia": "hành lý", "loai_tu": "danh từ" } ] },
            ]
            ```
            """;
        var parsed = VocabularyTextParser.ParseJson(json);
        Assert.Equal(["stapler", "printer", "luggage"], parsed.Items.Select(x => x.Headword));
        var stapler = parsed.Items[0];
        Assert.Equal(("cái dập ghim", "/ˈsteɪplər/", PartOfSpeech.Noun, "Pass me the stapler.", "Đưa tôi cái dập ghim.", "Office"),
            (stapler.MeaningVi, stapler.Phonetic, stapler.PartOfSpeech!.Value, stapler.Example, stapler.ExampleVi, stapler.Category));
        Assert.Equal(("Office", "Travel", PartOfSpeech.Noun), (parsed.Items[1].Category, parsed.Items[2].Category,
            parsed.Items[2].PartOfSpeech!.Value));
        Assert.Equal([1, 2, 3], parsed.Items.Select(x => x.Line));
    }

    [Fact]
    public void Json_object_shapes_categories_words_and_maps()
    {
        var categories = VocabularyTextParser.ParseJson("""
            { "categories": [ { "name": "HR", "vocabulary": [ { "term": "payroll", "vi": "bảng lương" } ] } ] }
            """);
        Assert.Equal(("payroll", "bảng lương", "HR"), Single(categories));

        var words = VocabularyTextParser.ParseJson("""{ "group": "Bank", "data": [ { "vocab": "loan", "definition": "khoản vay", "folder": "Finance" } ] }""");
        Assert.Equal(("loan", "khoản vay", "Finance"), Single(words));

        var map = VocabularyTextParser.ParseJson("""{ "refund": "hoàn tiền", "Sales": [ "discount" ], "receipt": { "meaning": "biên lai" }, "123": "x" }""");
        Assert.Equal([("refund", "hoàn tiền", null), ("discount", null, "Sales"), ("receipt", "biên lai", null)],
            map.Items.Select(x => (x.Headword, x.MeaningVi, x.Category)));
        Assert.Equal("no_latin_letter", Assert.Single(map.Rejected).Code);

        var plain = VocabularyTextParser.ParseJson("""["alpha", "ALPHA", 42, { "word": "beta", "pos": "phrasal verb" }]""");
        Assert.Equal([("alpha", false), ("ALPHA", true), ("beta", false)], plain.Items.Select(x => (x.Headword, x.Duplicate)));
        Assert.Equal(PartOfSpeech.Phrase, plain.Items[2].PartOfSpeech);
        Assert.Equal("invalid_item", Assert.Single(plain.Rejected).Code);

        Assert.Equal("invalid_import_json", Assert.Throws<ContentRuleException>(() =>
            VocabularyTextParser.ParseJson("{ not json")).Code);

        static (string, string?, string?) Single(Application.Models.LibraryImport.ParsedImport p) =>
            (Assert.Single(p.Items).Headword, p.Items[0].MeaningVi, p.Items[0].Category);
    }
}
