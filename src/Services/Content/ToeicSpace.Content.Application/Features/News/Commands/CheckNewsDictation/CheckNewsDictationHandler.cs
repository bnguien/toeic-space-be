using ToeicSpace.Content.Application.DTOs.News;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Mappings;
using ToeicSpace.Content.Application.Services;
using ToeicSpace.Content.Domain.Exceptions;
using ToeicSpace.Content.Domain.Rules;

namespace ToeicSpace.Content.Application.Features.News.Commands.CheckNewsDictation;

public sealed class CheckNewsDictationHandler(NewsReadingService service, IUnitOfWork unitOfWork, TimeProvider clock)
    : IRequestHandler<CheckNewsDictationCommand, DictationResultDto>
{
    public async Task<DictationResultDto> Handle(CheckNewsDictationCommand request, CancellationToken ct)
    {
        var article = await service.GetArticleAsync(request.Slug, ct);
        var sentence = article.FindSentence(request.SentenceId) ?? throw NewsErrors.SentenceNotFound();
        var grade = NewsDictationGrader.Grade(request.Text, sentence.Text);
        if (request.LearnerId == Guid.Empty) return grade.ToDto(recorded: false);

        var now = clock.GetUtcNow().UtcDateTime;
        var reading = await service.GetOrOpenAsync(request.LearnerId, article, now, ct);
        reading.RecordDictationAttempt(sentence.Id, request.Text, grade.Correct, request.Revealed, now);
        await unitOfWork.SaveChangesAsync(ct);
        return grade.ToDto(recorded: true);
    }
}
