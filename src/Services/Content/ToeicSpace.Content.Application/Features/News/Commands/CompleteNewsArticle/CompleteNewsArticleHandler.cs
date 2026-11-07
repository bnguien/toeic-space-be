using ToeicSpace.Content.Application.DTOs.News;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Services;

namespace ToeicSpace.Content.Application.Features.News.Commands.CompleteNewsArticle;

public sealed class CompleteNewsArticleHandler(NewsReadingService service, IUnitOfWork unitOfWork,
    TimeProvider clock) : IRequestHandler<CompleteNewsArticleCommand, NewsReadingStateDto>
{
    public async Task<NewsReadingStateDto> Handle(CompleteNewsArticleCommand request, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var article = await service.GetArticleAsync(request.Slug, ct);
        var reading = await service.GetOrOpenAsync(request.LearnerId, article, now, ct);
        reading.Complete(now);
        await unitOfWork.SaveChangesAsync(ct);
        return await service.PresentAsync(reading, article, ct);
    }
}
