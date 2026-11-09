using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Domain.Entities;

public sealed class Course
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? ShortDescription { get; set; }
    public string? Description { get; set; }
    public string? ThumbnailKey { get; set; }
    public CourseExamType ExamType { get; set; }
    public CourseLevel Level { get; set; }
    public int? TargetScore { get; set; }
    public List<string> Skills { get; set; } = [];
    public List<string> Objectives { get; set; } = [];
    public List<string> Prerequisites { get; set; } = [];
    public CourseStatus Status { get; set; } = CourseStatus.Draft;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public int Version { get; set; }
    public List<CourseModule> Modules { get; set; } = [];

    public void Validate(bool publishing = false)
    {
        if (string.IsNullOrWhiteSpace(Code) || Code.Length > 50 ||
            string.IsNullOrWhiteSpace(Title) || Title.Length > 200 ||
            string.IsNullOrWhiteSpace(Slug) || Slug.Length > 160 ||
            !System.Text.RegularExpressions.Regex.IsMatch(Slug, "^[a-z0-9]+(?:-[a-z0-9]+)*$"))
            throw new ContentRuleException("invalid_course", "Code, title and a lowercase URL slug are required within length limits.");
        if (ShortDescription?.Length > 500 || Description?.Length > 10000 ||
            !Enum.IsDefined(ExamType) || !Enum.IsDefined(Level))
            throw new ContentRuleException("invalid_course", "Course metadata is invalid.");
        if (Skills.Count > 20 || Objectives.Count > 30 || Prerequisites.Count > 30 ||
            Skills.Concat(Objectives).Concat(Prerequisites).Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 300))
            throw new ContentRuleException("invalid_course", "Course lists exceed their length limits.");
        if (TargetScore is { } score && (score % 10 != 0 || ExamType switch
            {
                CourseExamType.ListeningReading => score is < 10 or > 990,
                CourseExamType.SpeakingWriting => score is < 0 or > 400,
                CourseExamType.FourSkills => score is < 10 or > 1390,
                _ => true
            }))
            throw new ContentRuleException("invalid_target_score", "Target score is outside the exam's supported range or is not a multiple of 10.");
        if (publishing && (string.IsNullOrWhiteSpace(ShortDescription) || string.IsNullOrWhiteSpace(Description)
            || Skills.Count == 0 || Objectives.Count == 0))
            throw new ContentRuleException("publish_requirements", "Publishing requires descriptions, skills and objectives.", 409);
    }

    public void ChangeStatus(CourseStatus target, DateTime now)
    {
        if ((Status, target) is not ((CourseStatus.Draft, CourseStatus.Published)
            or (CourseStatus.Published, CourseStatus.Archived)
            or (CourseStatus.Archived, CourseStatus.Draft)))
            throw ContentRuleException.InvalidState();
        if (target == CourseStatus.Published) { Validate(publishing: true); PublishedAt = now; }
        Status = target;
        Touch(now);
    }

    public void Touch(DateTime now) { UpdatedAt = now; Version++; }
}
