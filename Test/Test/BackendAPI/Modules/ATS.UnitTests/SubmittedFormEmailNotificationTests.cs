using ATS.Constants;
using ATS.Data.Entities;
using ATS.Data.Repository;
using ATS.Services.EmailService;
using ATS.Services.OrderHistory;
using Auth.DTO;
using Auth.Shared.Contracts;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace Test.BackendAPI.Modules.ATS.UnitTests;

/// <summary>
/// The completed-form notice: who it is addressed to, who is copied, whose name appears in the
/// body, and that a delivery failure never escapes to the caller.
/// </summary>
/// <remarks>
/// Stops at <see cref="IAtsEmailSender"/>, for the same reason the other two notice tests do: a
/// successful SMTP send cannot be faked without a server. The composed body is asserted in
/// <c>AtsSubmittedFormEmailBodyTests</c>.
/// </remarks>
public class SubmittedFormEmailNotificationTests
{
	// Deliberately literals rather than SubmittedFormEmail.Subject / .CopyTeams. These are the
	// agreed copy, and a test that read the constant would keep passing if the constant were
	// changed - which is the one thing it exists to catch.
	//
	// The first copied mailbox is clientsupport@, not the ccteam@ the body's closing sentence
	// names: the notice is copied to one team and tells the reader to write to another. That
	// mismatch is in the agreed copy (see SubmittedFormEmail), so this literal tracks the
	// recipient only - AtsSubmittedFormEmailBodyTests pins the prose address separately.
	private const string SubmittedSubject = "CIBI | Order Status – In Progress";
	private const string CcTeam = "clientsupport@cibi.com.ph";
	private const string PreWorkTeam = "pre-workteam@cibi.com.ph";
	private const string RequestorEmail = "requestor@cibi.test";
	private const string RequestorName = "Ana Reyes";
	private const string CandidateEmail = "candidate@example.test";
	private const string SubmittedCandidateName = "Juan Dela Cruz";
	private const string EmailBody = "submitted-form-email-body";

	private static readonly Guid InvitationId = Guid.CreateVersion7();
	private static readonly Guid RequestorId = Guid.CreateVersion7();

	private readonly Mock<ILogger<SubmittedFormEmailNotification>> _logger = new();
	private readonly Mock<IAtsEmailSender> _emailSender = new();
	private readonly Mock<IAuthQueries> _authQueries = new();
	private readonly Mock<IATSRepository> _repository = new();
	private readonly Mock<IOrderHistoryService> _orderHistoryService = new();

	private readonly SubmittedFormEmailNotification _notifier;

	public SubmittedFormEmailNotificationTests()
	{
		_notifier = new SubmittedFormEmailNotification(
			_logger.Object,
			_emailSender.Object,
			_authQueries.Object,
			_repository.Object,
			_orderHistoryService.Object);
	}

