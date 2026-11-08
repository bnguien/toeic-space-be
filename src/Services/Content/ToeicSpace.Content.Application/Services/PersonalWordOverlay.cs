using ToeicSpace.Content.Application.DTOs.Vocabulary;
using ToeicSpace.Content.Application.DTOs.VocabularyLookup;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Application.Services;

/// <summary>
/// The single place where a learner's personal edits (<see cref="SavedWord"/> <c>My*</c> columns and note) are laid
/// over shared words: effective value = override ?? shared value. Every learner-facing response that returns words
/// goes through it (set detail, study queue, sessions, library, quizzes, lookup, mistakes). Shared words, other
/// learners and caches are never touched; guests (<see cref="Guid.Empty"/>) get the shared words unchanged.
/// </summary>
public sealed class PersonalWordOverlay(ILearnerLibraryRepository library)
{
    public const string PhoneticField = "phonetic";
    public const string PartOfSpeechField = "partOfSpeech";
    public const string MeaningViField = "meaningVi";
    public const string ExampleField = "example";
    public const string ExampleViField = "exampleVi";
    /// <summary>Lookup <c>sources</c> value of a field taken from the learner's own edit.</summary>
    public const string PersonalSource = "personal";

    private static readonly IReadOnlyDictionary<Guid, SavedWord> None = new Dictionary<Guid, SavedWord>();

    /// <summary>The learner's library rows of these words, by word id (empty for guests).</summary>
    public async Task<IReadOnlyDictionary<Guid, SavedWord>> LoadAsync(Guid learnerId,
        IReadOnlyCollection<Guid> wordIds, CancellationToken ct) =>
        learnerId == Guid.Empty || wordIds.Count == 0
            ? None
            : (await library.GetSavedWordsByWordAsync(learnerId, wordIds, ct)).ToDictionary(x => x.WordId);

    /// <summary>Only the rows with personal edits, by word id: cheap for large candidate pools such as quizzes.</summary>
    public async Task<IReadOnlyDictionary<Guid, SavedWord>> LoadPersonalizedAsync(Guid learnerId,
        CancellationToken ct) =>
        learnerId == Guid.Empty ? None : (await library.GetPersonalizedAsync(learnerId, ct)).ToDictionary(x => x.WordId);

    public async Task<IReadOnlyList<WordDto>> ApplyAsync(Guid learnerId, IReadOnlyList<WordDto> words,
        CancellationToken ct)
    {
        var saved = await LoadAsync(learnerId, words.Select(x => x.Id).Distinct().ToArray(), ct);
        return saved.Count == 0 ? words : words.Select(x => Apply(x, saved.GetValueOrDefault(x.Id))).ToArray();
    }

    /// <summary>Overridden field names in display order (camelCase), empty when the word is not edited.</summary>
    public static IReadOnlyList<string> PersonalFields(SavedWord saved)
    {
        var fields = new List<string>(5);
        if (saved.MyPhonetic is not null) fields.Add(PhoneticField);
        if (saved.MyPartOfSpeech is not null) fields.Add(PartOfSpeechField);
        if (saved.MyMeaningVi is not null) fields.Add(MeaningViField);
        if (saved.MyExample is not null) fields.Add(ExampleField);
        if (saved.MyExampleVi is not null) fields.Add(ExampleViField);
        return fields;
    }

    /// <summary>
    /// The word as the learner sees it: <c>note</c> from the library row; for an edited word the overridden fields,
    /// <c>personal</c> and <c>original</c> (shared values).
    /// </summary>
    public static WordDto Apply(WordDto shared, SavedWord? saved)
    {
        if (saved is null) return shared;
        var personal = PersonalFields(saved);
        if (personal.Count == 0) return shared with { Note = saved.Note };
        return shared with
        {
            Phonetic = saved.MyPhonetic ?? shared.Phonetic,
            PartOfSpeech = saved.MyPartOfSpeech ?? shared.PartOfSpeech,
            MeaningVi = saved.MyMeaningVi ?? shared.MeaningVi,
            Example = saved.MyExample ?? shared.Example,
            ExampleVi = saved.MyExampleVi ?? shared.ExampleVi,
            Note = saved.Note,
            Personal = personal,
            Original = new WordOriginalDto(shared.Phonetic, shared.PartOfSpeech, shared.MeaningVi, shared.Example,
                shared.ExampleVi)
        };
    }

    /// <summary>
    /// Effective copies for in-memory work such as quiz building: an edited word becomes a detached copy carrying the
    /// learner's values; other words are returned as they are. The copies are never attached or saved.
    /// </summary>
    public static IReadOnlyList<VocabularyWord> Effective(IEnumerable<VocabularyWord> words,
        IReadOnlyDictionary<Guid, SavedWord> personalized) =>
        personalized.Count == 0
            ? words as IReadOnlyList<VocabularyWord> ?? words.ToArray()
            : words.Select(x => personalized.TryGetValue(x.Id, out var saved) && !x.IsCustom ? Effective(x, saved) : x)
                .ToArray();

    private static VocabularyWord Effective(VocabularyWord word, SavedWord saved) => new()
    {
        Id = word.Id, OwnerKey = word.OwnerKey, Headword = word.Headword, NormalizedHeadword = word.NormalizedHeadword,
        PartOfSpeech = saved.MyPartOfSpeech ?? word.PartOfSpeech, Phonetic = saved.MyPhonetic ?? word.Phonetic,
        MeaningVi = saved.MyMeaningVi ?? word.MeaningVi, DefinitionEn = word.DefinitionEn,
        Example = saved.MyExample ?? word.Example, ExampleVi = saved.MyExampleVi ?? word.ExampleVi, Cefr = word.Cefr,
        Synonyms = word.Synonyms, CreatedAt = word.CreatedAt, UpdatedAt = word.UpdatedAt
    };

    /// <summary>
    /// A lookup of a word the learner already saved and edited shows the learner's values first; the overridden
    /// fields get the source <see cref="PersonalSource"/>. The shared gloss cache is not touched.
    /// </summary>
    public static WordLookupDto Apply(WordLookupDto lookup, SavedWord? saved)
    {
        if (saved is null || saved.WordId != lookup.OfficialWordId) return lookup;
        var personal = PersonalFields(saved);
        if (personal.Count == 0) return lookup;
        var sources = new Dictionary<string, string>(lookup.Sources);
        foreach (var field in personal) sources[field] = PersonalSource;
        return lookup with
        {
            Phonetic = saved.MyPhonetic ?? lookup.Phonetic,
            PartOfSpeech = saved.MyPartOfSpeech ?? lookup.PartOfSpeech,
            MeaningVi = saved.MyMeaningVi ?? lookup.MeaningVi,
            Example = saved.MyExample ?? lookup.Example,
            ExampleVi = saved.MyExampleVi ?? lookup.ExampleVi,
            Sources = sources
        };
    }
}
