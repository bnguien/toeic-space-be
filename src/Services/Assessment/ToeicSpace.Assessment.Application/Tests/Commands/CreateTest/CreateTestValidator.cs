using ToeicSpace.Assessment.Application.Tests.Common;

namespace ToeicSpace.Assessment.Application.Tests.Commands.CreateTest;

public sealed class CreateTestValidator : AbstractValidator<CreateTestCommand>
{
    public CreateTestValidator()
    {
        Include(new TestContentValidator());
    }
}
