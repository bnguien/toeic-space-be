using ToeicSpace.Content.Application.DTOs.VocabularyLibrary;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Application.Mappings;

public static class LibraryMappingExtensions
{
    public static SavedWordDto ToDto(this SavedWord x, LearnerWordProgress? progress, DateOnly localToday) =>
        new(x.Id, x.FolderId, x.Note, DateTime.SpecifyKind(x.SavedAt, DateTimeKind.Utc), x.Word!.IsCustom,
            x.Word.ToDto(), progress.ToProgressDto(localToday));

    public static FolderDto ToDto(this VocabularyFolder x, LibraryCountsDto counts) =>
        new(x.Id, x.Name, x.Order, counts.WordCount, counts.Learning, counts.Mastered, counts.Due);
}
