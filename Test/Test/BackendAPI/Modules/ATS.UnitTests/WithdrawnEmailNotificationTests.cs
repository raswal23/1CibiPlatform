using ATS.Configuration;
using ATS.Constants;
using ATS.Data.Entities;
using ATS.Services.EmailService;
using ATS.Services.OrderHistory;
using Auth.DTO;
using Auth.Shared.Contracts;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Test.BackendAPI.Modules.ATS.UnitTests;

/// <summary>
/// The withdrawal notice itself: who it is addressed to, who is copied, whose name appears in the
/// greeting, and - the part with teeth - that a delivery failure never escapes to the caller.
/// </summary>
/// <remarks>
/// Stops at <see cref="IAtsEmailSender"/>. A successful SMTP send cannot be faked without a server -
/// <c>SmtpLease</c> wraps a real <c>SmtpClient</c> - which is also why <c>AtsEmailFailoverTests</c>
/// injects its failures through <c>GetContextAsync</c>. The composed body is asserted separately in
/// <c>AtsWithdrawnEmailBodyTests</c>, since that is a pure function.
/// </remarks>
public class WithdrawnEmailNotificationTests
{
	// Deliberately literals rather than WithdrawnEmail.Subject / .CopyTeam. These two are the
	// agreed copy, and a test that read the constant would keep passing if the constant were
	// changed - which is the one thing it exists to catch.
	//
	// The copied mailbox is clientsupport@, not the ccteam@ the body's closing sentence names:
	// the notice is copied to one team and tells the reader to write to another. Both sides are
	// deliberate, so this literal tracks the recipient only - AtsWithdrawnEmailBodyTests pins the
	// prose address separately.
	private const string WithdrawnSubject = "Order Status â€“ Withdrawn";
	private const string CcTeam = "clientsupport@cibi.com.ph";
	private const string RequestorEmail = "requestor@cibi.test";
	private const string RequestorName = "Ana Reyes";
	private const string CandidateEmail = "candidate@example.test";
	private const string CandidateFirstName = "Juan";
	private const string CandidateLastName = "Dela Cruz";
	private const string EmailBody = "withdrawn-email-body";

	/// <summary>The attempt budget these tests configure, so an assertion can name it rather than 3.</summary>
	private const int MaxAttempts = 3;

	private static readonly Guid InvitationId = Guid.CreateVersion7();
	private static readonly Guid RequestorId = Guid.CreateVersion7();

	private readonly Mock<ILogger<WithdrawnEmailNotification>> _logger = new();
	private readonly Mock<IAtsEmailSender> _emailSender = new();
	private readonly Mock<IAuthQueries> _authQueries = new();
	private readonly Mock<IOrderHistoryService> _orderHistoryService = new();

	private readonly WithdrawnEmailNotification _notifier;

	public WithdrawnEmailNotificationTests()
	{
		// Zero back-off. The production default sleeps 2s then 4s between attempts, and a test that
		// drives a failing send must not spend six seconds proving the count.
		_notifier = new WithdrawnEmailNotification(
			_logger.Object,
			_emailSender.Object,
			_authQueries.Object,
			_orderHistoryService.Object,
			Options.Create(new AtsEmailDeliveryOptions
			{
				MaxAttemptsPerMessage = MaxAttempts,
				RetryBaseDelaySeconds = 0
			}));
	}

	private static EmailInvitationRequest CreateInvitation() => new()
	{
		EmailInvitationID = InvitationId,
		RequestorId = RequestorId,
		Requestor = RequestorName,
		EmailAddress = CandidateEmail,
		FirstName = CandidateFirstName,
		LastName = CandidateLastName
	};

