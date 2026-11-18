using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Common.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var results = await Task.WhenAll(validators.Select(x => x.ValidateAsync(new ValidationContext<TRequest>(request), ct)));
        var failures = results.SelectMany(x => x.Errors).Where(x => x is not null).ToList();
        if (failures.Count == 0) return await next();

        // The message stays the first failure; every failure is kept per parameter in the problem's "errors".
        var errors = failures.GroupBy(x => x.PropertyName)
            .ToDictionary(x => x.Key, x => x.Select(f => f.ErrorMessage).Distinct().ToArray());
        throw new ContentRuleException("validation_error", failures[0].ErrorMessage)
        {
            Extensions = new Dictionary<string, object?> { ["errors"] = errors }
        };
    }
}
