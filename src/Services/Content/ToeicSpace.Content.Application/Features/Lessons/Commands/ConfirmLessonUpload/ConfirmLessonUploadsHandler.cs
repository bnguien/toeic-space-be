using ToeicSpace.Content.Application.DTOs.Lessons;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Features.Lessons.Commands.ConfirmLessonUpload;

public sealed class ConfirmLessonUploadsHandler(ISender sender)
    : IRequestHandler<ConfirmLessonUploadsCommand, ConfirmLessonUploadsResult>
{
    public async Task<ConfirmLessonUploadsResult> Handle(ConfirmLessonUploadsCommand request, CancellationToken ct)
    {
        var items = new List<ConfirmLessonUploadItemResult>(request.Keys.Count);
        foreach (var key in request.Keys)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var result = await sender.Send(new ConfirmLessonUploadCommand(key, request.AdminId), ct);
                items.Add(new(key, result, null, null));
            }
            catch (ContentRuleException ex)
            {
                if (ex.StatusCode == 503) throw;
                items.Add(new(key, null, ex.Code, ex.Message));
            }
        }
        var count = items.Count(x => x.Upload is not null);
        return new ConfirmLessonUploadsResult(items.Count, count, items.Count - count, items);
    }
}
