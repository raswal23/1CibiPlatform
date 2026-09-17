using ATS.Constants;
using ATS.Data.Repository;
using ATS.Data.UnitOfWork;
using ATS.Services.AccessScope;
using ATS.Services.EmailService;
using ATS.Services.EndorsementSubmission;
using ATS.Services.OrderHistory;
using ATS.Services.OrderValidation;
using Auth.Shared.Contracts;
using BuildingBlocks.SharedServices.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace Test.BackendAPI.Modules.ATS.UnitTests;

/// <summary>
/// Who is copied on the two candidate-facing application form emails - the first invitation and the
/// package follow-up reminder - and that a sender which cannot carry a copy list still delivers.
/// </summary>
/// <remarks>
/// Stops at <see cref="IAtsEmailSender"/>, for the same reason the three notice tests do: a successful
/// SMTP send cannot be faked without a server. That the copied addresses reach the wire and are charged
/// to the account's daily cap is <c>ATSEmailService.SendThroughAccountAsync</c>'s business, covered by
/// <c>AtsEmailFailoverTests</c>.
/// </remarks>
public class ApplicationFormEmailCopyTests
{
	// Read from the constant, DEPARTING from SubmittedFormEmailNotificationTests, which pins
	// "ccteam@cibi.com.ph" and "pre-workteam@cibi.com.ph" as literals so that a change to the agreed
	// copy fails a test rather than going out silently.
	//
	// That convention is right, and it is already doing its job: the constants in this module are
	// currently swapped to a tester's mailboxes for branch verification, and the three sibling notice
	// tests are red because of it. Pinning the same literals here would add a fourth red test that
	// reports the identical, already-reported fact, and would say nothing about the wiring this file
	// exists to cover - which addresses are copied, on which of the two bodies, and what happens when
	// the sender cannot carry a copy list at all. Those assertions hold whatever the constant contains.
	//
	// The release check the literals provide is not lost: restoring all four constants is one step, and
	// the three sibling tests go green together when it happens.
	private static readonly IReadOnlyCollection<string> CopyTeams = ApplicationFormEmail.CopyTeams;

	private const string CandidateEmail = "candidate@example.test";
	private const string CandidateName = "Juan Dela Cruz";
	private const string ApplicationFormLink = "https://example.test/form/token";
	private const string RequestorName = "Ana Reyes";

	private const string InvitationSubject = "CIBI | Background Verification Information Request";
	private const string ReminderSubject = "CIBI | Reminder: Background Verification Information Request";

	private const string InvitationBody = "invitation-body";
	private const string ReminderBody = "reminder-body";

	private readonly Mock<IATSRepository> _repository = new();

	/// <summary>
	/// One mock satisfying both contracts, because the service decides what it can send by casting:
	/// <c>_emailService as IAtsEmailSender</c>. Two separate mocks would leave the cast returning null
	/// and silently exercise the fallback path in every test.
	/// </summary>
	private readonly Mock<IEmailService> _emailService = new();

	public ApplicationFormEmailCopyTests()
	{
		_emailService
			.Setup(sender => sender.SendAppplicationFormNotification(
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<string?>(),
				It.IsAny<string?>()))
			.Returns(InvitationBody);
	}

	/// <summary>
	/// Built per test rather than in the constructor, because <c>Mock.As</c> must run before anything
	/// touches <c>.Object</c> - and the service constructor does. A field initialised up front would
	/// make every result-aware test throw "mock type has already been initialized".
	/// </summary>
	private EndorsementSubmissionService CreateService() =>
		new(
			Mock.Of<ILogger<EndorsementSubmissionService>>(),
			_repository.Object,
			new ConfigurationBuilder().Build(),
			Mock.Of<IHashService>(),
			_emailService.Object,
			Mock.Of<HybridCache>(),
			Mock.Of<ISecureToken>(),
			new HttpContextAccessor(),
			Mock.Of<ICurrentUser>(),
			Mock.Of<IObjectStorageService>(),
			Mock.Of<IOrderHistoryService>(),
			Mock.Of<IUserClientRepository>(),
			Mock.Of<IAtsAccessScopeResolver>(),

			// Not exercised here: these tests only send, and the validator is only consulted on the
			// create paths.
			Mock.Of<IOrderInputValidator>(),
			Mock.Of<IUnitOfWork>());