	/// <summary>
	/// Stands in for the order row the notifier loads by id. Mirrors the repository's real contract,
	/// which returns an EMPTY placeholder rather than null when the id matches nothing.
	/// </summary>
	private EmailInvitationRequest SetupOrder(
		string? candidateEmail = CandidateEmail,
		string firstName = "Stored",
		string lastName = "Name")
	{
		var invitation = new EmailInvitationRequest
		{
			EmailInvitationID = InvitationId,
			RequestorId = RequestorId,
			Requestor = RequestorName,
			EmailAddress = candidateEmail,
			FirstName = firstName,
			LastName = lastName
		};

		_repository
			.Setup(repository => repository.GetEmailInvitationRequestByIdAsync(
				InvitationId,
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(invitation);

		return invitation;
	}

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
			.Setup(sender => sender.BuildSubmittedFormNotification(
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
	/// Two branches rather than one conditional expression: Moq builds an expression tree out of the
	/// lambda, and a tree may not contain an <c>is</c> pattern match.
	/// </summary>
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
				SubmittedSubject,
				EmailBody,
				It.IsAny<CancellationToken>(),
				It.Is<IReadOnlyCollection<string>?>(cc =>
					cc != null && cc.SequenceEqual(expectedCc))),
			times);
	}

	private void VerifyBody(Times times, string expectedCandidateName) =>
		_emailSender.Verify(
			sender => sender.BuildSubmittedFormNotification(
				RequestorName,
				expectedCandidateName),
			times);

	[Fact]
	public async Task SendAsync_ShouldAddressTheRequestorAndCopyBothTeamsAndTheCandidate()
	{
		// Arrange
		SetupOrder();
		SetupRequestorDirectoryEntry();
		SetupComposedBody();
		SetupSuccessfulSend();

		// Act
		await _notifier.SendAsync(
			new SubmittedFormEmailDetails(InvitationId, SubmittedCandidateName),
			CancellationToken.None);

		// Assert: addressed to the requestor, who is the one that can now download the form. Three
		// addresses copied - both CIBI teams, then the candidate, who completed it.
		VerifySend(Times.Once(), [CcTeam, PreWorkTeam, CandidateEmail]);
	}

	[Fact]
	public async Task SendAsync_ShouldNameTheCandidateFromTheSubmittedForm()
	{
		// Arrange: the order row holds a different name, and the one typed on the form has to win -
		// this notice is about what the candidate just submitted.
		SetupOrder(firstName: "Old", lastName: "Typo");
		SetupRequestorDirectoryEntry();
		SetupComposedBody();
		SetupSuccessfulSend();

		// Act
		await _notifier.SendAsync(
			new SubmittedFormEmailDetails(InvitationId, SubmittedCandidateName),
			CancellationToken.None);

		// Assert
		VerifyBody(Times.Once(), SubmittedCandidateName);
	}

	[Fact]
	public async Task SendAsync_ShouldFallBackToTheOrderName_WhenTheFormHasNoName()
	{
		// Arrange
		SetupOrder(firstName: "Stored", lastName: "Name");
		SetupRequestorDirectoryEntry();
		SetupComposedBody();
		SetupSuccessfulSend();

		// Act
		await _notifier.SendAsync(
			new SubmittedFormEmailDetails(InvitationId, "   "),
			CancellationToken.None);

		// Assert
		VerifyBody(Times.Once(), "Stored Name");
	}

	[Fact]
	public async Task SendAsync_ShouldFallBackToTheCandidateAddress_WhenNeitherSourceHasAName()
	{
		// Arrange: the copy reads "Your candidate, <name>, has successfully completed...", so an
		// empty name would leave a hole in the middle of the sentence.
		SetupOrder(firstName: string.Empty, lastName: string.Empty);
		SetupRequestorDirectoryEntry();
		SetupComposedBody();
		SetupSuccessfulSend();

		// Act
		await _notifier.SendAsync(
			new SubmittedFormEmailDetails(InvitationId, null),
			CancellationToken.None);

		// Assert
		VerifyBody(Times.Once(), CandidateEmail);
	}

	[Fact]
	public async Task SendAsync_ShouldCopyOnlyTheTeams_WhenTheOrderHasNoCandidateAddress()
	{
		// Arrange: the requestor is still owed the notice even with nobody to copy.
		SetupOrder(candidateEmail: null);
		SetupRequestorDirectoryEntry();
		SetupComposedBody();
		SetupSuccessfulSend();

		// Act
		await _notifier.SendAsync(
			new SubmittedFormEmailDetails(InvitationId, SubmittedCandidateName),
			CancellationToken.None);

		// Assert
		VerifySend(Times.Once(), [CcTeam, PreWorkTeam]);
	}

	[Fact]
	public async Task SendAsync_ShouldNotSend_WhenTheOrderHasNoRequestorId()
	{
		// Arrange: public-API orders carry no requestor id, so there is nobody to address.
		var invitation = SetupOrder();
		invitation.RequestorId = null;

		// Act
		await _notifier.SendAsync(
			new SubmittedFormEmailDetails(InvitationId, SubmittedCandidateName),
			CancellationToken.None);

		// Assert
		VerifySend(Times.Never());
		_authQueries.Verify(
			queries => queries.GetATSAssignedUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
			Times.Never);
	}

	[Fact]
	public async Task SendAsync_ShouldNotSend_WhenTheOrderCannotBeFound()
	{
		// Arrange: GetEmailInvitationRequestByIdAsync returns an empty placeholder rather than null
		// when the id matches nothing, so a missing order arrives looking like an order with no
		// requestor. This is the case that trap would otherwise turn into a null dereference.
		_repository
			.Setup(repository => repository.GetEmailInvitationRequestByIdAsync(
				InvitationId,
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(new EmailInvitationRequest());

		// Act
		await _notifier.SendAsync(
			new SubmittedFormEmailDetails(InvitationId, SubmittedCandidateName),
			CancellationToken.None);

		// Assert
		VerifySend(Times.Never());
	}

	[Fact]
	public async Task SendAsync_ShouldNotSend_WhenTheRequestorIsNotInTheAtsDirectory()
	{
		// Arrange: the id is present but the lookup finds no ATS-assigned user - the requestor left,
		// or their module grant was removed after the order was raised.
		SetupOrder();
		SetupRequestorDirectoryEntry(userEmail: null);

		// Act
		await _notifier.SendAsync(
			new SubmittedFormEmailDetails(InvitationId, SubmittedCandidateName),
			CancellationToken.None);

		// Assert
		VerifySend(Times.Never());
	}

	[Fact]
	public async Task SendAsync_ShouldNotThrow_WhenTheSenderThrows()
	{
		// Arrange: the submission is already committed, and its caller's catch block deletes the
		// uploaded attachments as compensation - so an exception escaping here would destroy the
		// files belonging to a form that is already saved.
		SetupOrder();
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
		Func<Task> act = () => _notifier.SendAsync(
			new SubmittedFormEmailDetails(InvitationId, SubmittedCandidateName),
			CancellationToken.None);

		// Assert
		await act.Should().NotThrowAsync();
	}

	[Fact]
	public async Task SendAsync_ShouldNotThrow_WhenEverySenderAccountRefuses()
	{
		// Arrange: Throttled means "defer", and there is no queue behind a completion notice.
		SetupOrder();
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
		Func<Task> act = () => _notifier.SendAsync(
			new SubmittedFormEmailDetails(InvitationId, SubmittedCandidateName),
			CancellationToken.None);

		// Assert
		await act.Should().NotThrowAsync();
		VerifySend(Times.Once(), [CcTeam, PreWorkTeam, CandidateEmail]);
	}

	[Fact]
	public async Task SendAsync_ShouldRecordTheNoticeInTheOrderHistory_WhenTheSendIsAttempted()
	{
		// Arrange
		SetupOrder();
		SetupRequestorDirectoryEntry();
		SetupComposedBody();
		SetupSuccessfulSend();

		// Act
		await _notifier.SendAsync(
			new SubmittedFormEmailDetails(InvitationId, SubmittedCandidateName),
			CancellationToken.None);

		// Assert: a second row beside the ApplicationFormSubmitted one the submission itself writes.
		// "The subject completed the form" and "we told the requestor" are different facts, and the
		// second can fail while the first already happened.
		VerifyHistoryRecorded(Times.Once());
	}

	[Fact]
	public async Task SendAsync_ShouldStillRecordTheNotice_WhenDeliveryFails()
	{
		// Arrange: the row records the ATTEMPT, so support can answer "did we try to tell them?"
		// from the timeline alone. Whether it landed is in the log.
		SetupOrder();
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
		await _notifier.SendAsync(
			new SubmittedFormEmailDetails(InvitationId, SubmittedCandidateName),
			CancellationToken.None);

		// Assert
		VerifyHistoryRecorded(Times.Once());
	}

	[Fact]
	public async Task SendAsync_ShouldNotRecordTheNotice_WhenThereIsNobodyToSendTo()
	{
		// Arrange: no send was attempted, so the timeline must not claim one was.
		var invitation = SetupOrder();
		invitation.RequestorId = null;

		// Act
		await _notifier.SendAsync(
			new SubmittedFormEmailDetails(InvitationId, SubmittedCandidateName),
			CancellationToken.None);

		// Assert
		VerifyHistoryRecorded(Times.Never());
		VerifySend(Times.Never());
	}

	// Literals rather than OrderStatus.InProgress, because that class is internal to the ATS
	// assembly and no InternalsVisibleTo reaches the test project. Pinning the stored string is the
	// stronger assertion anyway - it is exactly what the history API returns to the dialog.
	private void VerifyHistoryRecorded(Times times) =>
		_orderHistoryService.Verify(
			history => history.RecordAsync(
				InvitationId,
				OrderHistoryEventType.CompletionNoticeEmail,
				null,
				"In Progress",
				It.IsAny<CancellationToken>(),
				OrderHistorySource.Web),
			times);
}
