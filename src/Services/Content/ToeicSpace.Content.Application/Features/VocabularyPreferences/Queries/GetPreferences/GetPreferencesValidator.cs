namespace ToeicSpace.Content.Application.Features.VocabularyPreferences.Queries.GetPreferences;

public sealed class GetPreferencesValidator : AbstractValidator<GetPreferencesQuery>
{
    public GetPreferencesValidator() => RuleFor(x => x.LearnerId).NotEmpty();
}
