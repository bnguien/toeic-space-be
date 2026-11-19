using ToeicSpace.Classroom.Domain.Enums;
using ToeicSpace.Classroom.Domain.Exceptions;

namespace ToeicSpace.Classroom.Domain.Entities;

public sealed class Center
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string TimeZoneId { get; set; } = "Asia/Ho_Chi_Minh";
    public FacilityStatus Status { get; set; } = FacilityStatus.Active;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public static void Validate(string name, string address, string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200 ||
            string.IsNullOrWhiteSpace(address) || address.Trim().Length > 500)
            throw new ClassRuleException("invalid_center",
                "Name and address are required and must fit their length limits.");
        if (string.IsNullOrWhiteSpace(timeZoneId) || timeZoneId.Length > 100)
            throw new ClassRuleException("invalid_time_zone", "A valid IANA time zone is required.");
        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException ex)
        {
            throw new ClassRuleException("invalid_time_zone", "Unknown time zone.", 400, ex);
        }
        catch (InvalidTimeZoneException ex)
        {
            throw new ClassRuleException("invalid_time_zone", "Invalid time zone.", 400, ex);
        }
    }
}