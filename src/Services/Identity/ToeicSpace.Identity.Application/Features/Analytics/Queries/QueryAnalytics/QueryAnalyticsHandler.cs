using ToeicSpace.Identity.Application.Features.Analytics.Common;
using ToeicSpace.Identity.Application.Features.Analytics.Results;
using ToeicSpace.Identity.Application.Interfaces.Persistence;
using ToeicSpace.Identity.Domain.Exceptions;

namespace ToeicSpace.Identity.Application.Features.Analytics.Queries.QueryAnalytics;

public sealed class QueryAnalyticsHandler : IRequestHandler<QueryAnalyticsQuery, AnalyticsResultDto>
{
    private readonly IIdentityDbContext _context;
    private readonly TimeProvider _timeProvider;

    public QueryAnalyticsHandler(
        IIdentityDbContext context,
        TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task<AnalyticsResultDto> Handle(
        QueryAnalyticsQuery query,
        CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var errors = new List<KeyValuePair<string, string>>();

        var request = AnalyticsRequestParser.Parse(query, DateOnly.FromDateTime(now), errors)
            ?? throw new AppException(AnalyticsValues.ToErrorDictionary(errors));

        return await AnalyticsExecution.WithTimeoutAsync(async token =>
        {
            var groups = await ComputeAsync(request, token);
            var selection = AnalyticsResultBuilder.Select(request, groups);

            return AnalyticsResultBuilder.ToDto(request, selection, now);
        }, cancellationToken);
    }

    private Task<IReadOnlyList<AnalyticsGroup>> ComputeAsync(
        AnalyticsRequest request,
        CancellationToken cancellationToken)
        => request.Metric.Source switch
        {
            AnalyticsSource.Registrations => AccountMetrics.ComputeRegistrationsAsync(_context, request, cancellationToken),
            AnalyticsSource.Accounts => AccountMetrics.ComputeAccountsAsync(_context, request, cancellationToken),
            AnalyticsSource.OAuthLinks => AccountMetrics.ComputeOAuthLinksAsync(_context, request, cancellationToken),
            AnalyticsSource.Activity => AccountMetrics.ComputeInactiveAsync(_context, request, cancellationToken),
            AnalyticsSource.SignIns => SignInMetrics.ComputeAsync(_context, request, cancellationToken),
            _ => throw new InvalidOperationException($"Unknown analytics source '{request.Metric.Source}'.")
        };
}
