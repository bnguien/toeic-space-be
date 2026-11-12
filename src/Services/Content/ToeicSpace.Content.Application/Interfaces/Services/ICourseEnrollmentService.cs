using ToeicSpace.Content.Application.Common.Pagination;
using ToeicSpace.Content.Application.DTOs.Courses;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Application.Interfaces.Services;

public sealed record CheckoutCourseDto(Guid CourseId, decimal Price, string Currency, DateTime? ExpiresAt,
    Guid? ReservationId, bool AlreadyEnrolled);
public sealed record MyCourseDto(Guid CourseId, string Title, string Slug, string? ShortDescription,
    EnrollmentStatus EnrollmentStatus, DateTime EnrolledAt, string? ThumbnailUrl = null);

public interface ICourseEnrollmentService
{
    Task<CheckoutCourseDto> GetCheckoutAsync(Guid courseId, Guid userId, CancellationToken ct);
    Task<CheckoutCourseDto> ReserveAsync(Guid courseId, Guid userId, Guid reference, CancellationToken ct);
    Task ReleaseAsync(Guid reference, CancellationToken ct);
    Task<CourseEnrollment> FreeEnrollAsync(Guid courseId, Guid userId, CancellationToken ct);
    Task<CourseEnrollment> ConfirmAsync(Guid courseId, Guid userId, Guid reference, DateTime paidAt,
        bool requiresReservation, CancellationToken ct);
    Task<CourseEnrollment?> StatusAsync(Guid courseId, Guid userId, CancellationToken ct);
    Task<PagedResult<MyCourseDto>> MineAsync(Guid userId, string? search, int page, int pageSize, CancellationToken ct);
    Task<PublicLessonPreviewDto> LessonAsync(Guid courseId, Guid lessonId, Guid userId, CancellationToken ct);
}
