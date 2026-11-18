using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Exceptions;
using Xunit;

namespace ToeicSpace.Content.Tests;

public sealed class CoursePricingTests
{
    private static Course Course(decimal price, int? capacity) => new()
    {
        Code = "TEST", Title = "Test", Slug = "test", Price = price, Capacity = capacity,
        ExamType = CourseExamType.ListeningReading, Level = CourseLevel.Beginner
    };

    [Theory]
    [InlineData(0, null)]
    [InlineData(100000, 1)]
    public void ValidPricing(decimal price, int? capacity) => Course(price, capacity).Validate();

    [Theory]
    [InlineData(-1, null)]
    [InlineData(1.5, null)]
    [InlineData(0, 0)]
    [InlineData(0, -1)]
    public void InvalidPricing(decimal price, int? capacity) =>
        Assert.Throws<ContentRuleException>(() => Course(price, capacity).Validate());
}
