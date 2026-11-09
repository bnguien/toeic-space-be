namespace ToeicSpace.Assessment.Application.Interfaces.Persistence;

/// <summary>
/// Applies a keyword search to a question query. The implementation is provider specific
/// (MySQL FULLTEXT in production) so it lives in Infrastructure.
/// </summary>
public interface IQuestionSearch
{
    IQueryable<ToeicQuestion> Apply(
        IQueryable<ToeicQuestion> query,
        string searchTerm);
}
