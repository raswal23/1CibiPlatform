using ATS.Constants;
using ATS.Data.Repository;
using ATS.Data.UnitOfWork;
using ATS.Services.AccessScope;
using ATS.Services.EmailService;
using ATS.Services.EndorsementSubmission;
using ATS.Services.OrderHistory;
using ATS.Services.OrderValidation;
using Auth.DTO;
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
	// "clientsupport@cibi.com.ph" and "pre-workteam@cibi.com.ph" as literals so that a change to the
	// agreed copy fails a test rather than going out silently.
	//
	// That convention is right, and the sibling tests carry it for this module's copy list - the two
	// notice tests pin the same pair of addresses this constant holds. Pinning them a third time here
	// would report an already-reported fact and say nothing about the wiring this file exists to
	// cover: which addresses are copied, on which of the two bodies, and what happens when the sender
	// cannot carry a copy list at all. Those assertions hold whatever the constant contains, which is
	// what lets this file stay green while a constant is temporarily swapped for branch verification.
	private static readonly IReadOnlyCollection<string> CopyTeams = ApplicationFormEmail.CopyTeams;

	private const string CandidateEmail = "candidate@example.test";
	private const string CandidateName = "Juan Dela Cruz";
	private const string ApplicationFormLink = "https://example.test/form/token";
	private const string RequestorName = "Ana Reyes";
	private const string RequestorEmail = "ana.reyes@example.test";

	private static readonly Guid RequestorId = Guid.Parse("11111111-1111-1111-1111-111111111111");

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

	/// <summary>
	/// The requestor's mailbox is not on the order - only their display name is - so the copy list
	/// resolves it through the Auth directory, the same lookup the three sibling notices use.
	/// </summary>
	private readonly Mock<IAuthQueries> _authQueries = new();

	public ApplicationFormEmailCopyTests()
	{
		_authQueries
			.Setup(queries => queries.GetATSAssignedUserAsync(
				RequestorId,
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(new ATSUserLookupDTO
			{
				UserId = RequestorId,
				UserName = RequestorName,
				UserEmail = RequestorEmail
			});

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
			Mock.Of<IUnitOfWork>(),
			_authQueries.Object);

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

	private static Task<EmailDeliveryResult> SendAsync(
		EndorsementSubmissionService service,
		bool isFollowUp = false) =>
		SendAsync(service, RequestorId, isFollowUp);

	/// <summary>
	/// The requestor id is explicit here rather than an optional <c>Guid?</c> defaulting to null,
	/// so that "no requestor on the order" is a value a test can actually pass. A defaulted
	/// parameter coalesced to <see cref="RequestorId"/> would make the null case unreachable.
	/// </summary>
	private static Task<EmailDeliveryResult> SendAsync(
		EndorsementSubmissionService service,
		Guid? requestorId,
		bool isFollowUp = false) =>
		service.SendApplicationFormToUserEmailWithResultAsync(
			CandidateEmail,
			CandidateName,
			ApplicationFormLink,
			RequestorName,
			requestorId,

			// Null so ResolveClientNameAsync returns without touching the repository - the client name
			// is cosmetic and the body is stubbed anyway.
			clientId: null,
			CancellationToken.None,
			isFollowUp);

	/// <summary>
	/// The full expected copy list: the fixed teams, then the requestor resolved from the directory.
	/// </summary>
	private static readonly string[] TeamsAndRequestor = [.. CopyTeams, RequestorEmail];

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
					cc != null && cc.SequenceEqual(TeamsAndRequestor))),
			Times.Once);

	[Fact]
	public async Task SendApplicationForm_ShouldAddressTheCandidateAndCopyBothTeamsAndTheRequestor()
	{
		// Arrange
		var sender = SetupResultAwareSender();
		var service = CreateService();

		// Act
		var result = await SendAsync(service);

		// Assert: the candidate is the TO address - the form is theirs to fill in - and both CIBI teams
		// plus the requestor who raised the order are copied, so each holds the same thread.
		result.IsSent.Should().BeTrue();
		VerifyCopiedTeams(sender, InvitationSubject, InvitationBody);
	}

	[Fact]
	public async Task SendApplicationForm_ShouldResolveTheRequestorMailboxFromTheDirectory()
	{
		// Arrange: the order carries the requestor's DISPLAY NAME, not an address - Requestor is
		// whatever ICurrentUser.FullName held at order time. The id is the durable handle, and the Auth
		// directory is the only source for the mailbox.
		var sender = SetupResultAwareSender();
		var service = CreateService();

		// Act
		await SendAsync(service);

		// Assert
		_authQueries.Verify(
			queries => queries.GetATSAssignedUserAsync(RequestorId, It.IsAny<CancellationToken>()),
			Times.Once);

		sender.Verify(
			resultAware => resultAware.SendATSEmailWithResultAsync(
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<CancellationToken>(),
				It.Is<IReadOnlyCollection<string>?>(cc =>
					cc != null && cc.Contains(RequestorEmail) && !cc.Contains(RequestorName))),
			Times.Once);
	}

	[Fact]
	public async Task SendApplicationForm_ShouldStillCopyTheTeams_WhenTheOrderHasNoRequestorId()
	{
		// Arrange: a bulk row or a public API order can be raised without one. The teams still need
		// the thread, so the requestor is simply left off rather than the copy list being abandoned.
		var sender = SetupResultAwareSender();
		var service = CreateService();

		// Act
		var result = await SendAsync(service, requestorId: null);

		// Assert
		result.IsSent.Should().BeTrue();

		// There is no id to look up, so the directory is not consulted at all - not consulted and
		// answering nothing must both end in the same copy list.
		_authQueries.Verify(
			queries => queries.GetATSAssignedUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
			Times.Never);

		sender.Verify(
			resultAware => resultAware.SendATSEmailWithResultAsync(
				CandidateEmail,
				InvitationSubject,
				InvitationBody,
				It.IsAny<CancellationToken>(),
				It.Is<IReadOnlyCollection<string>?>(cc =>
					cc != null && cc.SequenceEqual(CopyTeams))),
			Times.Once);
	}

	[Fact]
	public async Task SendApplicationForm_ShouldStillSend_WhenTheRequestorNoLongerResolves()
	{
		// Arrange: the user lost their ATS assignment since raising the order, so the directory returns
		// nothing. The candidate's link is the point of the message - a copy that cannot be addressed
		// must not take the invitation down with it. On the single-order path it would roll back the
		// whole order, because the send runs inside the TransactionRunner boundary.
		var sender = SetupResultAwareSender();

		_authQueries
			.Setup(queries => queries.GetATSAssignedUserAsync(
				RequestorId,
				It.IsAny<CancellationToken>()))
			.ReturnsAsync((ATSUserLookupDTO?)null);

		var service = CreateService();

		// Act
		var result = await SendAsync(service);

		// Assert
		result.IsSent.Should().BeTrue();

		sender.Verify(
			resultAware => resultAware.SendATSEmailWithResultAsync(
				CandidateEmail,
				InvitationSubject,
				InvitationBody,
				It.IsAny<CancellationToken>(),
				It.Is<IReadOnlyCollection<string>?>(cc =>
					cc != null && cc.SequenceEqual(CopyTeams))),
			Times.Once);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public async Task SendApplicationForm_ShouldLeaveTheRequestorOff_WhenTheirMailboxIsBlank(string? userEmail)
	{
		// Arrange: the directory knows the user but holds no usable address for them. Whitespace counts
		// as blank - MailboxAddress.Parse would throw on it inside BuildMessage, taking down a send that
		// the candidate's link is the whole point of.
		var sender = SetupResultAwareSender();

		_authQueries
			.Setup(queries => queries.GetATSAssignedUserAsync(
				RequestorId,
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(new ATSUserLookupDTO
			{
				UserId = RequestorId,
				UserName = RequestorName,
				UserEmail = userEmail!
			});

		var service = CreateService();

		// Act
		var result = await SendAsync(service);

		// Assert
		result.IsSent.Should().BeTrue();

		sender.Verify(
			resultAware => resultAware.SendATSEmailWithResultAsync(
				CandidateEmail,
				InvitationSubject,
				InvitationBody,
				It.IsAny<CancellationToken>(),
				It.Is<IReadOnlyCollection<string>?>(cc =>
					cc != null && cc.SequenceEqual(CopyTeams))),
			Times.Once);
	}

	[Fact]
	public async Task SendApplicationForm_ShouldStillSend_WhenTheDirectoryLookupThrows()
	{
		// Arrange: same reasoning as the test above, for a directory that is unreachable rather than
		// one that answers "no such user". SideEffectGuard swallows it and the send carries on.
		var sender = SetupResultAwareSender();

		_authQueries
			.Setup(queries => queries.GetATSAssignedUserAsync(
				RequestorId,
				It.IsAny<CancellationToken>()))
			.ThrowsAsync(new InvalidOperationException("The user directory is unavailable."));

		var service = CreateService();

		// Act
		var result = await SendAsync(service);

		// Assert
		result.IsSent.Should().BeTrue();

		sender.Verify(
			resultAware => resultAware.SendATSEmailWithResultAsync(
				CandidateEmail,
				InvitationSubject,
				InvitationBody,
				It.IsAny<CancellationToken>(),
				It.Is<IReadOnlyCollection<string>?>(cc =>
					cc != null && cc.SequenceEqual(CopyTeams))),
			Times.Once);
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