	/// <summary>
	/// Adds the ATS-only contract to the same mock object, so the service's cast succeeds and the
	/// result-aware path - the only one that can carry a copy list - is the one under test.
	/// </summary>
	private Mock<IAtsEmailSender> SetupResultAwareSender()
	{
		var sender = _emailService.As<IAtsEmailSender>();

		sender
			.Setup(resultAware => resultAware.BuildApplicationFormReminderNotification(
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<string?>(),
				It.IsAny<string?>()))
			.Returns(ReminderBody);

		sender
			.Setup(resultAware => resultAware.SendATSEmailWithResultAsync(
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<CancellationToken>(),
				It.IsAny<IReadOnlyCollection<string>?>()))
			.ReturnsAsync(EmailDeliveryResult.Sent);

		return sender;
	}

	private Task<EmailDeliveryResult> SendAsync(
		EndorsementSubmissionService service,
		bool isFollowUp = false) =>
		service.SendApplicationFormToUserEmailWithResultAsync(
			CandidateEmail,
			CandidateName,
			ApplicationFormLink,
			RequestorName,

			// Null so ResolveClientNameAsync returns without touching the repository - the client name
			// is cosmetic and the body is stubbed anyway.
			clientId: null,
			CancellationToken.None,
			isFollowUp);

	private static void VerifyCopiedTeams(
		Mock<IAtsEmailSender> sender,
		string expectedSubject,
		string expectedBody) =>
		sender.Verify(
			resultAware => resultAware.SendATSEmailWithResultAsync(
				CandidateEmail,
				expectedSubject,
				expectedBody,
				It.IsAny<CancellationToken>(),
				It.Is<IReadOnlyCollection<string>?>(cc =>
					cc != null && cc.SequenceEqual(CopyTeams))),
			Times.Once);

	[Fact]
	public async Task SendApplicationForm_ShouldAddressTheCandidateAndCopyBothTeams()
	{
		// Arrange
		var sender = SetupResultAwareSender();
		var service = CreateService();

		// Act
		var result = await SendAsync(service);

		// Assert: the candidate is the TO address - the form is theirs to fill in - and both CIBI teams
		// are copied so a team mailbox holds the same thread the candidate does.
		result.IsSent.Should().BeTrue();
		VerifyCopiedTeams(sender, InvitationSubject, InvitationBody);
	}

	[Fact]
	public async Task SendApplicationForm_ShouldCopyBothTeamsOnTheFollowUpReminder_Too()
	{
		// Arrange: the reminder is the same request sent again, so the teams see the chase as well as
		// the original. Both bodies travel the same send, and the copy list does not branch on which.
		var sender = SetupResultAwareSender();
		var service = CreateService();

		// Act
		var result = await SendAsync(service, isFollowUp: true);

		// Assert
		result.IsSent.Should().BeTrue();
		VerifyCopiedTeams(sender, ReminderSubject, ReminderBody);
	}

	[Fact]
	public async Task SendApplicationForm_ShouldNeverCopyTheCandidateTwice()
	{
		// Arrange: the candidate is the TO address. Repeating them in the copy list would show them
		// their own address as a Cc and charge the sending account's daily cap for a recipient that is
		// already on the message.
		var sender = SetupResultAwareSender();
		var service = CreateService();

		// Act
		await SendAsync(service);

		// Assert
		sender.Verify(
			resultAware => resultAware.SendATSEmailWithResultAsync(
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<CancellationToken>(),
				It.Is<IReadOnlyCollection<string>?>(cc =>
					cc != null && !cc.Contains(CandidateEmail))),
			Times.Once);
	}

	[Fact]
	public async Task SendApplicationForm_ShouldStillSendToTheCandidate_WhenTheSenderCannotCarryACopyList()
	{
		// Arrange: the keyed "ats" registration is always ATSEmailService in production, but the cast is
		// guarded rather than assumed. A sender that is not the ATS one has no cc parameter - the bool
		// contract in BuildingBlocks that Auth and the test fakes implement - so the candidate still
		// gets their link, just without the teams copied. Losing the copy must never lose the send.
		_emailService
			.Setup(sender => sender.SendATSEmailAsync(
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<string>()))
			.ReturnsAsync(true);

		// No SetupResultAwareSender, so the cast inside the service yields null.
		var service = CreateService();

		// Act
		var result = await SendAsync(service);

		// Assert
		result.IsSent.Should().BeTrue();
		_emailService.Verify(
			sender => sender.SendATSEmailAsync(
				CandidateEmail,
				InvitationSubject,
				InvitationBody),
			Times.Once);
	}
}
