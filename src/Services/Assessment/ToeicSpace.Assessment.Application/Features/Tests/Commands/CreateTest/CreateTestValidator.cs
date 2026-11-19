using ToeicSpace.Assessment.Application.Common.Validation;


namespace ToeicSpace.Assessment.Application.Features.Tests.Commands.CreateTest;

public sealed class CreateTestValidator : AbstractValidator<CreateTestCommand>
{
    public CreateTestValidator()
    {
        Include(new TestContentValidator());
    }
}
