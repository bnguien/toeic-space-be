using ToeicSpace.Content.Application.DTOs.Lessons;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Features.Lessons.Commands.PresignLessonUpload;

public sealed class PresignLessonUploadsHandler(ISender sender)
    : IRequestHandler<PresignLessonUploadsCommand, PresignLessonUploadsResult>
{
    public async Task<PresignLessonUploadsResult> Handle(PresignLessonUploadsCommand request, CancellationToken ct)
    {
        var items = new List<PresignLessonUploadItemResult>(request.Files.Count);
        for (var index = 0; index < request.Files.Count; index++)
        {
            ct.ThrowIfCancellationRequested();
            var file = request.Files[index];
            try
            {
                var result = await sender.Send(new PresignLessonUploadCommand(
                    file.Kind, file.FileName, file.ContentType, file.SizeBytes, request.AdminId), ct);
                items.Add(new PresignLessonUploadItemResult(index, result, null, null));
            }
            catch (ContentRuleException ex)
            {
                items.Add(new PresignLessonUploadItemResult(index, null, ex.Code, ex.Message));
            }
        }
        var count = items.Count(x => x.Upload is not null);
        return new PresignLessonUploadsResult(items.Count, count, items.Count - count, items);
    }
}
