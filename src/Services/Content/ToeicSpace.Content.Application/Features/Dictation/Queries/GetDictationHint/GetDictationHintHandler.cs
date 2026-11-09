using ToeicSpace.Content.Application.DTOs.Dictation;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Mappings;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Exceptions;
using ToeicSpace.Content.Domain.Rules;

namespace ToeicSpace.Content.Application.Features.Dictation.Queries.GetDictationHint;

public sealed class GetDictationHintHandler(IDictationCatalogRepository catalog)
    : IRequestHandler<GetDictationHintQuery, DictationHintDto>
{
    public async Task<DictationHintDto> Handle(GetDictationHintQuery request, CancellationToken ct)
    {
        var item = await catalog.GetItemAsync(request.ItemId, ct) ?? throw DictationErrors.ItemNotFound();
        var hint = DictationHints.For(DictationGrader.CleanUserText(request.UserText), item.Text,
            request.Mode ?? DictationHintMode.FirstLetter) ?? throw DictationErrors.EmptyItem();
        return hint.ToDto();
    }
}
