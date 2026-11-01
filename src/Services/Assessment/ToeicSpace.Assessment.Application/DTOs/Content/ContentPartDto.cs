namespace ToeicSpace.Assessment.Application.DTOs.Content;

public sealed record ContentPartDto(
    ToeicPart Part,
    string PartName,
    ToeicSection Section,
    int QuestionCount,
    IReadOnlyList<ContentPassageDto> Passages,
    IReadOnlyList<ContentQuestionDto> StandaloneQuestions);
