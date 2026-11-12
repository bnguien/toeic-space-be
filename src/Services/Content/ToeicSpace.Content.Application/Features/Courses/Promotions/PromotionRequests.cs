using ToeicSpace.Content.Application.Interfaces.Services;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Application.Features.Courses.Promotions;

public sealed record SavePromotionCommand(Guid? Id, CoursePromotion Value) : IRequest<PromotionView>;
public sealed class SavePromotionHandler(ICoursePromotionService promotions) : IRequestHandler<SavePromotionCommand, PromotionView>
{
    public Task<PromotionView> Handle(SavePromotionCommand request, CancellationToken ct) =>
        promotions.SaveAsync(request.Id, request.Value, ct);
}
public sealed record SetPromotionActiveCommand(Guid Id, bool Active) : IRequest<PromotionView>;
public sealed class SetPromotionActiveHandler(ICoursePromotionService promotions)
    : IRequestHandler<SetPromotionActiveCommand, PromotionView>
{
    public Task<PromotionView> Handle(SetPromotionActiveCommand request, CancellationToken ct) =>
        promotions.ActivateAsync(request.Id, request.Active, ct);
}
public sealed record DeletePromotionCommand(Guid Id) : IRequest;
public sealed class DeletePromotionHandler(ICoursePromotionService promotions)
    : IRequestHandler<DeletePromotionCommand>
{
    public Task Handle(DeletePromotionCommand request, CancellationToken ct) =>
        promotions.DeleteAsync(request.Id, ct);
}
public sealed record ListPromotionsQuery : IRequest<IReadOnlyList<PromotionView>>;
public sealed record PreviewPromotionQuery(Guid CourseId, CoursePromotion Value) : IRequest<PromotionPrice>;
public sealed class PreviewPromotionHandler(ICoursePromotionService promotions) : IRequestHandler<PreviewPromotionQuery, PromotionPrice>
{
    public Task<PromotionPrice> Handle(PreviewPromotionQuery request, CancellationToken ct) =>
        promotions.PreviewAsync(request.CourseId, request.Value, ct);
}
public sealed class ListPromotionsHandler(ICoursePromotionService promotions)
    : IRequestHandler<ListPromotionsQuery, IReadOnlyList<PromotionView>>
{
    public Task<IReadOnlyList<PromotionView>> Handle(ListPromotionsQuery request, CancellationToken ct) =>
        promotions.ListAsync(ct);
}
