using System.Text.Json.Serialization;
using ToeicSpace.Classroom.Application.DTOs.Classes;

namespace ToeicSpace.Classroom.Application.Features.Classes.Commands.UpdateClass;

/// <summary>
/// A partial update of class metadata. A property that is absent from the JSON payload is left unchanged;
/// an explicit <c>null</c> clears nullable properties such as Description, StartDate, and EndDate.
/// </summary>
public sealed class UpdateClassCommand : IRequest<ClassDetailDto>
{
    private string? code;
    private string? name;
    private string? description;
    private DateOnly? startDate;
    private DateOnly? endDate;

    [JsonIgnore]
    public bool HasCode { get; private set; }

    public string? Code
    {
        get => code;
        init { HasCode = true; code = value; }
    }

    [JsonIgnore]
    public bool HasName { get; private set; }

    public string? Name
    {
        get => name;
        init { HasName = true; name = value; }
    }

    [JsonIgnore]
    public bool HasDescription { get; private set; }

    public string? Description
    {
        get => description;
        init { HasDescription = true; description = value; }
    }

    [JsonIgnore]
    public bool HasStartDate { get; private set; }

    public DateOnly? StartDate
    {
        get => startDate;
        init { HasStartDate = true; startDate = value; }
    }

    [JsonIgnore]
    public bool HasEndDate { get; private set; }

    public DateOnly? EndDate
    {
        get => endDate;
        init { HasEndDate = true; endDate = value; }
    }

    [JsonIgnore]
    public Guid ClassId { get; set; }

    [JsonIgnore]
    public Guid AdminId { get; set; }
}
