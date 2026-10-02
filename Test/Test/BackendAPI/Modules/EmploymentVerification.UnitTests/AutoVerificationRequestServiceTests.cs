using ATS.Shared.Contracts;
using EmploymentVerification.Data.Entities;
using EmploymentVerification.Data.Repository.ContactDirectory;
using EmploymentVerification.Services;
using EmploymentVerification.Services.AutoRequest;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace EmploymentVerification.UnitTests;

/// <summary>
/// Covers what the automatic sender decides: whose consent is honoured, which address
/// a request goes to, and what it refuses to guess at.
/// </summary>
public class AutoVerificationRequestServiceTests
{
	private readonly Mock<IEmploymentVerificationService> _verificationService = new(MockBehavior.Strict);
	private readonly Mock<IContactDirectoryRepository> _contactRepository = new(MockBehavior.Strict);

	private AutoVerificationRequestService CreateSut() =>
		new(
			_verificationService.Object,
			_contactRepository.Object,
			new ConfigurationBuilder().AddInMemoryCollection().Build(),
			NullLogger<AutoVerificationRequestService>.Instance);

	private static ATSInProgressEmploymentRecord Record(
		short segment = 1,
		string employer = "CONCENTRIX",
		string? supervisorEmail = "supervisor@example.test",
		bool permissionToContact = true,
		Guid? subjectId = null) =>
		new(
			SubjectId: subjectId ?? Guid.NewGuid(),
			EmploymentSegment: segment,
			CandidateName: "Juan Dela Cruz",
			Employer: employer,
			Position: "Analyst",
			StartDate: new DateOnly(2022, 1, 1),
			EndDate: new DateOnly(2024, 3, 1),
			SupervisorName: "Maria Santos",
			SupervisorEmail: supervisorEmail,
			PermissionToContact: permissionToContact);

