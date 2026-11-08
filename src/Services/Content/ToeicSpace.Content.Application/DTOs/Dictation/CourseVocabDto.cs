namespace ToeicSpace.Content.Application.DTOs.Dictation;

public sealed record CourseVocabCourseDto(int Id, string Title, int Level, string Pack, int ItemCount);

/// <summary>A word to pre-learn; <paramref name="Sentence"/> is the 1-based sentence of the course it belongs to.</summary>
public sealed record CourseVocabWordDto(string Word, string Lemma, string Meaning, string PartOfSpeech, string Ipa,
    int Sentence);

public sealed record CourseVocabDto(CourseVocabCourseDto Course, IReadOnlyList<CourseVocabWordDto> Words);
