using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ToeicSpace.Content.Application.Interfaces.Services;

namespace ToeicSpace.Content.API.Controllers;

[ApiController]
[Route("api/v1/internal/checkout")]
[AllowAnonymous]
public sealed class InternalCheckoutController(ICourseEnrollmentService enrollments, ICoursePromotionService promotions,
    IConfiguration configuration) : ControllerBase
{
    private bool Authorized()
    {
        var expected = configuration["InternalApi:Key"];
        if (string.IsNullOrWhiteSpace(expected) || expected.Length < 32 ||
            !Request.Headers.TryGetValue("X-Internal-Key", out var provided)) return false;
        var a = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        var b = SHA256.HashData(Encoding.UTF8.GetBytes(provided.ToString()));
        return CryptographicOperations.FixedTimeEquals(a, b);
    }

    [HttpGet("courses/{courseId:guid}")]
    public async Task<IActionResult> Course(Guid courseId, [FromQuery] Guid userId, CancellationToken ct) =>
        Authorized() ? Ok(await enrollments.GetCheckoutAsync(courseId, userId, ct)) : Unauthorized();

    [HttpPost("reservations")]
    public async Task<IActionResult> Reserve([FromBody] ReserveSlotRequest body, CancellationToken ct) =>
        Authorized() ? Ok(await enrollments.ReserveAsync(body.CourseId, body.UserId, body.CheckoutReference, ct)) : Unauthorized();

    [HttpPost("reservations/{reference:guid}/release")]
    public async Task<IActionResult> Release(Guid reference, CancellationToken ct)
    {
        if (!Authorized()) return Unauthorized();
        await enrollments.ReleaseAsync(reference, ct);
        return Ok();
    }

    [HttpPost("fulfill")]
    public async Task<IActionResult> Confirm([FromBody] ConfirmPurchaseRequest body, CancellationToken ct) =>
        Authorized() ? Ok(await enrollments.ConfirmAsync(body.CourseId, body.UserId, body.CheckoutReference,
            body.PaidAt, body.RequiresReservation, ct)) : Unauthorized();

    [HttpGet("promotions/{courseId:guid}")]
    public async Task<IActionResult> Promotion(Guid courseId, CancellationToken ct)
    {
        if (!Authorized()) return Unauthorized();
        var prices = await promotions.PricesAsync([courseId], ct);
        return Ok(prices.GetValueOrDefault(courseId));
    }

    [HttpPost("promotions/reservations")]
    public async Task<IActionResult> ReservePromotion(PromotionReserveRequest body, CancellationToken ct) =>
        Authorized() ? Ok(await promotions.ReserveAsync(body.CourseId, body.PromotionId, body.CheckoutReference,
            body.OriginalPrice, body.ExpiresAt, ct)) : Unauthorized();

    [HttpPost("promotions/reservations/{reference:guid}/confirm")]
    public async Task<IActionResult> ConfirmPromotion(Guid reference, CancellationToken ct)
    {
        if (!Authorized()) return Unauthorized();
        await promotions.ConfirmAsync(reference, ct);
        return Ok();
    }

    [HttpPost("promotions/reservations/{reference:guid}/release")]
    public async Task<IActionResult> ReleasePromotion(Guid reference, CancellationToken ct)
    {
        if (!Authorized()) return Unauthorized();
        await promotions.ReleaseAsync(reference, ct);
        return Ok();
    }

    public sealed record ReserveSlotRequest(Guid CourseId, Guid UserId, Guid CheckoutReference);
    public sealed record ConfirmPurchaseRequest(Guid CourseId, Guid UserId, Guid CheckoutReference, DateTime PaidAt,
        bool RequiresReservation);
    public sealed record PromotionReserveRequest(Guid CourseId, Guid PromotionId, Guid CheckoutReference,
        decimal OriginalPrice, DateTime ExpiresAt);
}
