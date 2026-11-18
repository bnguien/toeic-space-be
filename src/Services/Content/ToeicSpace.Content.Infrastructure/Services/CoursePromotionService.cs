using Microsoft.EntityFrameworkCore;
using ToeicSpace.Content.Application.Interfaces.Services;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Exceptions;
using ToeicSpace.Content.Infrastructure.Persistence;

namespace ToeicSpace.Content.Infrastructure.Services;

public sealed class CoursePromotionService(ContentDbContext db, TimeProvider clock) : ICoursePromotionService
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    private static void Validate(CoursePromotion value)
    {
        if (string.IsNullOrWhiteSpace(value.Name) || value.Name.Length > 160 || value.CourseIds is null ||
            value.CourseIds.Count == 0 || value.CourseIds.Any(x => x == Guid.Empty) ||
            value.CourseIds.Count != value.CourseIds.Distinct().Count() ||
            !Enum.IsDefined(value.DiscountType) || value.StartAt.Kind == DateTimeKind.Local ||
            value.ExpiresAt.Kind == DateTimeKind.Local || value.StartAt >= value.ExpiresAt ||
            value.DiscountValue <= 0 || value.DiscountValue > 9999999999999999m ||
            value.UsageLimit is <= 0 || value.MaximumDiscountAmount is <= 0 or > 999999999999999999m ||
            value.MaximumDiscountAmount is { } max && max != decimal.Truncate(max) ||
            value.DiscountType == PromotionDiscountType.Percentage &&
                (value.DiscountValue > 100 || value.DiscountValue * 100 != decimal.Truncate(value.DiscountValue * 100)) ||
            value.DiscountType == PromotionDiscountType.FixedAmount && value.DiscountValue != decimal.Truncate(value.DiscountValue))
            throw new ContentRuleException("invalid_promotion", "Promotion configuration is invalid.", 400);
    }

    public async Task<PromotionView> SaveAsync(Guid? id, CoursePromotion value, CancellationToken ct)
    {
        Validate(value);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        CoursePromotion? old = null;
        if (id.HasValue)
            old = await db.CoursePromotions.FromSqlInterpolated($"SELECT * FROM CoursePromotions WHERE Id = {id.Value} FOR UPDATE")
                .SingleOrDefaultAsync(ct) ?? throw ContentRuleException.NotFound();
        var courseIds = value.CourseIds.Concat(old?.CourseIds ?? []).Distinct().Order().ToArray();
        foreach (var courseId in courseIds)
            if (!await db.Courses.FromSqlInterpolated($"SELECT * FROM Courses WHERE Id = {courseId} FOR UPDATE").AnyAsync(ct))
                throw ContentRuleException.NotFound();
        if (value.IsActive && value.ExpiresAt > Now)
        {
            var others = await db.CoursePromotions.AsNoTracking().Where(x => x.Id != (id ?? Guid.Empty) &&
                x.IsActive && x.ExpiresAt > value.StartAt && x.StartAt < value.ExpiresAt && x.ExpiresAt > Now).ToListAsync(ct);
            if (others.Any(x => x.CourseIds.Intersect(value.CourseIds).Any()))
                throw new ContentRuleException("promotion_overlap", "Active promotions for a course cannot overlap.", 409);
        }
        var used = old is null ? 0 : await TakenAsync(old.Id, ct);
        if (value.UsageLimit is { } limit && used > limit)
            throw new ContentRuleException("promotion_limit", "Limit is below existing usage.", 409);
        var promotion = old ?? new CoursePromotion();
        if (old is null) promotion.Id = value.Id == Guid.Empty ? Guid.NewGuid() : value.Id;
        if (old is null) db.CoursePromotions.Add(promotion);
        promotion.Name = value.Name.Trim();
        promotion.DiscountType = value.DiscountType;
        promotion.DiscountValue = value.DiscountValue;
        promotion.MaximumDiscountAmount = value.MaximumDiscountAmount;
        promotion.StartAt = DateTime.SpecifyKind(value.StartAt, DateTimeKind.Utc);
        promotion.ExpiresAt = DateTime.SpecifyKind(value.ExpiresAt, DateTimeKind.Utc);
        promotion.IsActive = value.IsActive;
        promotion.UsageLimit = value.UsageLimit;
        promotion.CourseIds = value.CourseIds.ToList();
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await ViewAsync(promotion, ct);
    }

    public async Task<PromotionPrice> PreviewAsync(Guid courseId, CoursePromotion value, CancellationToken ct)
    {
        Validate(value);
        var course = await db.Courses.AsNoTracking().SingleOrDefaultAsync(x => x.Id == courseId, ct)
            ?? throw ContentRuleException.NotFound();
        if (!value.CourseIds.Contains(courseId))
            throw new ContentRuleException("invalid_promotion", "Course is not selected for this promotion.", 400);
        var discount = value.Discount(course.Price);
        return new PromotionPrice(value.Id, value.Name, course.Price, discount, course.Price - discount,
            DateTime.SpecifyKind(value.ExpiresAt, DateTimeKind.Utc));
    }

    public async Task<PromotionView> ActivateAsync(Guid id, bool active, CancellationToken ct)
    {
        var value = await db.CoursePromotions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw ContentRuleException.NotFound();
        value.IsActive = active;
        return await SaveAsync(id, value, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var promotion = await db.CoursePromotions
            .FromSqlInterpolated($"SELECT * FROM CoursePromotions WHERE Id = {id} FOR UPDATE")
            .SingleOrDefaultAsync(ct)
            ?? throw new ContentRuleException("promotion_not_found", "Promotion not found.", 404);
        var reservations = await db.PromotionReservations.Where(x => x.PromotionId == id).ToListAsync(ct);
        if (reservations.Any(x => x.Status != PromotionReservationStatus.Released))
            throw new ContentRuleException("promotion_in_use",
                "Promotion has usage history and cannot be deleted.", 409);
        db.PromotionReservations.RemoveRange(reservations);
        db.CoursePromotions.Remove(promotion);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<PromotionView>> ListAsync(CancellationToken ct)
    {
        var promotions = await db.CoursePromotions.AsNoTracking().OrderByDescending(x => x.StartAt).ToListAsync(ct);
        var result = new List<PromotionView>(promotions.Count);
        foreach (var promotion in promotions) result.Add(await ViewAsync(promotion, ct));
        return result;
    }

    private async Task<PromotionView> ViewAsync(CoursePromotion p, CancellationToken ct) => new(p,
        await db.PromotionReservations.CountAsync(x => x.PromotionId == p.Id && x.Status == PromotionReservationStatus.Redeemed, ct),
        await db.PromotionReservations.CountAsync(x => x.PromotionId == p.Id && x.Status == PromotionReservationStatus.Reserved, ct));

    public async Task<IReadOnlyDictionary<Guid, PromotionPrice>> PricesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return new Dictionary<Guid, PromotionPrice>();
        var now = Now;
        var courses = await db.Courses.AsNoTracking().Where(x => ids.Contains(x.Id) && x.Price > 0)
            .Select(x => new { x.Id, x.Price }).ToListAsync(ct);
        var promotions = await db.CoursePromotions.AsNoTracking().Where(x => x.IsActive &&
            x.StartAt <= now && x.ExpiresAt > now).ToListAsync(ct);
        var promotionIds = promotions.Select(x => x.Id).ToArray();
        var counts = await db.PromotionReservations.Where(x => promotionIds.Contains(x.PromotionId) &&
            x.Status != PromotionReservationStatus.Released).GroupBy(x => x.PromotionId)
            .Select(x => new { Id = x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.Id, x => x.Count, ct);
        var result = new Dictionary<Guid, PromotionPrice>();
        foreach (var course in courses)
        {
            var eligible = promotions.Where(x => x.CourseIds.Contains(course.Id) &&
                (!x.UsageLimit.HasValue || counts.GetValueOrDefault(x.Id) < x.UsageLimit));
            var p = eligible.OrderBy(x => x.ExpiresAt).FirstOrDefault();
            if (p is null) continue;
            var discount = p.Discount(course.Price);
            if (discount <= 0) continue;
            result[course.Id] = new PromotionPrice(p.Id, p.Name, course.Price, discount,
                course.Price - discount, DateTime.SpecifyKind(p.ExpiresAt, DateTimeKind.Utc));
        }
        return result;
    }

    public async Task<PromotionPrice?> ReserveAsync(Guid courseId, Guid promotionId, Guid reference, decimal expectedPrice,
        DateTime expiresAt, CancellationToken ct)
    {
        if (reference == Guid.Empty) throw new ContentRuleException("invalid_promotion", "Missing reference.", 400);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Lock BEFORE any consistent read in the transaction: MySQL REPEATABLE READ otherwise
        // keeps the pre-lock snapshot and two checkouts can both see the last usage slot.
        var p = await db.CoursePromotions.FromSqlInterpolated($"SELECT * FROM CoursePromotions WHERE Id = {promotionId} FOR UPDATE")
            .SingleOrDefaultAsync(ct);
        var course = await db.Courses.FromSqlInterpolated($"SELECT * FROM Courses WHERE Id = {courseId} FOR UPDATE")
            .SingleOrDefaultAsync(ct);
        var existing = await db.PromotionReservations.SingleOrDefaultAsync(x => x.CheckoutReference == reference, ct);
        if (existing is not null)
        {
            if (existing.PromotionId != promotionId || existing.CourseId != courseId ||
                existing.Status != PromotionReservationStatus.Reserved)
                throw new ContentRuleException("promotion_conflict", "Reservation belongs to another checkout.", 409);
            var saved = await db.CoursePromotions.AsNoTracking().SingleAsync(x => x.Id == promotionId, ct);
            if (existing.OriginalPrice != expectedPrice)
                throw new ContentRuleException("promotion_conflict", "Reservation price changed.", 409);
            return new PromotionPrice(saved.Id, saved.Name, existing.OriginalPrice, existing.DiscountAmount,
                existing.OriginalPrice - existing.DiscountAmount, DateTime.SpecifyKind(saved.ExpiresAt, DateTimeKind.Utc));
        }
        var now = Now;
        var original = course?.Price ?? 0;
        if (p is null || !p.IsActive || now < p.StartAt || now >= p.ExpiresAt ||
            !p.CourseIds.Contains(courseId) || original != expectedPrice || original <= 0 || expiresAt <= now ||
            p.UsageLimit is { } limit && await TakenAsync(p.Id, ct) >= limit)
            throw new ContentRuleException("promotion_unavailable", "Promotion is no longer available.", 409);
        var discount = p.Discount(original);
        db.PromotionReservations.Add(new PromotionReservation
        {
            PromotionId = p.Id, CourseId = courseId, CheckoutReference = reference,
            OriginalPrice = original, DiscountAmount = discount,
            CreatedAt = now, ExpiresAt = expiresAt
        });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new PromotionPrice(p.Id, p.Name, original, discount, original - discount,
            DateTime.SpecifyKind(p.ExpiresAt, DateTimeKind.Utc));
    }

    private Task<int> TakenAsync(Guid id, CancellationToken ct) => db.PromotionReservations.CountAsync(x =>
        x.PromotionId == id && x.Status != PromotionReservationStatus.Released, ct);

    public Task ConfirmAsync(Guid reference, CancellationToken ct) => SettleAsync(reference, true, ct);
    public Task ReleaseAsync(Guid reference, CancellationToken ct) => SettleAsync(reference, false, ct);

    private async Task SettleAsync(Guid reference, bool paid, CancellationToken ct)
    {
        var id = await db.PromotionReservations.AsNoTracking().Where(x => x.CheckoutReference == reference)
            .Select(x => x.PromotionId).SingleOrDefaultAsync(ct);
        if (id == Guid.Empty) return;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.CoursePromotions.FromSqlInterpolated($"SELECT * FROM CoursePromotions WHERE Id = {id} FOR UPDATE").SingleAsync(ct);
        var reservation = await db.PromotionReservations.SingleAsync(x => x.CheckoutReference == reference, ct);
        var target = paid ? PromotionReservationStatus.Redeemed : PromotionReservationStatus.Released;
        if (reservation.Status != target)
        {
            if (reservation.Status != PromotionReservationStatus.Reserved)
                throw new ContentRuleException("promotion_conflict", "Reservation already settled differently.", 409);
            reservation.Status = target;
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
    }
}
