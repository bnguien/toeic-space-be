using ToeicSpace.Classroom.Domain.Exceptions;

namespace ToeicSpace.Classroom.Application.Common.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!validators.Any()) return await next();

        var context = new ValidationContext<TRequest>(request);
        var results = await Task.WhenAll(validators.Select(x => x.ValidateAsync(context, cancellationToken)));
        var failures = results.SelectMany(x => x.Errors).Where(x => x is not null).ToList();
        if (failures.Count == 0) return await next();

        // The message stays the first failure; every failure is kept per parameter for the problem's "errors".
        throw new ClassRuleException("validation_error", failures[0].ErrorMessage)
        {
            Errors = failures.GroupBy(x => x.PropertyName)
                .ToDictionary(x => x.Key, x => x.Select(f => f.ErrorMessage).Distinct().ToArray())
        };
    }
}
