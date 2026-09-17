using ATS.Constants;
using ATS.Services.EmailService;
using ATS.Services.OrderHistory;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace Test.BackendAPI.Modules.ATS.UnitTests;

/// <summary>
/// The dispute acknowledgement: who it goes to, which of the two body lines it renders, and that a
/// delivery failure never escapes to the caller.
/// </summary>
/// <remarks>
/// Stops at <see cref="IAtsEmailSender"/>, for the same reason <c>WithdrawnEmailNotificationTests</c>
/// does: a successful SMTP send cannot be faked without a server. The composed body is asserted in
/// <c>AtsDisputeEmailBodyTests</c>.
/// </remarks>
public class DisputeEmailNotificationTests
{
	// Deliberately literals rather than DisputeEmail.Subject / .CopyTeam. These are the agreed
	// copy, and a test that read the constant would keep passing if the constant were changed -
	// which is the one thing it exists to catch.
	private const string DisputeSubject = "CIBI | Order Dispute";
	private const string CopyTeam = "clientsupport@cibi.com.ph";
	private const string FilerEmail = "requestor@cibi.test";
	private const string FilerName = "Ana Reyes";
	private const string CandidateName = "Ada Lovelace";
	private const string EmailBody = "dispute-email-body";

	private static readonly Guid InvitationId = Guid.CreateVersion7();

	private readonly Mock<ILogger<DisputeEmailNotification>> _logger = new();
	private readonly Mock<IAtsEmailSender> _emailSender = new();
	private readonly Mock<IOrderHistoryService> _orderHistoryService = new();

	private readonly DisputeEmailNotification _notifier;

	public DisputeEmailNotificationTests()
	{
		_notifier = new DisputeEmailNotification(
			_logger.Object,
			_emailSender.Object,
			_orderHistoryService.Object);
	}

	private static DisputeEmailDetails CreateDetails(
		string? requestorEmail = FilerEmail,
		string? requestorName = FilerName,
		string? disputeCategory = "Report",
		string? disputeReason = "The employment dates on the report are wrong.") => new(
			InvitationId,
			requestorEmail,
			requestorName,
			CandidateName,
			disputeCategory,
			disputeReason ?? string.Empty);

	private void SetupComposedBody() =>
		_emailSender
			.Setup(sender => sender.BuildDisputeNotification(
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<string?>()))
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

	private void VerifySend(Times times) =>
		_emailSender.Verify(
			sender => sender.SendATSEmailWithResultAsync(
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<CancellationToken>(),
				It.IsAny<IReadOnlyCollection<string>?>()),
			times);

	private void VerifySendTo(Times times, IReadOnlyCollection<string>? expectedCc = null)
	{
		if (expectedCc is null)
		{
			VerifySend(times);

			return;
		}

		_emailSender.Verify(
			sender => sender.SendATSEmailWithResultAsync(
				FilerEmail,
				DisputeSubject,
				EmailBody,
				It.IsAny<CancellationToken>(),
				It.Is<IReadOnlyCollection<string>?>(cc =>
					cc != null && cc.SequenceEqual(expectedCc))),
			times);
	}

	private void VerifyBody(Times times, string expectedCategory, string? expectedDetails) =>
		_emailSender.Verify(
			sender => sender.BuildDisputeNotification(
				It.IsAny<string>(),
				It.IsAny<string>(),
				expectedCategory,
				expectedDetails),
			times);

	[Fact]
	public async Task SendAsync_ShouldAddressTheFilerAndCopyClientSupport()
	{
		// Arrange
		SetupComposedBody();
		SetupSuccessfulSend();

		// Act
		await _notifier.SendAsync(CreateDetails(), CancellationToken.None);

		// Assert: one recipient copied, not two - the body's closing sentence names ccteam as well,
		// but only client support is actually on the message.
		VerifySendTo(Times.Once(), [CopyTeam]);
	}

	[Fact]
	public async Task SendAsync_ShouldRenderTheCategoryAlone_WhenTheReasonIsTheSameLabel()
	{
		// Arrange: the console asks every category for its own description, so this is now the odd
		// case rather than the normal one - a filer who typed the category's own name into "Please
		// specify". Showing both lines would read "Category: Billing / Details: Billing".
		SetupComposedBody();
		SetupSuccessfulSend();

		// Act
		await _notifier.SendAsync(
			CreateDetails(disputeCategory: "Billing", disputeReason: "Billing"),
			CancellationToken.None);

		// Assert
		VerifyBody(Times.Once(), "Billing", null);
	}

	[Theory]
	[InlineData("Billing")]
	[InlineData("Report")]
	[InlineData("Others")]
	public async Task SendAsync_ShouldRenderBothLines_ForEveryCategory(string category)
	{
		// Arrange: all three categories now carry free text, so all three render both lines. This
		// used to be the "Others" case alone.
		const string details = "The report lists an employer I never worked for.";
		SetupComposedBody();
		SetupSuccessfulSend();

		// Act
		await _notifier.SendAsync(
			CreateDetails(disputeCategory: category, disputeReason: details),
			CancellationToken.None);

		// Assert
		VerifyBody(Times.Once(), category, details);
	}

