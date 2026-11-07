using ToeicSpace.Content.Application.DTOs.Dictation;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Rules;

namespace ToeicSpace.Content.Application.Mappings;

public static class DictationMappingExtensions
{
    public static DictationVoiceDto ToDto(this SpeechVoice voice) =>
        new(voice.Key, voice.Name, voice.Accent, SpeechVoices.AccentLabels.GetValueOrDefault(voice.Accent, voice.Accent),
            voice.Gender, SpeechVoices.GenderLabels.GetValueOrDefault(voice.Gender, string.Empty), voice.Flag);

    public static DictationLevelDto ToDto(this DictationLevelInfo level) =>
        new(level.Level, level.Name, level.Band, level.Cefr);

    public static DictationCourseInfoDto ToInfoDto(this DictationCourse course) =>
        new(course.Id, course.Code, course.Title, course.Pack, course.Level);

    public static DictationKeywordDto ToDto(this DictationKeyword keyword) =>
        new(keyword.Word, keyword.Meaning, keyword.PartOfSpeech, keyword.Ipa, keyword.Cefr);

    public static DictationNoteDto ToDto(this DictationNote note) =>
        new(note.Id, note.Text, note.Type, note.Explanation);

    public static SkeletonWordDto ToDto(this SkeletonWord word) => new(word.Before, word.Length, word.After);

    public static WordProofDto ToDto(this WordProofData proof) =>
        new(proof.Algorithm, proof.Iterations, proof.Salt, proof.Digests);

    public static CheckWordDto ToDto(this GradedWord word) => new(word.Text, word.Status, word.Typed);

    public static DictationHintDto ToDto(this DictationHint hint) =>
        new(hint.Index, hint.Total, hint.Mode, hint.Length, hint.Word, hint.Letter);

    public static SoundPreferencesDto ToDto(this DictationSoundPreferences sound) =>
        new(sound.Enabled, sound.Volume, sound.Tick, sound.Chime);
}
