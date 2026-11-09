namespace ToeicSpace.Content.Domain.Enums;

/// <summary>Grammar note of a sentence: about one word or about a phrase.</summary>
public enum DictationNoteType { Word = 1, Phrase = 2 }

/// <summary>State of a roadmap stage for the viewing learner.</summary>
public enum DictationStageState { Done = 1, Current = 2, Locked = 3 }

/// <summary>Hint kind: the first letter of the first wrong word, or that whole word.</summary>
public enum DictationHintMode { FirstLetter = 1, RevealWord = 2 }

/// <summary>Grade of one aligned word: answer word heard right / wrong / missing, or a word the learner added.</summary>
public enum DictationWordStatus { Correct = 1, Wrong = 2, Missing = 3, Extra = 4 }
