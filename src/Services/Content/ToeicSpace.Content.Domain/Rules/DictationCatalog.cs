namespace ToeicSpace.Content.Domain.Rules;

public sealed record DictationLevelInfo(int Level, string Name, string Band, string Cefr);

/// <summary>Static catalog of the dictation roadmap: levels, score bands, roadmap packs and topic names.</summary>
public static class DictationCatalog
{
    public static readonly IReadOnlyList<DictationLevelInfo> Levels =
    [
        new(1, "Starter", "10-250", "A1"),
        new(2, "Elementary", "250-400", "A2"),
        new(3, "Intermediate", "400-600", "B1"),
        new(4, "Upper-Intermediate", "600-800", "B2"),
        new(5, "Advanced", "800-990", "B2+/C1"),
    ];

    /// <summary>Score band of the AIO course set (packs toeic-250 … toeic-990), shown on the roadmap.</summary>
    public static readonly IReadOnlyDictionary<int, string> RoadmapBands = new Dictionary<int, string>
    {
        [1] = "10-250", [2] = "250-450", [3] = "450-650", [4] = "650-800", [5] = "800-990",
    };

    /// <summary>Packs drawn on the TOEIC roadmap (pack → level); other packs are topic packs.</summary>
    public static readonly IReadOnlyDictionary<string, int> RoadmapPacks = new Dictionary<string, int>
    {
        ["toeic-250"] = 1, ["toeic-450"] = 2, ["toeic-650"] = 3, ["toeic-800"] = 4, ["toeic-990"] = 5,
    };

    public static readonly IReadOnlyList<(string Key, string Name)> Topics =
    [
        ("office_hr", "Văn phòng & Nhân sự"),
        ("business_finance", "Kinh doanh & Tài chính"),
        ("shopping_service", "Mua sắm & Dịch vụ khách hàng"),
        ("travel_business_trip", "Du lịch & Đi lại công tác"),
        ("restaurant_events", "Nhà hàng & Sự kiện"),
        ("health_realestate_media", "Sức khỏe, Bất động sản & Truyền thông"),
        ("manufacturing_technical", "Sản xuất & Kỹ thuật"),
    ];

    public static DictationLevelInfo LevelInfo(int level) =>
        Levels.FirstOrDefault(x => x.Level == level) ?? Levels[0];

    public static bool IsLevel(int level) => level is >= 1 and <= 5;
}
