using ToeicSpace.Content.Application.DTOs.News;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Services;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Features.News.Commands.OpenNewsArticle;

public sealed class OpenNewsArticleHandler(NewsReadingService service, INewsReadingRepository readings,
    IUnitOfWork unitOfWork, TimeProvider clock) : IRequestHandler<OpenNewsArticleCommand, NewsReadingStateDto>
{
    public async Task<NewsReadingStateDto> Handle(OpenNewsArticleCommand request, CancellationToken ct)
    {
        var article = await service.GetArticleAsync(request.Slug, ct);
        var reading = await readings.GetForUpdateAsync(request.LearnerId, article.Id, ct);
        if (reading is null)
        {
            reading = NewsReading.Open(request.LearnerId, article.Id, clock.GetUtcNow().UtcDateTime);
            readings.Add(reading);
            try
            {
                await unitOfWork.SaveChangesAsync(ct);
            }
            catch (ContentRuleException ex) when (ex.Code == "unique_constraint")
            {
                // Two tabs opened the article at the same moment: the other request created the row.
                var current = await readings.GetAsync(request.LearnerId, article.Id, ct);
                if (current is null) throw;
                reading = current;
            }
        }
        return await service.PresentAsync(reading, article, ct);
    }
}
