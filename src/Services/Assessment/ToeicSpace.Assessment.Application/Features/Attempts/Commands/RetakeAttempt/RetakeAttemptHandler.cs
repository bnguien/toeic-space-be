using Microsoft.Extensions.Logging;
using ToeicSpace.Assessment.Application.DTOs.Attempts;
using ToeicSpace.Assessment.Application.Interfaces.Persistence;
using ToeicSpace.Assessment.Application.Models.Attempts;
using ToeicSpace.Assessment.Application.Services.Attempts;

namespace ToeicSpace.Assessment.Application.Features.Attempts.Commands.RetakeAttempt;

public sealed class RetakeAttemptHandler : IRequestHandler<RetakeAttemptCommand, AttemptDto>
{
    private readonly IAssessmentDbContext _context;
    private readonly AttemptAccess _access;
    private readonly AttemptContentReader _contentReader;
    private readonly AttemptDocumentReader _documents;
    private readonly ILogger<RetakeAttemptHandler> _logger;

    public RetakeAttemptHandler(
        IAssessmentDbContext context,
        AttemptAccess access,
        AttemptContentReader contentReader,
        AttemptDocumentReader documents,
        ILogger<RetakeAttemptHandler> logger)
    {
        _context = context;
        _access = access;
        _contentReader = contentReader;
        _documents = documents;
        _logger = logger;
    }

    public async Task<AttemptDto> Handle(RetakeAttemptCommand request, CancellationToken cancellationToken)
    {
        var owner = _access.RequireLearner();
        var scope = request.Scope!.Value;
        var original = await _access.LoadOwnedAsync(request.AttemptId, track: false, cancellationToken);

        if (original.Status != AttemptStatus.Completed)
        {
            throw AttemptAccess.NotCompleted();
        }

        var content = await _contentReader.LoadAsync(original, trackAnswers: false, cancellationToken);
        var selected = Select(content, scope);

        if (selected.Count == 0)
        {
            throw AttemptAccess.NothingToPractice();
        }

        var sourceId = AttemptAccess.SourceId(original);

        return await _context.ExecuteAttemptUpdateAsync(owner, sourceId, async () =>
        {
            var state = await _access.FenceAsync(owner, sourceId, cancellationToken);
            var active = await _access.InProgressFor(owner, sourceId).SingleOrDefaultAsync(cancellationToken);

            if (active is not null)
            {
                await _access.AbandonAsync(active, state, cancellationToken);
            }

            var questions = selected.Select(entry => entry.Question).ToList();
            var passageIds = questions.Where(question => question.PassageId.HasValue)
                .Select(question => question.PassageId!.Value)
                .ToHashSet();
            var document = new AttemptSnapshotDocument(
                content.Document.Set,
                content.Document.Passages.Where(passage => passageIds.Contains(passage.Id)).ToList(),
                content.Source);

            var attempt = AttemptSnapshotFactory.Add(_context, new NewAttempt(
                owner,
                original.TestId,
                original.PracticeSetId,
                AttemptMode.Practice,
                AttemptTitles.ForRetake(original.Title, scope),
                document,
                questions,
                null,
                _access.Now));

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Retake {Scope} of attempt {OriginalAttemptId} started as {AttemptId} with {QuestionCount} questions",
                scope, original.Id, attempt.Id, questions.Count);

            return await _documents.ReadAsync(attempt, cancellationToken);
        }, cancellationToken);
    }

    private static IReadOnlyList<AttemptQuestionEntry> Select(AttemptContent content, RetakeScope scope)
    {
        if (scope == RetakeScope.All)
        {
            return content.Questions;
        }

        var candidates = content.Questions
            .Select(entry => new SelectionCandidate(entry.Row.Id, entry.Question.PassageId))
            .ToList();
        var missed = content.Questions
            .Where(entry => entry.IsCorrect != true)
            .Select(entry => entry.Row.Id)
            .ToHashSet();
        var keep = PracticeSelectionPlanner.ExpandToUnits(candidates, missed).ToHashSet();

        return content.Questions.Where(entry => keep.Contains(entry.Row.Id)).ToList();
    }
}
