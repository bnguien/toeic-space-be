namespace ToeicSpace.Identity.Application.DTOs.Users;

/// <param name="Start">Local start time of the bucket (no offset), e.g. 2026-10-22T13:00:00.</param>
public sealed record TrendBucketResult(DateTime Start, int Registrations, int ActiveUsers);
