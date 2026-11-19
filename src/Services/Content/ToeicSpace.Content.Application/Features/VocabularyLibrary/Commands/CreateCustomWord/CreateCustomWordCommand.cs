using System.Text.Json.Serialization;
using ToeicSpace.Content.Application.DTOs.VocabularyLibrary;
using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Application.Features.VocabularyLibrary.Commands.CreateCustomWord;

/// <summary>Creates a private word owned by the learner and saves it to the library.</summary>
public sealed record CreateCustomWordCommand(string Headword, string? Phonetic, PartOfSpeech PartOfSpeech,
    string MeaningVi, string? DefinitionEn, string? Example, string? ExampleVi, Guid? FolderId, string? Note,
    int TzOffset = 0, [property: JsonIgnore] Guid LearnerId = default) : IRequest<SavedWordDto>
{
    public CustomWordInput ToInput() =>
        new(Headword, Phonetic, PartOfSpeech, MeaningVi, DefinitionEn, Example, ExampleVi);
}
