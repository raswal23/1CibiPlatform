using ATS.Services.EmailService;
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

	private readonly Mock<ILogger<DisputeEmailNotification>> _logger = new();
	private readonly Mock<IAtsEmailSender> _emailSender = new();

	private readonly DisputeEmailNotification _notifier;

	public DisputeEmailNotificationTests()
	{
		_notifier = new DisputeEmailNotification(
			_logger.Object,
			_emailSender.Object);
	}

	private static DisputeEmailDetails CreateDetails(
		string? requestorEmail = FilerEmail,
		string? requestorName = FilerName,
		string? disputeCategory = "Report",
		string? disputeReason = "Report") => new(
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
		// Arrange: a Billing or Report dispute. The console sends the category label as the reason
		// too, so showing both lines would read "Category: Report / Details: Report".
		SetupComposedBody();
		SetupSuccessfulSend();

		// Act
		await _notifier.SendAsync(
			CreateDetails(disputeCategory: "Billing", disputeReason: "Billing"),
			CancellationToken.None);

		// Assert
		VerifyBody(Times.Once(), "Billing", null);
	}

	[Fact]
	public async Task SendAsync_ShouldRenderBothLines_WhenTheReasonDiffersFromTheCategory()
	{
		// Arrange: an "Others" dispute, the only case where the console captures free text.
		const string details = "The report lists an employer I never worked for.";
		SetupComposedBody();
		SetupSuccessfulSend();

		// Act
		await _notifier.SendAsync(
			CreateDetails(disputeCategory: "Others", disputeReason: details),
			CancellationToken.None);

		// Assert
		VerifyBody(Times.Once(), "Others", details);
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
}
