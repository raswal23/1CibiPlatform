using ATS.Data.Entities;
using FluentAssertions;
using Moq;
using Test.BackendAPI.Modules.ATS.UnitTests.Fixture;

namespace Test.BackendAPI.Modules.ATS.UnitTests;

public class EmailNotificationProcessorServiceTests : IClassFixture<ATSServiceFixture>
{
	private readonly ATSServiceFixture _fixture;

	public EmailNotificationProcessorServiceTests(ATSServiceFixture fixture)
	{
		_fixture = fixture;

		// The fixture is shared across the class, so clear recorded invocations and
		// setups to keep each test's Verify assertions independent.
		_fixture.MockRepository.Reset();
		_fixture.MockEndorsementSubmissionService.Reset();
	}

	private static EmailInvitationRequest PendingRequest(string email) => new()
	{
		EmailInvitationID = Guid.CreateVersion7(),
		FirstName = "Test",
		LastName = "Candidate",
		EmailAddress = email,
		HashToken = "hash-token",
		EmailSentStatus = "Pending"
	};

	#region Positive Path
	[Fact]
	public async Task ProcessForPendingStatusAsync_ShouldReturn_WhenNoPendingRequests()
	{
		// Arrange
		var service = _fixture.EmailNotificationProcessorService;

		_fixture.MockRepository
			.Setup(x => x.GetPendingEmailInvitationRequestsAsync())
			.ReturnsAsync(new List<EmailInvitationRequest>());

		// Act
		Func<Task> act = async () => await service.ProcessForPendingStatusAsync(CancellationToken.None);

		// Assert
		await act.Should().NotThrowAsync();
		_fixture.MockEndorsementSubmissionService.Verify(
			x => x.SendApplicationFormToUserEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
			Times.Never);
	}

	[Fact]
	public async Task ProcessForPendingStatusAsync_ShouldSendEmail_AndMarkAsSent()
	{
		// Arrange
		var service = _fixture.EmailNotificationProcessorService;
		var pending = new List<EmailInvitationRequest> { PendingRequest("candidate@example.com") };

		_fixture.MockRepository
			.Setup(x => x.GetPendingEmailInvitationRequestsAsync())
			.ReturnsAsync(pending);

		_fixture.MockEndorsementSubmissionService
			.Setup(x => x.SendApplicationFormToUserEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
			.ReturnsAsync(true);

		// Act
		await service.ProcessForPendingStatusAsync(CancellationToken.None);

		// Assert
		_fixture.MockEndorsementSubmissionService.Verify(
			x => x.SendApplicationFormToUserEmailAsync("candidate@example.com", It.IsAny<string>(), It.IsAny<string>()),
			Times.Once);

		_fixture.MockRepository.Verify(
			x => x.UpdateBulkEmailInvitationRequestForSentEmailAsync(
				It.Is<List<EmailInvitationRequest>>(list => list.Count == 1)),
			Times.Once);
	}

	[Fact]
	public async Task ProcessForPendingStatusAsync_ShouldReleaseStaleClaims_BeforeClaimingWork()
	{
		// Arrange
		var service = _fixture.EmailNotificationProcessorService;

		_fixture.MockRepository
			.Setup(x => x.ReleaseStaleEmailInvitationClaimsAsync(It.IsAny<TimeSpan>()))
			.ReturnsAsync(3);

		_fixture.MockRepository
			.Setup(x => x.GetPendingEmailInvitationRequestsAsync())
			.ReturnsAsync(new List<EmailInvitationRequest>());

		// Act
		await service.ProcessForPendingStatusAsync(CancellationToken.None);

		// Assert: rows stranded in Processing by a crashed worker are recovered.
		_fixture.MockRepository.Verify(
			x => x.ReleaseStaleEmailInvitationClaimsAsync(It.Is<TimeSpan>(t => t > TimeSpan.Zero)),
			Times.Once);
	}
	#endregion

	#region Negative Path
	[Fact]
	public async Task ProcessForPendingStatusAsync_ShouldMarkAsError_WhenSendingFails()
	{
		// Arrange
		var service = _fixture.EmailNotificationProcessorService;
		var pending = new List<EmailInvitationRequest> { PendingRequest("broken@example.com") };

		_fixture.MockRepository
			.Setup(x => x.GetPendingEmailInvitationRequestsAsync())
			.ReturnsAsync(pending);

		_fixture.MockEndorsementSubmissionService
			.Setup(x => x.SendApplicationFormToUserEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
			.ThrowsAsync(new InvalidOperationException("SMTP unavailable"));

		// Act
		Func<Task> act = async () => await service.ProcessForPendingStatusAsync(CancellationToken.None);

		// Assert
		await act.Should().NotThrowAsync();
		_fixture.MockRepository.Verify(
			x => x.UpdateBulkEmailInvitationRequestForNotSentEmailAsync(
				It.Is<List<EmailInvitationRequest>>(list => list.Count == 1)),
			Times.Once);
	}

	[Fact]
	public async Task ProcessForPendingStatusAsync_ShouldPropagate_WhenRepositoryFails()
	{
		// Arrange
		var service = _fixture.EmailNotificationProcessorService;

		_fixture.MockRepository
			.Setup(x => x.GetPendingEmailInvitationRequestsAsync())
			.ThrowsAsync(new InvalidOperationException("database unavailable"));

		// Act
		Func<Task> act = async () => await service.ProcessForPendingStatusAsync(CancellationToken.None);

		// Assert: the Quartz job surfaces the failure rather than silently skipping work.
		await act.Should().ThrowAsync<InvalidOperationException>();
	}
	#endregion
}
