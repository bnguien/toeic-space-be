namespace ToeicSpace.Content.Application.Features.Dictation.Queries.GetDictationRoadmap;

public sealed class GetDictationRoadmapValidator : AbstractValidator<GetDictationRoadmapQuery>
{
    public GetDictationRoadmapValidator()
    {
        RuleFor(x => x.LearnerId).Must(x => x != Guid.Empty).When(x => x.LearnerId is not null);
    }
}