	/// <summary>
	/// The requestor's mailbox is not on the order - only an Auth user id - so the notice depends on
	/// this directory lookup. A null email stands for "no ATS-assigned user found".
	/// </summary>
	private void SetupRequestorDirectoryEntry(string? userEmail = RequestorEmail, string userName = RequestorName)
	{
		_authQueries
			.Setup(queries => queries.GetATSAssignedUserAsync(
				RequestorId,
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(userEmail is null
				? null
				: new ATSUserLookupDTO
				{
					UserId = RequestorId,
					UserName = userName,
					UserEmail = userEmail
				});
	}

	private void SetupComposedBody() =>
		_emailSender
			.Setup(sender => sender.BuildWithdrawnApplicationNotification(
				It.IsAny<string>(),
				It.IsAny<string>()))
			.Returns(EmailBody);

	private void SetupSuccessfulSend() =>
		_emailSender
			.Setup(sender => sender.SendATSEmailWithResultAsync(
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<CancellationToken>(),
				It.IsAny<IReadOnlyCollection<string>?>()))
			.ReturnsAsync(EmailDeliveryResult.Sent);

	/// <summary>
	/// Verifies the send. Passing no expected copy asserts only whether it happened at all, so the
	/// skip cases do not have to restate an address they never reached.
	/// </summary>
	/// <remarks>
	/// Two branches rather than one conditional expression: Moq builds an expression tree out of the
	/// lambda, and a tree may not contain an <c>is</c> pattern match.
	/// </remarks>
	private void VerifySend(Times times, IReadOnlyCollection<string>? expectedCc = null)
	{
		if (expectedCc is null)
		{
			_emailSender.Verify(
				sender => sender.SendATSEmailWithResultAsync(
					It.IsAny<string>(),
					It.IsAny<string>(),
					It.IsAny<string>(),
					It.IsAny<CancellationToken>(),
					It.IsAny<IReadOnlyCollection<string>?>()),
				times);

			return;
		}

		_emailSender.Verify(
			sender => sender.SendATSEmailWithResultAsync(
				RequestorEmail,
				WithdrawnSubject,
				EmailBody,
				It.IsAny<CancellationToken>(),
				It.Is<IReadOnlyCollection<string>?>(cc =>
					cc != null && cc.SequenceEqual(expectedCc))),
			times);
	}

	[Fact]
	public async Task SendAsync_ShouldAddressTheRequestorAndCopyTheCandidateAndCcTeam()
	{
		// Arrange
		SetupRequestorDirectoryEntry();
		SetupComposedBody();
		SetupSuccessfulSend();

		// Act
		await _notifier.SendAsync(CreateInvitation(), CancellationToken.None);

		// Assert: addressed to the requestor, who is the one that has to stop the verification. The
		// candidate is copied rather than addressed - they are the one who pressed Withdraw.
		VerifySend(Times.Once(), [CcTeam, CandidateEmail]);
	}

	[Fact]
	public async Task SendAsync_ShouldGreetTheRequestorAndNameTheCandidateFromTheOrder()
	{
		// Arrange
		SetupRequestorDirectoryEntry();
		SetupComposedBody();
		SetupSuccessfulSend();

		// Act
		await _notifier.SendAsync(CreateInvitation(), CancellationToken.None);

		// Assert
		_emailSender.Verify(
			sender => sender.BuildWithdrawnApplicationNotification(
				RequestorName,
				$"{CandidateFirstName} {CandidateLastName}"),
			Times.Once);
	}

	[Fact]
	public async Task SendAsync_ShouldFallBackToTheStoredRequestorName_WhenTheDirectoryHasNone()
	{
		// Arrange: the directory row can carry an empty joined name for older users, and the order
		// already stores the name the requestor was shown when they raised it.
		const string storedName = "A. Reyes";
		var invitation = CreateInvitation();
		invitation.Requestor = storedName;
		SetupRequestorDirectoryEntry(userName: string.Empty);
		SetupComposedBody();
		SetupSuccessfulSend();

		// Act
		await _notifier.SendAsync(invitation, CancellationToken.None);

		// Assert
		_emailSender.Verify(
			sender => sender.BuildWithdrawnApplicationNotification(
				storedName,
				It.IsAny<string>()),
			Times.Once);
	}

	[Fact]
	public async Task SendAsync_ShouldFallBackToTheCandidateAddress_WhenTheOrderHasNoCandidateName()
	{
		// Arrange: the copy reads "Your candidate, <name>, has withdrawn", so an order with no name
		// parts still has to identify someone.
		var invitation = CreateInvitation();
		invitation.FirstName = null;
		invitation.LastName = null;
		SetupRequestorDirectoryEntry();
		SetupComposedBody();
		SetupSuccessfulSend();

		// Act
		await _notifier.SendAsync(invitation, CancellationToken.None);

		// Assert
		_emailSender.Verify(
			sender => sender.BuildWithdrawnApplicationNotification(
				RequestorName,
				CandidateEmail),
			Times.Once);
	}

	[Fact]
	public async Task SendAsync_ShouldCopyOnlyTheCcTeam_WhenTheOrderHasNoCandidateAddress()
	{
		// Arrange: the requestor is still owed the notice even with nobody to copy.
		var invitation = CreateInvitation();
		invitation.EmailAddress = null;
		SetupRequestorDirectoryEntry();
		SetupComposedBody();
		SetupSuccessfulSend();

		// Act
		await _notifier.SendAsync(invitation, CancellationToken.None);

		// Assert
		VerifySend(Times.Once(), [CcTeam]);
	}

	[Fact]
	public async Task SendAsync_ShouldNotSend_WhenTheOrderHasNoRequestorId()
	{
		// Arrange: public-API orders carry no requestor id, so there is nobody to address.
		var invitation = CreateInvitation();
		invitation.RequestorId = null;

		// Act
		await _notifier.SendAsync(invitation, CancellationToken.None);

		// Assert
		VerifySend(Times.Never());
		_authQueries.Verify(
			queries => queries.GetATSAssignedUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
			Times.Never);
	}

	[Fact]
	public async Task SendAsync_ShouldNotSend_WhenTheRequestorIsNotInTheAtsDirectory()
	{
		// Arrange: the id is present but the lookup finds no ATS-assigned user - the requestor left,
		// or their module grant was removed after the order was raised.
		SetupRequestorDirectoryEntry(userEmail: null);

		// Act
		await _notifier.SendAsync(CreateInvitation(), CancellationToken.None);

		// Assert
		VerifySend(Times.Never());
	}

	[Fact]
	public async Task SendAsync_ShouldNotThrow_WhenTheSenderThrows()
	{
		// Arrange: the caller has already committed the withdrawal, so a dead SMTP account must not
		// reach CustomExceptionHandler - that would answer a successful withdrawal with a 500.
		SetupRequestorDirectoryEntry();
		SetupComposedBody();
		_emailSender
			.Setup(sender => sender.SendATSEmailWithResultAsync(
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<CancellationToken>(),
				It.IsAny<IReadOnlyCollection<string>?>()))
			.ThrowsAsync(new InvalidOperationException("SMTP is unreachable."));

		// Act
		Func<Task> act = () => _notifier.SendAsync(CreateInvitation(), CancellationToken.None);

		// Assert
		await act.Should().NotThrowAsync();
	}

	[Fact]
	public async Task SendAsync_ShouldNotThrow_WhenEverySenderAccountRefuses()
	{
		// Arrange: Throttled is the sender saying "defer", not a fault to surface. There is no queue
		// behind a withdrawal, so the outcome is logged and dropped.
		SetupRequestorDirectoryEntry();
		SetupComposedBody();
		_emailSender
			.Setup(sender => sender.SendATSEmailWithResultAsync(
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<CancellationToken>(),
				It.IsAny<IReadOnlyCollection<string>?>()))
			.ReturnsAsync(EmailDeliveryResult.Throttled(null, "Every registered sender account is capped."));

		// Act
		Func<Task> act = () => _notifier.SendAsync(CreateInvitation(), CancellationToken.None);

		// Assert
		await act.Should().NotThrowAsync();
		VerifySend(Times.Once(), [CcTeam, CandidateEmail]);
	}

	[Fact]
	public async Task SendAsync_ShouldRecordTheNoticeInTheOrderHistory_WhenTheSendIsAttempted()
	{
		// Arrange
		SetupRequestorDirectoryEntry();
		SetupComposedBody();
		SetupSuccessfulSend();

		// Act
		await _notifier.SendAsync(CreateInvitation(), CancellationToken.None);

		// Assert: a second row beside the ApplicationFormWithdrawn one the withdrawal itself writes.
		// "The subject withdrew" and "we told the requestor" are different facts, and the second can
		// fail while the first already happened.
		VerifyHistoryRecorded(Times.Once());
	}

	[Fact]
	public async Task SendAsync_ShouldStillRecordTheNotice_WhenDeliveryFails()
	{
		// Arrange: the row records the ATTEMPT, so support can answer "did we try to tell them?"
		// from the timeline alone. Whether it landed is in the log.
		SetupRequestorDirectoryEntry();
		SetupComposedBody();
		_emailSender
			.Setup(sender => sender.SendATSEmailWithResultAsync(
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<CancellationToken>(),
				It.IsAny<IReadOnlyCollection<string>?>()))
			.ReturnsAsync(EmailDeliveryResult.Throttled(null, "Every registered sender account is capped."));

		// Act
		await _notifier.SendAsync(CreateInvitation(), CancellationToken.None);

		// Assert
		VerifyHistoryRecorded(Times.Once());
	}

	[Fact]
	public async Task SendAsync_ShouldNotRecordTheNotice_WhenThereIsNobodyToSendTo()
	{
		// Arrange: no send was attempted, so the timeline must not claim one was.
		SetupRequestorDirectoryEntry(userEmail: null);

		// Act
		await _notifier.SendAsync(CreateInvitation(), CancellationToken.None);

		// Assert
		VerifyHistoryRecorded(Times.Never());
		VerifySend(Times.Never());
	}

	// Literals rather than OrderStatus.ApplicationWithdrawn, because that class is internal to the
	// ATS assembly and no InternalsVisibleTo reaches the test project. Pinning the stored string is
	// the stronger assertion anyway - it is exactly what the history API returns to the dialog.
	private void VerifyHistoryRecorded(Times times) =>
		_orderHistoryService.Verify(
			history => history.RecordAsync(
				InvitationId,
				OrderHistoryEventType.WithdrawalNoticeEmail,
				null,
				"Application Withdrawn",
				It.IsAny<CancellationToken>(),
				OrderHistorySource.Web),
			times);
}
