using ToeicSpace.Content.Application.DTOs.Dictation;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Features.Dictation.Queries.GetCourseVocab;

/// <summary>Words to pre-learn in basic mode, in sentence order (entries without a word or meaning are skipped).</summary>
public sealed class GetCourseVocabHandler(IDictationCatalogRepository catalog)
    : IRequestHandler<GetCourseVocabQuery, CourseVocabDto>
{
    public async Task<CourseVocabDto> Handle(GetCourseVocabQuery request, CancellationToken ct)
    {
        var course = await catalog.GetCourseAsync(request.CourseId, ct) ?? throw DictationErrors.CourseNotFound();
        var items = await catalog.GetCourseItemsAsync(course.Id, ct);
        var words = items.SelectMany((item, index) => item.Vocab
                .Where(v => !string.IsNullOrWhiteSpace(v.Word) && !string.IsNullOrWhiteSpace(v.Meaning))
                .Select(v => new CourseVocabWordDto(v.Word, v.Lemma, v.Meaning, v.PartOfSpeech, v.Ipa, index + 1)))
            .ToArray();
        return new CourseVocabDto(new CourseVocabCourseDto(course.Id, course.Title, course.Level, course.Pack,
            course.ItemCount), words);
    }
}
