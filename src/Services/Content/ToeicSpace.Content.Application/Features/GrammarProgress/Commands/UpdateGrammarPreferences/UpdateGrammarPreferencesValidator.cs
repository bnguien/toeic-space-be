namespace ToeicSpace.Content.Application.Features.GrammarProgress.Commands.UpdateGrammarPreferences;

public sealed class UpdateGrammarPreferencesValidator : AbstractValidator<UpdateGrammarPreferencesCommand>
{
    public UpdateGrammarPreferencesValidator()
    {
        RuleFor(x => x.RoadmapWidth).InclusiveBetween(300, 720).When(x => x.RoadmapWidth is not null);
    }
}
