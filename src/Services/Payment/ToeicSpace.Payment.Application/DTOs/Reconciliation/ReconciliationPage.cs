namespace ToeicSpace.Payment.Application.DTOs.Reconciliation;

public sealed record ReconciliationPage<T>(int Total, IReadOnlyList<T> Items);