	private void ExpectNoDirectoryMatch() =>
		_contactRepository
			.Setup(repository => repository.GetKnownActiveMailboxesAsync(
				It.IsAny<IReadOnlyCollection<string>>(),
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(new HashSet<string>());

	/// <summary>Addresses the directory lists, stored lower-cased as the service writes them.</summary>
	private void ExpectDirectory(params string[] knownAddresses) =>
		_contactRepository
			.Setup(repository => repository.GetKnownActiveMailboxesAsync(
				It.IsAny<IReadOnlyCollection<string>>(),
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(knownAddresses
				.Select(address => address.ToLowerInvariant())
				.ToHashSet());

	/// <summary>Captures the subject ids handed back to ATS as finished.</summary>
	private readonly List<Guid> _released = [];

	private void ExpectAvailable(params ATSInProgressEmploymentRecord[] records)
	{
		_verificationService
			.Setup(service => service.GetAvailableATSRecordsAsync(It.IsAny<CancellationToken>()))
			.ReturnsAsync(records);

		// Every pass reconciles lapsed links before reading, then hands finished orders
		// back. Both are stubbed here so each test only has to assert the part it cares
		// about, while the strict mock still catches an unexpected call.
		_verificationService
			.Setup(service => service.ReinstateLapsedOrdersAsync(It.IsAny<CancellationToken>()))
			.Returns(Task.CompletedTask);

		_verificationService
			.Setup(service => service.ReleaseFinishedOrdersAsync(
				It.IsAny<IReadOnlyCollection<Guid>>(),
				It.IsAny<CancellationToken>()))
			.Callback<IReadOnlyCollection<Guid>, CancellationToken>(
				(subjectIds, _) => _released.AddRange(subjectIds))
			.Returns(Task.CompletedTask);
	}

	private List<CreateEmploymentVerificationRequest> CaptureSends()
	{
		var captured = new List<CreateEmploymentVerificationRequest>();

		_verificationService
			.Setup(service => service.CreateAndSendAsync(
				It.IsAny<CreateEmploymentVerificationRequest>(),
				It.IsAny<CancellationToken>()))
			.Callback<CreateEmploymentVerificationRequest, CancellationToken>(
				(request, _) => captured.Add(request))
			.ReturnsAsync(new EmploymentVerificationRequest());

		return captured;
	}

	[Fact]
	public async Task SendDueRequestsAsync_ShouldSend_WhenTheSupervisorAddressIsListedInTheDirectory()
	{
		ExpectAvailable(Record(supervisorEmail: "maria.santos@concentrix.test"));
		ExpectDirectory("maria.santos@concentrix.test");
		var sends = CaptureSends();

		var result = await CreateSut().SendDueRequestsAsync(CancellationToken.None);

		result.Sent.Should().Be(1);
		sends.Single().HrEmail.Should().Be("maria.santos@concentrix.test");
		sends.Single().RecipientSource.Should().Be("Directory");
	}

	[Fact]
	public async Task SendDueRequestsAsync_ShouldNotSend_WhenTheSupervisorAddressIsNotInTheDirectory()
	{
		ExpectAvailable(Record(employer: "SOME STARTUP INC", supervisorEmail: "boss@startup.test"));
		ExpectNoDirectoryMatch();

		var result = await CreateSut().SendDueRequestsAsync(CancellationToken.None);

		// The directory is an allow-list, not a preference. An address the candidate
		// supplied but the directory does not know is never written to - otherwise a
		// candidate could nominate who verifies their own employment history.
		result.Sent.Should().Be(0);
		result.SkippedNoRecipient.Should().Be(1);

		// Strict mocks: a CreateAndSendAsync call here would fail the test outright.
	}

	[Fact]
	public async Task SendDueRequestsAsync_ShouldMatchTheDirectory_RegardlessOfAddressCasing()
	{
		ExpectAvailable(Record(supervisorEmail: "  Maria.Santos@Concentrix.TEST "));
		ExpectDirectory("maria.santos@concentrix.test");
		var sends = CaptureSends();

		var result = await CreateSut().SendDueRequestsAsync(CancellationToken.None);

		// Contacts are stored lower-cased, so a form entry in mixed case must still
		// match or the gate would reject addresses the directory genuinely lists.
		result.Sent.Should().Be(1);
		sends.Single().HrEmail.Should().Be("Maria.Santos@Concentrix.TEST");
	}

	[Fact]
	public async Task SendDueRequestsAsync_ShouldNotSend_WhenTheFormLeftTheSupervisorAddressBlank()
	{
		ExpectAvailable(Record(employer: "CONCENTRIX", supervisorEmail: null));
		ExpectNoDirectoryMatch();

		var result = await CreateSut().SendDueRequestsAsync(CancellationToken.None);

		result.Sent.Should().Be(0);
		result.SkippedNoRecipient.Should().Be(1);
	}

	[Fact]
	public async Task SendDueRequestsAsync_ShouldSkip_WhenTheCandidateDidNotPermitContact()
	{
		ExpectAvailable(Record(permissionToContact: false));

		var result = await CreateSut().SendDueRequestsAsync(CancellationToken.None);

		result.Sent.Should().Be(0);
		result.SkippedNoConsent.Should().Be(1);

		// Not even looked up: a segment without consent is not a send candidate, so no
		// directory query is issued for it. The strict contact mock proves that.
		_contactRepository.VerifyNoOtherCalls();
	}

	[Fact]
	public async Task SendDueRequestsAsync_ShouldCarryTheSegment_SoEachEmployerIsTrackedSeparately()
	{
		var subjectId = Guid.NewGuid();

		ExpectAvailable(
			Record(segment: 1, employer: "CONCENTRIX", supervisorEmail: "one@a.test", subjectId: subjectId),
			Record(segment: 2, employer: "ALORICA", supervisorEmail: "two@b.test", subjectId: subjectId),
			Record(segment: 3, employer: "TELEPERFORMANCE", supervisorEmail: "three@c.test", subjectId: subjectId));

		ExpectDirectory("one@a.test", "two@b.test", "three@c.test");
		var sends = CaptureSends();

		await CreateSut().SendDueRequestsAsync(CancellationToken.None);

		// One order, three employers, three requests - each stamped with its own slot.
		// Without the segment they would be indistinguishable and the availability
		// check would treat the first as covering all three.
		sends.Should().HaveCount(3);
		sends.Select(send => send.EmploymentSegment).Should().BeEquivalentTo(new short?[] { 1, 2, 3 });
		sends.Should().OnlyContain(send => send.AtsSubjectId == subjectId);
	}

	[Fact]
	public async Task SendDueRequestsAsync_ShouldSendEachSegmentToItsOwnEmployer_NotTheFirstOnes()
	{
		var subjectId = Guid.NewGuid();

		ExpectAvailable(
			Record(segment: 1, employer: "CONCENTRIX", supervisorEmail: "first@concentrix.test", subjectId: subjectId),
			Record(segment: 2, employer: "ALORICA", supervisorEmail: "second@alorica.test", subjectId: subjectId));

		ExpectDirectory("first@concentrix.test", "second@alorica.test");
		var sends = CaptureSends();

		await CreateSut().SendDueRequestsAsync(CancellationToken.None);

		// The recipient is resolved per record inside the loop. Carrying one between
		// iterations would ask the first employer to confirm the second's employment.
		sends.Single(send => send.EmploymentSegment == 1).HrEmail.Should().Be("first@concentrix.test");
		sends.Single(send => send.EmploymentSegment == 2).HrEmail.Should().Be("second@alorica.test");

		sends.Single(send => send.EmploymentSegment == 1).PreviousEmployer.Should().Be("CONCENTRIX");
		sends.Single(send => send.EmploymentSegment == 2).PreviousEmployer.Should().Be("ALORICA");
	}

	[Fact]
	public async Task SendDueRequestsAsync_ShouldSendTwice_WhenTwoSegmentsShareOneMailbox()
	{
		var subjectId = Guid.NewGuid();

		// Two subsidiaries behind one HR inbox. They are two separate employment claims,
		// so the inbox gets two emails with two tokens - confirming one says nothing
		// about the other.
		ExpectAvailable(
			Record(segment: 1, employer: "CONCENTRIX MANILA", supervisorEmail: "hr@concentrix.test", subjectId: subjectId),
			Record(segment: 2, employer: "CONCENTRIX CEBU", supervisorEmail: "hr@concentrix.test", subjectId: subjectId));

		ExpectDirectory("hr@concentrix.test");
		var sends = CaptureSends();

		var result = await CreateSut().SendDueRequestsAsync(CancellationToken.None);

		result.Sent.Should().Be(2);
		sends.Should().OnlyContain(send => send.HrEmail == "hr@concentrix.test");
		sends.Select(send => send.PreviousEmployer)
			.Should().BeEquivalentTo(new[] { "CONCENTRIX MANILA", "CONCENTRIX CEBU" });
	}

	[Fact]
	public async Task SendDueRequestsAsync_ShouldKeepGoing_WhenOneSegmentFailsToSend()
	{
		ExpectAvailable(
			Record(segment: 1, employer: "FIRST CO", supervisorEmail: "one@first.test"),
			Record(segment: 2, employer: "SECOND CO", supervisorEmail: "two@second.test"));

		ExpectDirectory("one@first.test", "two@second.test");

		_verificationService
			.SetupSequence(service => service.CreateAndSendAsync(
				It.IsAny<CreateEmploymentVerificationRequest>(),
				It.IsAny<CancellationToken>()))
			.ThrowsAsync(new InvalidOperationException("The verification email could not be sent."))
			.ReturnsAsync(new EmploymentVerificationRequest());

		var result = await CreateSut().SendDueRequestsAsync(CancellationToken.None);

		// One unreachable mailbox must not abandon the pass.
		result.Failed.Should().Be(1);
		result.Sent.Should().Be(1);
	}

	[Fact]
	public async Task SendDueRequestsAsync_ShouldSubstituteAPosition_WhenTheFormLeftItBlank()
	{
		ExpectAvailable(Record() with { Position = null });
		ExpectDirectory("supervisor@example.test");
		var sends = CaptureSends();

		await CreateSut().SendDueRequestsAsync(CancellationToken.None);

		// Position is non-nullable on the entity, so a blank one would reach PostgreSQL
		// as a constraint violation rather than a skipped segment.
		sends.Single().Position.Should().Be("Not provided");
	}

	[Fact]
	public async Task SendDueRequestsAsync_ShouldReleaseTheOrder_WhenEverySegmentIsSettled()
	{
		var subjectId = Guid.NewGuid();

		ExpectAvailable(
			Record(segment: 1, supervisorEmail: "one@a.test", subjectId: subjectId),
			// Declined consent is settled, not outstanding: it will never become
			// sendable, so it must not hold the order in the queue forever.
			Record(segment: 2, permissionToContact: false, subjectId: subjectId));

		ExpectDirectory("one@a.test");
		CaptureSends();

		await CreateSut().SendDueRequestsAsync(CancellationToken.None);

		_released.Should().ContainSingle().Which.Should().Be(subjectId);
	}

	[Fact]
	public async Task SendDueRequestsAsync_ShouldKeepTheOrderQueued_WhenASegmentIsWaitingOnAContact()
	{
		var subjectId = Guid.NewGuid();

		ExpectAvailable(
			Record(segment: 1, supervisorEmail: "one@a.test", subjectId: subjectId),
			Record(segment: 2, supervisorEmail: "unlisted@b.test", subjectId: subjectId));

		ExpectDirectory("one@a.test");
		CaptureSends();

		await CreateSut().SendDueRequestsAsync(CancellationToken.None);

		// Segment 2 is deferred, not settled - an operator may add that contact
		// tomorrow, and the order has to still be visible when they do. Releasing it
		// here would strand that segment permanently.
		_released.Should().BeEmpty();
	}

	[Fact]
	public async Task SendDueRequestsAsync_ShouldKeepTheOrderQueued_WhenASendFailed()
	{
		var subjectId = Guid.NewGuid();

		ExpectAvailable(Record(segment: 1, supervisorEmail: "one@a.test", subjectId: subjectId));
		ExpectDirectory("one@a.test");

		_verificationService
			.Setup(service => service.CreateAndSendAsync(
				It.IsAny<CreateEmploymentVerificationRequest>(),
				It.IsAny<CancellationToken>()))
			.ThrowsAsync(new InvalidOperationException("The verification email could not be sent."));

		await CreateSut().SendDueRequestsAsync(CancellationToken.None);

		_released.Should().BeEmpty();
	}

	[Fact]
	public async Task SendDueRequestsAsync_ShouldReconcileLapsedLinks_BeforeReadingWhatIsAvailable()
	{
		ExpectAvailable();

		await CreateSut().SendDueRequestsAsync(CancellationToken.None);

		// A released order is invisible to the read below it, so a link that lapses
		// after release would never be retried unless this runs first.
		_verificationService.Verify(
			service => service.ReinstateLapsedOrdersAsync(It.IsAny<CancellationToken>()),
			Times.Once);
	}

	[Fact]
	public async Task SendDueRequestsAsync_ShouldDoNothing_WhenNoSegmentsAreAvailable()
	{
		ExpectAvailable();

		var result = await CreateSut().SendDueRequestsAsync(CancellationToken.None);

		result.Should().BeEquivalentTo(
			new AutoVerificationRequestResult(0, 0, 0, 0, 0, false));

		_contactRepository.VerifyNoOtherCalls();
	}
}
