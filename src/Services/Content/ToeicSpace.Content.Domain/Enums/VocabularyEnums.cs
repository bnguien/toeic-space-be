namespace ToeicSpace.Content.Domain.Enums;

public enum PartOfSpeech
{
    Noun = 1, Verb = 2, Adjective = 3, Adverb = 4, Preposition = 5, Conjunction = 6,
    Pronoun = 7, Determiner = 8, Interjection = 9, Phrase = 10, Other = 11
}

/// <summary>Learner grade of one review. <see cref="Seen"/> is the passive Reel timeout.</summary>
public enum ReviewGrade { Forget = 1, Hard = 2, Good = 3, Easy = 4, Seen = 5 }

public enum StudyMode { Flashcard = 1, Quiz = 2, Reel = 3 }

public enum WordStatus { New = 1, Learning = 2, Mastered = 3 }
