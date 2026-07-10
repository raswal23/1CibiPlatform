using ATS.Features.ResendApplicationForm;
using FluentAssertions;
using Moq;
using Test.BackendAPI.Modules.ATS.UnitTests.Fixture;

namespace Test.BackendAPI.Modules.ATS.UnitTests;

public class ResendApplicationFormHandlerTests
{
	private readonly ATSServiceFixture _fixture;

	public ResendApplicationFormHandlerTests()
	{
		_fixture = new ATSServiceFixture();
	}

	#region Positive Path
	[Fact]
	public async Task Handle_ShouldReturnSuccess_WhenServiceReturnsTrue()
	{
		// Arrange
		var handler = new ResendApplicationFormCommandHandler(_fixture.MockEndorsementSubmissionService.Object);
		var id = Guid.NewGuid();
		_fixture.MockEndorsementSubmissionService
			.Setup(s => s.ResendApplicationFormAsync(id, It.IsAny<CancellationToken>()))
			.ReturnsAsync(true);

		var command = new ResendApplicationFormCommand(id);

		// Act
		var result = await handler.Handle(command, CancellationToken.None);

		// Assert
		result.Should().NotBeNull();
		result.Success.Should().BeTrue();
	}
	#endregion

	#region Negative Path
	[Fact]
	public async Task Handle_ShouldThrowNotFoundException_WhenServiceThrowsNotFound()
	{
		// Arrange
		var handler = new ResendApplicationFormCommandHandler(_fixture.MockEndorsementSubmissionService.Object);
		var id = Guid.NewGuid();
		_fixture.MockEndorsementSubmissionService
			.Setup(s => s.ResendApplicationFormAsync(id, It.IsAny<CancellationToken>()))
			.ThrowsAsync(new KeyNotFoundException("not found"));

		var command = new ResendApplicationFormCommand(id);

		// Act
		Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

		// Assert
		await act.Should().ThrowAsync<KeyNotFoundException>();
	}
	#endregion
}
