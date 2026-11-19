using ToeicSpace.Content.Application.DTOs.Vocabulary;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Services;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Features.VocabularyQuizzes.Commands.CreateQuiz;

/// <summary>
/// Distractor priority: same part of speech from the answer's (earliest) official stage, then the learner's own
/// custom words of that part of speech, then any official word of that part of speech; finally any word of the
/// stage, the learner's other custom words and the other words of the request. A signed-in learner's personal edits
/// apply to every word involved (prompt meaning, part of speech, distractor meanings).
/// </summary>
public sealed class CreateQuizHandler(IVocabularyCatalogRepository catalog, ILearnerLibraryRepository library,
    PersonalWordOverlay overlay) : IRequestHandler<CreateQuizCommand, IReadOnlyList<QuizQuestionDto>>
{
    private const int OfficialSampleSize = 40;

    public async Task<IReadOnlyList<QuizQuestionDto>> Handle(CreateQuizCommand request, CancellationToken ct)
    {
        var ids = request.WordIds.Distinct().ToArray();
        var personal = await overlay.LoadPersonalizedAsync(request.LearnerId, ct);
        var words = Mine(await catalog.GetWordsAsync(ids, request.LearnerId, ct)).ToDictionary(x => x.Id);
        if (words.Count != ids.Length) throw VocabularyErrors.WordNotFound();

        var stageOf = (await catalog.GetMembershipAsync(null, ids, ct))
            .GroupBy(x => x.WordId)
            .ToDictionary(x => x.Key, x => x.OrderBy(m => m.StageOrder).ThenBy(m => m.SetOrder).First().StageId);
        var stagePools = new Dictionary<Guid, IReadOnlyList<VocabularyWord>>();
        foreach (var stageId in stageOf.Values.Distinct())
            stagePools[stageId] = Mine(await catalog.GetStageWordsAsync(stageId, ct));
        var custom = request.LearnerId == Guid.Empty
            ? []
            : await library.GetCustomWordsAsync(request.LearnerId, ct);
        var batch = words.Values.ToArray();

        // Any official word of the same part of speech, sampled only where the first two pools are too small.
        var officialSamples = new Dictionary<PartOfSpeech, IReadOnlyList<VocabularyWord>>();
        foreach (var answer in batch)
        {
            if (officialSamples.ContainsKey(answer.PartOfSpeech)) continue;
            var nearby = SamePos(StagePool(answer), answer).Concat(SamePos(custom, answer));
            if (QuizQuestionBuilder.CountEligible(answer, nearby) >= QuizQuestionBuilder.OptionCount - 1) continue;
            var candidates = (await catalog.GetOfficialWordIdsAsync(answer.PartOfSpeech, ct)).ToArray();
            Random.Shared.Shuffle(candidates);
            officialSamples[answer.PartOfSpeech] =
                Mine(await catalog.GetWordsAsync(candidates.Take(OfficialSampleSize).ToArray(), Guid.Empty, ct));
        }

        return request.WordIds.Select(id =>
        {
            var answer = words[id];
            var stage = StagePool(answer);
            return QuizQuestionBuilder.Build(answer,
            [
                SamePos(stage, answer),
                SamePos(custom, answer),
                officialSamples.GetValueOrDefault(answer.PartOfSpeech) ?? [],
                stage,
                custom,
                batch
            ], Random.Shared);
        }).ToArray();

        IReadOnlyList<VocabularyWord> StagePool(VocabularyWord answer) =>
            stageOf.TryGetValue(answer.Id, out var stageId) ? stagePools[stageId] : [];

        IReadOnlyList<VocabularyWord> Mine(IReadOnlyList<VocabularyWord> pool) =>
            PersonalWordOverlay.Effective(pool, personal);
    }

    private static IEnumerable<VocabularyWord> SamePos(IEnumerable<VocabularyWord> pool, VocabularyWord answer) =>
        pool.Where(x => x.PartOfSpeech == answer.PartOfSpeech);
}
