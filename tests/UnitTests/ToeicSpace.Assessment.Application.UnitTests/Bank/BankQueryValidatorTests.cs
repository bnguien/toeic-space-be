using ToeicSpace.Assessment.Application.Passages.Queries.GetPassages;
using ToeicSpace.Assessment.Application.Practice.Queries.GetPracticeSets;
using ToeicSpace.Assessment.Application.Questions.Queries.GetQuestions;
using ToeicSpace.Assessment.Application.Tests.Queries.GetTests;

namespace ToeicSpace.Assessment.Application.UnitTests.Bank;

/// <summary>Paging and filter values arrive from the query string, so they are validated before the handler runs.</summary>
public class BankQueryValidatorTests
{
    private readonly GetQuestionsValidator _questions = new();

    [Fact]
    public void DefaultQuery_IsValid()
    {
        _questions.Validate(new GetQuestionsQuery()).IsValid.Should().BeTrue();
        new GetTestsValidator().Validate(new GetTestsQuery()).IsValid.Should().BeTrue();
        new GetPassagesValidator().Validate(new GetPassagesQuery()).IsValid.Should().BeTrue();
        new GetPracticeSetsValidator().Validate(new GetPracticeSetsQuery()).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void PageBelowOne_ShouldFail(int page)
    {
        _questions.Validate(new GetQuestionsQuery(Page: page)).Errors
            .Should().Contain(error => error.PropertyName == "Page");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void PageSizeOutsideTheAllowedRange_ShouldFail(int pageSize)
    {
        _questions.Validate(new GetQuestionsQuery(PageSize: pageSize)).Errors
            .Should().Contain(error => error.PropertyName == "PageSize");
    }

    [Fact]
    public void PageSizeAtTheLimit_ShouldPass()
    {
        _questions.Validate(new GetQuestionsQuery(PageSize: 100)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void PartOutsideTheEnum_ShouldFail()
    {
        _questions.Validate(new GetQuestionsQuery(Part: (ToeicPart)9)).Errors
            .Should().Contain(error => error.PropertyName == "Part");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    public void DifficultyOutsideTheEnum_ShouldFail(int difficulty)
    {
        _questions.Validate(new GetQuestionsQuery(DifficultyLevel: (QuestionDifficulty)difficulty)).Errors
            .Should().Contain(error => error.PropertyName == "DifficultyLevel");
    }

    [Fact]
    public void StatusOutsideTheEnum_ShouldFail()
    {
        _questions.Validate(new GetQuestionsQuery(Status: (ContentStatus)7)).Errors
            .Should().Contain(error => error.PropertyName == "Status");
    }

    [Fact]
    public void OverlongSearch_ShouldFail()
    {
        _questions.Validate(new GetQuestionsQuery(Search: new string('a', 201))).Errors
            .Should().Contain(error => error.PropertyName == "Search");
    }

    [Fact]
    public void PagingRules_AreTheSameForEveryBankList()
    {
        new GetTestsValidator().Validate(new GetTestsQuery(PageSize: 101)).IsValid.Should().BeFalse();
        new GetPassagesValidator().Validate(new GetPassagesQuery(Page: 0)).IsValid.Should().BeFalse();
        new GetPracticeSetsValidator().Validate(new GetPracticeSetsQuery(PageSize: 0)).IsValid.Should().BeFalse();
    }
}
