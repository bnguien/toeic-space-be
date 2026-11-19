namespace ToeicSpace.Assessment.Application.Interfaces.Security;

/// <summary>
/// Replaces identifiers with stable pseudonyms (keyed HMAC-SHA256) so analytics exports can be joined
/// per learner without exposing who the learner is.
/// </summary>
public interface ILearnerPseudonymizer
{
    /// <summary>False when no server-side key is configured; exports must then be refused.</summary>
    bool IsConfigured { get; }

    /// <summary>First 16 hex characters of HMAC-SHA256(key, userId in "D" format).</summary>
    string Learner(Guid userId);

    /// <summary>Pseudonym of an attempt, so answer events can be joined to attempt events.</summary>
    string Attempt(Guid attemptId);
}