	[Fact]
	public async Task SendAsync_ShouldUseTheReasonAsTheCategory_WhenNoCategoryWasSent()
	{
		// Arrange: a client that predates the DisputeCategory field still owes the filer an
		// acknowledgement, and the reason is the best available label.
		SetupComposedBody();
		SetupSuccessfulSend();

		// Act
		await _notifier.SendAsync(
			CreateDetails(disputeCategory: null, disputeReason: "Report"),
			CancellationToken.None);

		// Assert: one line, not the same value twice.
		VerifyBody(Times.Once(), "Report", null);
	}

	[Fact]
	public async Task SendAsync_ShouldFallBackToTheEmailAddress_WhenTheFilerHasNoName()
	{
		// Arrange: FullName is a token claim and is absent on tokens issued before it existed. The
		// copy reads "Dear <name>," so an address still identifies the recipient.
		SetupComposedBody();
		SetupSuccessfulSend();

		// Act
		await _notifier.SendAsync(CreateDetails(requestorName: null), CancellationToken.None);

		// Assert
		_emailSender.Verify(
			sender => sender.BuildDisputeNotification(
				FilerEmail,
				CandidateName,
				It.IsAny<string>(),
				It.IsAny<string?>()),
			Times.Once);
	}

	[Fact]
	public async Task SendAsync_ShouldNotSend_WhenTheFilerHasNoEmailAddress()
	{
		// Arrange: no address means nobody to acknowledge to. The dispute is already recorded,
		// which is the part that matters.
		SetupComposedBody();

		// Act
		await _notifier.SendAsync(CreateDetails(requestorEmail: null), CancellationToken.None);

		// Assert
		VerifySend(Times.Never());
	}

	[Fact]
	public async Task SendAsync_ShouldNotThrow_WhenTheSenderThrows()
	{
		// Arrange: the caller has already committed the dispute, so a dead SMTP account must not
		// reach CustomExceptionHandler - that would answer a filed dispute with a 500 and the filer
		// would submit the same dispute again.
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
		Func<Task> act = () => _notifier.SendAsync(CreateDetails(), CancellationToken.None);

		// Assert
		await act.Should().NotThrowAsync();
	}

	[Fact]
	public async Task SendAsync_ShouldNotThrow_WhenEverySenderAccountRefuses()
	{
		// Arrange: Throttled means "defer", and there is no queue behind a dispute acknowledgement.
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
		Func<Task> act = () => _notifier.SendAsync(CreateDetails(), CancellationToken.None);

		// Assert
		await act.Should().NotThrowAsync();
		VerifySendTo(Times.Once(), [CopyTeam]);
	}

	[Fact]
	public async Task SendAsync_ShouldRecordTheAcknowledgementInTheOrderHistory_WhenTheSendIsAttempted()
	{
		// Arrange
		SetupComposedBody();
		SetupSuccessfulSend();

		// Act
		await _notifier.SendAsync(CreateDetails(), CancellationToken.None);

		// Assert: a second row beside the ReportDisputed one the filing itself writes. "The report
		// was disputed" and "we acknowledged it to the filer" are different facts, and the second
		// can fail while the first already happened.
		VerifyHistoryRecorded(Times.Once());
	}

	[Fact]
	public async Task SendAsync_ShouldStillRecordTheAcknowledgement_WhenDeliveryFails()
	{
		// Arrange: the row records the ATTEMPT, so support can answer "did we try to tell them?"
		// from the timeline alone. Whether it landed is in the log.
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
		await _notifier.SendAsync(CreateDetails(), CancellationToken.None);

		// Assert
		VerifyHistoryRecorded(Times.Once());
	}

	[Fact]
	public async Task SendAsync_ShouldNotRecordTheAcknowledgement_WhenThereIsNobodyToSendTo()
	{
		// Arrange: no send was attempted, so the timeline must not claim one was.
		SetupComposedBody();

		// Act
		await _notifier.SendAsync(CreateDetails(requestorEmail: null), CancellationToken.None);

		// Assert
		VerifyHistoryRecorded(Times.Never());
		VerifySend(Times.Never());
	}

	// Literals rather than OrderStatus.Completed, because that class is internal to the ATS assembly
	// and no InternalsVisibleTo reaches the test project. Pinning the stored string is the stronger
	// assertion anyway - it is exactly what the history API returns to the dialog.
	private void VerifyHistoryRecorded(Times times) =>
		_orderHistoryService.Verify(
			history => history.RecordAsync(
				InvitationId,
				OrderHistoryEventType.DisputeAcknowledgementEmail,
				null,
				"Completed",
				It.IsAny<CancellationToken>(),
				OrderHistorySource.Web),
			times);
}
