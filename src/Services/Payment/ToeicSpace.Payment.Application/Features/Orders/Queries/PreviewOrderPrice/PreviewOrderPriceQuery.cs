using MediatR;
using ToeicSpace.Payment.Application.DTOs.Orders;

namespace ToeicSpace.Payment.Application.Features.Orders.Queries.PreviewOrderPrice;

public sealed record PreviewOrderPriceQuery(Guid CourseId, Guid UserId, string? CouponCode) : IRequest<PricePreview>;
