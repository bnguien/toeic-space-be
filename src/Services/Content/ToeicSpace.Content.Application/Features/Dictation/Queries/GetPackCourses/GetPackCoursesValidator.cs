using System.Text.RegularExpressions;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Features.Dictation.Queries.GetPackCourses;

public sealed partial class GetPackCoursesValidator : AbstractValidator<GetPackCoursesQuery>
{
    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex PackSlug();

    public GetPackCoursesValidator()
    {
        RuleFor(x => x.Pack).Must(x => x is not null && x.Length <= 40 && PackSlug().IsMatch(x.Trim()))
            .WithMessage(DictationErrors.InvalidPack().Message);
    }
}
