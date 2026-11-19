using ToeicSpace.Content.Application.DTOs.Lessons;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Features.Lessons.Commands.AddLessonResource;

public sealed class AddLessonResourcesHandler(ISender sender)
    : IRequestHandler<AddLessonResourcesCommand, AddLessonResourcesResult>
{
    public async Task<AddLessonResourcesResult> Handle(AddLessonResourcesCommand request, CancellationToken ct)
    {
        var items = new List<AddLessonResourceItemResult>(request.ObjectKeys.Count);
        foreach (var key in request.ObjectKeys)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var resource = await sender.Send(new AddLessonResourceCommand(key)
                {
                    CourseId = request.CourseId, ModuleId = request.ModuleId,
                    LessonId = request.LessonId, AdminId = request.AdminId
                }, ct);
                items.Add(new(key, resource, null, null));
            }
            catch (ContentRuleException ex)
            {
                items.Add(new(key, null, ex.Code, ex.Message));
            }
        }
        var added = items.Count(x => x.Resource is not null);
        return new AddLessonResourcesResult(items.Count, added, items.Count - added, items);
    }
}
