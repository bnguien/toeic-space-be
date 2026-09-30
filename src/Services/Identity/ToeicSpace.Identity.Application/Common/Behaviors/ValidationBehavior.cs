using ToeicSpace.Identity.Domain.Exceptions;

namespace ToeicSpace.Identity.Application.Common.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);
        var validationResults = await Task.WhenAll(
            _validators.Select(validator =>
                validator.ValidateAsync(context, cancellationToken)));

        var failures = validationResults
            .SelectMany(result => result.Errors)
            .Where(failure => failure is not null)
            .ToArray();

        // Keep reset-token failures distinct from password field validation errors.
        var tokenFailure = failures.FirstOrDefault(failure => failure.ErrorCode == ErrorCodes.TokenInvalid);
        if (tokenFailure is not null)
        {
            throw AppException.Validation(tokenFailure.ErrorMessage, tokenFailure.ErrorCode);
        }

        var errors = failures
            .GroupBy(failure => failure.PropertyName)
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => failure.ErrorMessage)
                    .Distinct()
                    .ToArray());

        if (errors.Count > 0)
        {
            throw new AppException(errors);
        }

        return await next();
    }
}
