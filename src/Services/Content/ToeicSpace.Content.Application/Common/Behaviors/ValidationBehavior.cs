using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Common.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var results = await Task.WhenAll(validators.Select(x => x.ValidateAsync(new ValidationContext<TRequest>(request), ct)));
        var failure = results.SelectMany(x => x.Errors).FirstOrDefault(x => x is not null);
        if (failure is not null) throw new ContentRuleException("validation_error", failure.ErrorMessage);
        return await next();
    }
}
