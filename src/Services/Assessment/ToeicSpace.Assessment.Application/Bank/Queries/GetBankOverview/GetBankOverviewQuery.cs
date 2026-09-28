using ToeicSpace.Assessment.Application.Dtos;

namespace ToeicSpace.Assessment.Application.Bank.Queries.GetBankOverview;

/// <summary>Counts for the question bank overview. Content managers only, so nothing is filtered by status.</summary>
public sealed record GetBankOverviewQuery : IRequest<BankOverviewDto>;
