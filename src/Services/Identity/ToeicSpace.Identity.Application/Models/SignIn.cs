namespace ToeicSpace.Identity.Application.Models;

/// <summary>A sign-in or session refresh: each one creates a refresh token.</summary>
public sealed record SignIn(Guid UserId, DateTime At);
