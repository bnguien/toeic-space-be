using ToeicSpace.Assessment.Application.DTOs.Bank;


namespace ToeicSpace.Assessment.Application.Features.Bank.Queries.GetBankOverview;

/// <summary>Counts for the question bank overview. Content managers only, so nothing is filtered by status.</summary>
public sealed record GetBankOverviewQuery : IRequest<BankOverviewDto>;
