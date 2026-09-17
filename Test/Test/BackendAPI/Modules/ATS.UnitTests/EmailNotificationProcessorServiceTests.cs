using ATS.Constants;
using ATS.Data.Entities;
using ATS.Services.EmailAccounts;
using ATS.Services.EmailService;
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

	/// <summary>
	/// Sets the result-aware send used by the processor. The bool overload is only used by
	/// the single-order paths now, so stubbing it would leave the processor's calls
	/// returning a default and every assertion meaningless.
	/// </summary>
	private void SetupSendResult(EmailDeliveryResult result) =>
		_fixture.MockEndorsementSubmissionService
			.Setup(x => x.SendApplicationFormToUserEmailWithResultAsync(
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<int?>(),
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(result);

	/// <summary>
	/// Stubs the send for ANY value of the trailing isFollowUp flag.
	/// </summary>
	/// <remarks>
	/// SetupSendResult above names six arguments, so the optional seventh is baked into the
	/// expression tree as its default - it only matches isFollowUp: false. A test that expects
	/// a reminder would otherwise fall through to an unstubbed call returning null, and fail
	/// somewhere far away from the reason.
	/// </remarks>
	private void SetupSendResultForAnyCopy(EmailDeliveryResult result) =>
		_fixture.MockEndorsementSubmissionService
			.Setup(x => x.SendApplicationFormToUserEmailWithResultAsync(
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<int?>(),
				It.IsAny<CancellationToken>(),
				It.IsAny<bool>()))
			.ReturnsAsync(result);

	private void VerifyCopyUsed(bool expectedIsFollowUp) =>
		_fixture.MockEndorsementSubmissionService.Verify(
			x => x.SendApplicationFormToUserEmailWithResultAsync(
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<int?>(),
				It.IsAny<CancellationToken>(),
				expectedIsFollowUp),
			Times.Once);

	private void VerifySendCount(Times times) =>
		_fixture.MockEndorsementSubmissionService.Verify(
			x => x.SendApplicationFormToUserEmailWithResultAsync(
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<int?>(),
				It.IsAny<CancellationToken>()),
			times);

	#region Positive Path
	[Fact]
	public async Task ProcessForPendingStatusAsync_ShouldReturn_WhenNoPendingRequests()
	{
		// Arrange
		var service = _fixture.EmailNotificationProcessorService;
		_fixture.MockEndorsementSubmissionService.Invocations.Clear();

		_fixture.MockRepository
			.Setup(x => x.GetPendingEmailInvitationRequestsAsync())
			.ReturnsAsync(new List<EmailInvitationRequest>());

		// Act
		Func<Task> act = async () => await service.ProcessForPendingStatusAsync(CancellationToken.None);

		// Assert
		await act.Should().NotThrowAsync();
		VerifySendCount(Times.Never());
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

		SetupSendResult(EmailDeliveryResult.Sent);

		// Act
		await service.ProcessForPendingStatusAsync(CancellationToken.None);

		// Assert
		_fixture.MockEndorsementSubmissionService.Verify(
			x => x.SendApplicationFormToUserEmailWithResultAsync(
				"candidate@example.com",
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<int?>(),
				It.IsAny<CancellationToken>()),
			Times.Once);

		_fixture.MockRepository.Verify(
			x => x.UpdateBulkEmailInvitationRequestForSentEmailAsync(
				It.Is<List<EmailInvitationRequest>>(list => list.Count == 1)),
			Times.Once);
	}

	#region Reminder vs. first-invitation copy

	/// <summary>
	/// LastFollowUpSentDate paired with a null EmailSentAt is what selects reminder wording.
	/// </summary>
	/// <remarks>
	/// Worth pinning because the failure is silent and lands in a candidate's inbox: the send
	/// succeeds either way, and the only symptom is a reminder that reads like a first
	/// invitation ("here is your form") or a first invitation that reads like a reminder ("we
	/// have not yet received your form") for someone who was never chased.
	/// </remarks>
	[Fact]
	public async Task ProcessForPendingStatusAsync_ShouldUseReminderCopy_WhenTheChaserReleasedTheRow()
	{
		// Arrange
		var service = _fixture.EmailNotificationProcessorService;

		var chased = PendingRequest("chased@example.com");
		// What the release UPDATE leaves behind: a date stamped, EmailSentAt cleared.
		chased.LastFollowUpSentDate = DateOnly.FromDateTime(DateTime.UtcNow);
		chased.EmailSentAt = null;

		_fixture.MockRepository
			.Setup(x => x.GetPendingEmailInvitationRequestsAsync())
			.ReturnsAsync(new List<EmailInvitationRequest> { chased });

		SetupSendResultForAnyCopy(EmailDeliveryResult.Sent);

		// Act
		await service.ProcessForPendingStatusAsync(CancellationToken.None);

		// Assert
		VerifyCopyUsed(expectedIsFollowUp: true);
	}

	// A never-chased order gets first-invitation wording. This is the case the retired
	// FollowUpQueuedAt got wrong: its migration backfilled every pre-existing row, so a legacy
	// order resent by an operator claimed to be a reminder.
	[Fact]
	public async Task ProcessForPendingStatusAsync_ShouldUseFirstInvitationCopy_WhenTheOrderWasNeverChased()
	{
		// Arrange
		var service = _fixture.EmailNotificationProcessorService;

		var neverChased = PendingRequest("new@example.com");
		neverChased.LastFollowUpSentDate = null;
		neverChased.EmailSentAt = null;

		_fixture.MockRepository
			.Setup(x => x.GetPendingEmailInvitationRequestsAsync())
			.ReturnsAsync(new List<EmailInvitationRequest> { neverChased });

		SetupSendResultForAnyCopy(EmailDeliveryResult.Sent);

		// Act
		await service.ProcessForPendingStatusAsync(CancellationToken.None);

		// Assert
		VerifyCopyUsed(expectedIsFollowUp: false);
	}

	// The date alone is not enough. Once a reminder has actually been delivered the row carries
	// both a date and an EmailSentAt, and a later send must not keep claiming to be that
	// reminder.
	[Fact]
	public async Task ProcessForPendingStatusAsync_ShouldUseFirstInvitationCopy_WhenTheReminderWasAlreadyDelivered()
	{
		// Arrange
		var service = _fixture.EmailNotificationProcessorService;

		var alreadyDelivered = PendingRequest("delivered@example.com");
		alreadyDelivered.LastFollowUpSentDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
		alreadyDelivered.EmailSentAt = DateTime.UtcNow.AddDays(-1);

		_fixture.MockRepository
			.Setup(x => x.GetPendingEmailInvitationRequestsAsync())
			.ReturnsAsync(new List<EmailInvitationRequest> { alreadyDelivered });

		SetupSendResultForAnyCopy(EmailDeliveryResult.Sent);

		// Act
		await service.ProcessForPendingStatusAsync(CancellationToken.None);

		// Assert
		VerifyCopyUsed(expectedIsFollowUp: false);
	}

	#endregion

	// Every delivery is recorded, so "when did this candidate actually get their email?" is
	// answerable from the order's history rather than from a log file.
	[Fact]
	public async Task ProcessForPendingStatusAsync_ShouldRecordHistory_ForEverySentEmail()
	{
		// Arrange
		var service = _fixture.EmailNotificationProcessorService;
		_fixture.MockOrderHistoryService.Invocations.Clear();

		var first = PendingRequest("first@example.com");
		var second = PendingRequest("second@example.com");

		_fixture.MockRepository
			.Setup(x => x.GetPendingEmailInvitationRequestsAsync())
			.ReturnsAsync(new List<EmailInvitationRequest> { first, second });

		SetupSendResult(EmailDeliveryResult.Sent);

		// Act
		await service.ProcessForPendingStatusAsync(CancellationToken.None);

		// Assert: one call carrying both ids, not one call per row - a pass can hold 200.
		_fixture.MockOrderHistoryService.Verify(
			x => x.RecordManyAsync(
				It.Is<IReadOnlyCollection<Guid>>(ids =>
					ids.Count == 2
					&& ids.Contains(first.EmailInvitationID)
					&& ids.Contains(second.EmailInvitationID)),
				OrderHistoryEventType.InvitationEmailSent,
				null,
				// ATS.Constants.OrderStatus is internal, so the literal - same as the other
				// tests in this suite.
				"Pending Candidate Info",
				It.IsAny<CancellationToken>(),
				OrderHistorySource.System,
				It.IsAny<Guid?>()),
			Times.Once);
	}

	// The status and its history entry are one fact. Writing the status without the history
	// would leave an order that says Done with nothing to say when - and nothing revisits a
	// Done row to notice.
	[Fact]
	public async Task ProcessForPendingStatusAsync_ShouldWriteTheSentStatusAndItsHistory_InOneTransaction()
	{
		// Arrange
		var service = _fixture.EmailNotificationProcessorService;
		_fixture.MockUnitOfWork.Invocations.Clear();

		_fixture.MockRepository
			.Setup(x => x.GetPendingEmailInvitationRequestsAsync())
			.ReturnsAsync(new List<EmailInvitationRequest> { PendingRequest("candidate@example.com") });

		SetupSendResult(EmailDeliveryResult.Sent);

		// Act
		await service.ProcessForPendingStatusAsync(CancellationToken.None);

		// Assert
		_fixture.MockUnitOfWork.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
		_fixture.MockUnitOfWork.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
		_fixture.MockUnitOfWork.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
	}

	// Nothing was delivered, so there is nothing to record and no reason to open a
	// transaction at all.
	[Fact]
	public async Task ProcessForPendingStatusAsync_ShouldNotRecordHistory_WhenEverySendFailed()
	{
		// Arrange
		var service = _fixture.EmailNotificationProcessorService;
		_fixture.MockOrderHistoryService.Invocations.Clear();
		_fixture.MockUnitOfWork.Invocations.Clear();

		_fixture.MockRepository
			.Setup(x => x.GetPendingEmailInvitationRequestsAsync())
			.ReturnsAsync(new List<EmailInvitationRequest> { PendingRequest("candidate@example.com") });

		SetupSendResult(EmailDeliveryResult.Permanent("550", "No such user here"));

		// Act
		await service.ProcessForPendingStatusAsync(CancellationToken.None);

		// Assert
		_fixture.MockOrderHistoryService.Verify(
			x => x.RecordManyAsync(
				It.IsAny<IReadOnlyCollection<Guid>>(),
				It.IsAny<string>(),
				It.IsAny<string?>(),
				It.IsAny<string>(),
				It.IsAny<CancellationToken>(),
				It.IsAny<string>(),
				It.IsAny<Guid?>()),
			Times.Never);

		_fixture.MockUnitOfWork.Verify(
			x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()),
			Times.Never);
	}

	[Fact]
	public async Task ProcessForPendingStatusAsync_ShouldReleaseStaleClaims_BeforeClaimingWork()
	{
		// Arrange
		var service = _fixture.EmailNotificationProcessorService;
		_fixture.MockEndorsementSubmissionService.Invocations.Clear();

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
			.Setup(x => x.SendApplicationFormToUserEmailWithResultAsync(
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<int?>(),
				It.IsAny<CancellationToken>()))
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
	public async Task ProcessForPendingStatusAsync_ShouldNotRetry_WhenRejectionIsPermanent()
	{
		// Arrange: the server refused the mailbox outright.
		var service = _fixture.EmailNotificationProcessorService;
		var pending = new List<EmailInvitationRequest> { PendingRequest("nobody@example.com") };

		_fixture.MockRepository
			.Setup(x => x.GetPendingEmailInvitationRequestsAsync())
			.ReturnsAsync(pending);

		SetupSendResult(EmailDeliveryResult.Permanent("550", "No such user here"));

		// Act
		await service.ProcessForPendingStatusAsync(CancellationToken.None);

		// Assert: exactly one attempt. Retrying a 550 produces the identical refusal and
		// spends a budget that a genuinely transient address needs.
		VerifySendCount(Times.Once());

		_fixture.MockRepository.Verify(
			x => x.UpdateBulkEmailInvitationRequestForNotSentEmailAsync(
				It.Is<List<EmailInvitationRequest>>(list => list.Count == 1)),
			Times.Once);
	}

	[Fact]
	public async Task ProcessForPendingStatusAsync_ShouldRetryTransientFailures_UpToTheConfiguredLimit()
	{
		// Arrange
		var service = _fixture.EmailNotificationProcessorService;
		var pending = new List<EmailInvitationRequest> { PendingRequest("flaky@example.com") };

		_fixture.MockRepository
			.Setup(x => x.GetPendingEmailInvitationRequestsAsync())
			.ReturnsAsync(pending);

		SetupSendResult(EmailDeliveryResult.Transient("451", "Temporary local problem"));

		// Act
		await service.ProcessForPendingStatusAsync(CancellationToken.None);

		// Assert: a transient fault is worth retrying, but only to the configured ceiling.
		VerifySendCount(Times.Exactly(_fixture.EmailDeliveryOptions.MaxAttemptsPerPass));
	}

	[Fact]
	public async Task ProcessForPendingStatusAsync_ShouldDeferWithoutChargingAnAttempt_WhenProviderThrottles()
	{
		// Arrange: this is the incident. The provider answered "try again later", and the
		// old code kept knocking until every row had spent its budget.
		var service = new ATSServiceFixture();

		try
		{
			var pending = new List<EmailInvitationRequest>
			{
				PendingRequest("first@example.com"),
				PendingRequest("second@example.com"),
				PendingRequest("third@example.com")
			};

			service.MockRepository
				.Setup(x => x.GetPendingEmailInvitationRequestsAsync())
				.ReturnsAsync(pending);

			service.MockEndorsementSubmissionService
				.Setup(x => x.SendApplicationFormToUserEmailWithResultAsync(
					It.IsAny<string>(),
					It.IsAny<string>(),
					It.IsAny<string>(),
					It.IsAny<string>(),
					It.IsAny<int?>(),
					It.IsAny<CancellationToken>()))
				.ReturnsAsync(EmailDeliveryResult.Throttled("421", "4.7.0 Try again later"));

			// Act
			await service.EmailNotificationProcessorService
				.ProcessForPendingStatusAsync(CancellationToken.None);

			// Assert: the rows go back to Pending with their attempt count intact, so a
			// throttle can never retire a valid address.
			service.MockRepository.Verify(
				x => x.ReleaseEmailInvitationClaimsAsync(
					It.Is<List<EmailInvitationRequest>>(list => list.Count > 0)),
				Times.Once);

			// And they are NOT recorded as delivery failures.
			service.MockRepository.Verify(
				x => x.UpdateBulkEmailInvitationRequestForNotSentEmailAsync(
					It.IsAny<List<EmailInvitationRequest>>()),
				Times.Never);

			// And somebody is told. A capped sender defers rows silently and correctly - the
			// queue looks calm and invitations simply stop - so the notification is the only
			// thing that makes the outage visible.
			service.MockNotificationService.Verify(
				x => x.RaiseAsync(
					It.IsAny<Guid>(),
					AtsNotificationType.EmailAccountsExhausted,
					It.IsAny<string>(),
					It.IsAny<string>(),
					It.IsAny<string>(),
					It.IsAny<Guid?>(),
					It.IsAny<CancellationToken>()),
				Times.AtLeastOnce);
		}
		finally
		{
			service.Dispose();
		}
	}

	[Fact]
	public async Task ProcessForPendingStatusAsync_ShouldSkipTheWholePass_WhenEveryAccountIsUnavailable()
	{
		// Arrange: every registered account is capped, cooling down or unverified. Note this
		// now takes ALL of them - one throttled account no longer stops the pass, because the
		// switcher moves to the next one.
		var fixture = new ATSServiceFixture();

		try
		{
			fixture.MockPoolRegistry
				.Setup(x => x.GetNextSendableAccountAsync(
					It.IsAny<IReadOnlyCollection<int>>(),
					It.IsAny<CancellationToken>()))
				.ReturnsAsync((AtsEmailAccountSnapshot?)null);

			// Act
			await fixture.EmailNotificationProcessorService
				.ProcessForPendingStatusAsync(CancellationToken.None);

			// Assert: no rows are even claimed. Claiming them would hold them out of every
			// other worker's reach purely to park them behind the back-off.
			fixture.MockRepository.Verify(
				x => x.GetPendingEmailInvitationRequestsAsync(),
				Times.Never);
		}
		finally
		{
			fixture.Dispose();
		}
	}

	[Fact]
	public async Task ProcessForPendingStatusAsync_ShouldStillRunThePass_WhenOnlySomeAccountsAreUnavailable()
	{
		// Arrange: this is the whole point of the feature. The highest-priority account is
		// capped, but a lower-priority one is healthy - so the pass must go ahead rather than
		// standing down as it did when there was a single sender.
		var fixture = new ATSServiceFixture();

		try
		{
			var secondary = ATSServiceFixture.HealthyAccount with
			{
				AtsEmailAccountId = 2,
				EmailAddress = "ats-secondary@example.com",
				Priority = 2
			};

			fixture.MockPoolRegistry
				.Setup(x => x.GetNextSendableAccountAsync(
					It.IsAny<IReadOnlyCollection<int>>(),
					It.IsAny<CancellationToken>()))
				.ReturnsAsync(secondary);

			fixture.MockRepository
				.Setup(x => x.GetPendingEmailInvitationRequestsAsync())
				.ReturnsAsync(new List<EmailInvitationRequest> { PendingRequest("first@example.com") });

			fixture.MockEndorsementSubmissionService
				.Setup(x => x.SendApplicationFormToUserEmailWithResultAsync(
					It.IsAny<string>(),
					It.IsAny<string>(),
					It.IsAny<string>(),
					It.IsAny<string>(),
					It.IsAny<int?>(),
					It.IsAny<CancellationToken>()))
				.ReturnsAsync(EmailDeliveryResult.Sent);

			// Act
			await fixture.EmailNotificationProcessorService
				.ProcessForPendingStatusAsync(CancellationToken.None);

			// Assert: rows were claimed and sent through the surviving account.
			fixture.MockRepository.Verify(
				x => x.GetPendingEmailInvitationRequestsAsync(),
				Times.Once);

			fixture.MockRepository.Verify(
				x => x.UpdateBulkEmailInvitationRequestForSentEmailAsync(
					It.Is<List<EmailInvitationRequest>>(list => list.Count == 1)),
				Times.Once);
		}
		finally
		{
			fixture.Dispose();
		}
	}

	[Fact]
	public async Task ProcessForPendingStatusAsync_ShouldFailImmediately_WhenAddressIsMissing()
	{
		// Arrange
		var service = _fixture.EmailNotificationProcessorService;
		var pending = new List<EmailInvitationRequest> { PendingRequest("   ") };

		_fixture.MockRepository
			.Setup(x => x.GetPendingEmailInvitationRequestsAsync())
			.ReturnsAsync(pending);

		// Act
		await service.ProcessForPendingStatusAsync(CancellationToken.None);

		// Assert: a row with no address can never be sent, so it never reaches the sender
		// and is not retried.
		VerifySendCount(Times.Never());

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
