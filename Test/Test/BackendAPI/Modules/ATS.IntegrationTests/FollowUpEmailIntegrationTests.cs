using ATS.Data.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Test.BackendAPI.Infrastructure.ATS.Infrastracture;

namespace Test.BackendAPI.Modules.ATS.IntegrationTests;

/// <summary>
/// The package follow-up chaser: one reminder, N days after the order, reusing the candidate's
/// original link. See docs/ats-package-follow-up-email.md.
/// </summary>
/// <remarks>
/// These exercise the release query directly rather than the Quartz job, because the job is a
/// four-line wrapper around it and everything worth asserting - who gets chased, who does not,
/// and that nobody gets chased twice - lives in the SQL.
/// </remarks>
public class FollowUpEmailIntegrationTests : BaseIntegrationTest
{
	public FollowUpEmailIntegrationTests(IntegrationTestWebAppFactory factory)
		: base(factory)
	{
	}

	private const string SeededHashToken = "follow-up-hash-token";

	#region Released

	[Fact]
	public async Task ReleaseDueFollowUps_ShouldRequeueTheRow_WhenTheIntervalHasElapsed()
	{
		// Arrange
		await SetFollowUpDaysAsync(3);
		var id = await SeedOrderAsync(orderCreatedAt: DateTime.UtcNow.AddDays(-4));

		// Act
		var released = await _atsRepository.ReleaseDueFollowUpInvitationsAsync(CancellationToken.None);

		// Assert
		released.Should().ContainSingle().Which.EmailInvitationID.Should().Be(id);

		var updated = await ReadAsync(id);

		// Back on the email job's queue - the chaser queues, it never sends. That is what keeps
		// a reminder inside the same pool, caps and pacing as every other ATS message.
		updated.EmailSentStatus.Should().Be("Pending");

		// A reminder is a fresh delivery, so the cap of five applies to it in its own right.
		updated.EmailSendAttempts.Should().Be(0);
		updated.EmailClaimedAt.Should().BeNull();
		updated.EmailSentAt.Should().BeNull();
	}

	// The whole point of the feature: the email already sitting in the candidate's inbox has to
	// keep working. If this ever fails, the reminder is actively worse than sending nothing -
	// it would retire the link the candidate was about to click.
	[Fact]
	public async Task ReleaseDueFollowUps_ShouldNotRotateTheToken()
	{
		await SetFollowUpDaysAsync(1);
		var createdAt = DateTime.UtcNow.AddDays(-5);
		var id = await SeedOrderAsync(orderCreatedAt: createdAt, hashTokenCreatedAt: createdAt);

		await _atsRepository.ReleaseDueFollowUpInvitationsAsync(CancellationToken.None);

		var updated = await ReadAsync(id);
		updated.HashToken.Should().Be(SeededHashToken);
		updated.HashTokenCreatedAt.Should().BeCloseTo(createdAt, TimeSpan.FromSeconds(1));
	}

	[Fact]
	public async Task ReleaseDueFollowUps_ShouldStampFollowUpQueuedAt()
	{
		await SetFollowUpDaysAsync(2);
		var id = await SeedOrderAsync(orderCreatedAt: DateTime.UtcNow.AddDays(-3));

		await _atsRepository.ReleaseDueFollowUpInvitationsAsync(CancellationToken.None);

		var updated = await ReadAsync(id);
		updated.FollowUpQueuedAt.Should().NotBeNull();
		updated.FollowUpQueuedAt!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
	}

	// Fire once, ever. The stamp is written in the same UPDATE as the requeue precisely so this
	// holds even if the process dies mid-pass.
	[Fact]
	public async Task ReleaseDueFollowUps_ShouldReleaseNothingOnASecondPass()
	{
		await SetFollowUpDaysAsync(1);
		await SeedOrderAsync(orderCreatedAt: DateTime.UtcNow.AddDays(-9));

		var first = await _atsRepository.ReleaseDueFollowUpInvitationsAsync(CancellationToken.None);
		var second = await _atsRepository.ReleaseDueFollowUpInvitationsAsync(CancellationToken.None);

		first.Should().ContainSingle();
		second.Should().BeEmpty();
	}

	#endregion

	#region Not released

	[Fact]
	public async Task ReleaseDueFollowUps_ShouldSkipTheRow_WhenFollowUpIsZero()
	{
		// 0 is the off switch, and it is the default every package starts with.
		await SetFollowUpDaysAsync(0);
		await SeedOrderAsync(orderCreatedAt: DateTime.UtcNow.AddDays(-30));

		var released = await _atsRepository.ReleaseDueFollowUpInvitationsAsync(CancellationToken.None);

		released.Should().BeEmpty();
	}

	[Fact]
	public async Task ReleaseDueFollowUps_ShouldSkipTheRow_WhenTheIntervalHasNotElapsed()
	{
		await SetFollowUpDaysAsync(7);
		await SeedOrderAsync(orderCreatedAt: DateTime.UtcNow.AddDays(-2));

		var released = await _atsRepository.ReleaseDueFollowUpInvitationsAsync(CancellationToken.None);

		released.Should().BeEmpty();
	}

	// Data screening has no candidate to email. NULL is excluded as well as false: an
	// unclassified order cannot prove it is manual, matching the email claim query.
	[Theory]
	[InlineData(false)]
	[InlineData(null)]
	public async Task ReleaseDueFollowUps_ShouldSkipTheRow_WhenItIsNotManualScreening(bool? autoChasing)
	{
		await SetFollowUpDaysAsync(1);
		await SeedOrderAsync(orderCreatedAt: DateTime.UtcNow.AddDays(-5), autoChasing: autoChasing);

		var released = await _atsRepository.ReleaseDueFollowUpInvitationsAsync(CancellationToken.None);

		released.Should().BeEmpty();
	}

	// Nobody who already dealt with the form gets chased about it.
	[Theory]
	[InlineData("Done")]
	[InlineData("Withdrawn")]
	public async Task ReleaseDueFollowUps_ShouldSkipTheRow_WhenTheFormIsNoLongerPending(string status)
	{
		await SetFollowUpDaysAsync(1);
		await SeedOrderAsync(
			orderCreatedAt: DateTime.UtcNow.AddDays(-5),
			applicationFormStatus: status);

		var released = await _atsRepository.ReleaseDueFollowUpInvitationsAsync(CancellationToken.None);

		released.Should().BeEmpty();
	}

	// A row still queued, in flight, or failed is not being ignored by the candidate - it was
	// never delivered. Chasing someone about an email they never received is nonsense, and
	// requeueing a Processing row would race the worker that is mid-send on it.
	[Theory]
	[InlineData("Pending")]
	[InlineData("Processing")]
	[InlineData("Error")]
	public async Task ReleaseDueFollowUps_ShouldSkipTheRow_WhenTheFirstEmailWasNeverDelivered(string status)
	{
		await SetFollowUpDaysAsync(1);
		await SeedOrderAsync(
			orderCreatedAt: DateTime.UtcNow.AddDays(-5),
			emailSentStatus: status);

		var released = await _atsRepository.ReleaseDueFollowUpInvitationsAsync(CancellationToken.None);

		released.Should().BeEmpty();
	}

	[Fact]
	public async Task ReleaseDueFollowUps_ShouldSkipTheRow_WhenItHasAlreadyBeenChased()
	{
		await SetFollowUpDaysAsync(1);
		await SeedOrderAsync(
			orderCreatedAt: DateTime.UtcNow.AddDays(-5),
			followUpQueuedAt: DateTime.UtcNow.AddDays(-1));

		var released = await _atsRepository.ReleaseDueFollowUpInvitationsAsync(CancellationToken.None);

		released.Should().BeEmpty();
	}

	#endregion

	#region Helpers

	/// <summary>
	/// Sets the interval on the package every seeded order points at. The default package is
	/// created with FollowUpEmail = 0, so a test that wants a chaser has to say so.
	/// </summary>
	private async Task SetFollowUpDaysAsync(int days)
	{
		await _dbContext.Database.ExecuteSqlRawAsync(
			"""UPDATE ats."PackageDetails" SET "FollowUpEmail" = {0} WHERE "PackageId" = {1};""",
			days, DefaultPackageId);
	}

	/// <summary>
	/// A delivered, unanswered, manual-screening order - the one shape that gets chased.
	/// Each parameter exists so a test can break exactly one of those conditions.
	/// </summary>
	private async Task<Guid> SeedOrderAsync(
		DateTime orderCreatedAt,
		bool? autoChasing = true,
		string applicationFormStatus = "Pending",
		string emailSentStatus = "Done",
		DateTime? followUpQueuedAt = null,
		DateTime? hashTokenCreatedAt = null)
	{
		var order = new EmailInvitationRequest
		{
			EmailInvitationID = Guid.CreateVersion7(),
			FirstName = "Juan",
			LastName = "Dela Cruz",
			MiddleInitial = "S",
			EmailAddress = "juan.delacruz@example.com",
			MobileNumber = "09171234567",
			PackageId = DefaultPackageId,
			SelectPackage = DefaultPackageName,
			RushNormal = "Normal",
			HashToken = SeededHashToken,
			HashTokenCreatedAt = hashTokenCreatedAt ?? orderCreatedAt,
			AutoChasing = autoChasing,
			ApplicationFormStatus = applicationFormStatus,
			EmailSentStatus = emailSentStatus,
			EmailSentAt = emailSentStatus == "Done" ? orderCreatedAt : null,
			OrderStatus = "Pending Candidate Info",
			OrderCreatedAt = orderCreatedAt,
			FollowUpQueuedAt = followUpQueuedAt
		};

		await _dbContext.EmailInvitationRequests.AddAsync(order);
		await _dbContext.SaveChangesAsync();

		// The release runs as raw SQL against the database, so anything still tracked here would
		// shadow what it wrote when the assertions read the row back.
		_dbContext.ChangeTracker.Clear();

		return order.EmailInvitationID;
	}

	private async Task<EmailInvitationRequest> ReadAsync(Guid id)
	{
		return await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.SingleAsync(x => x.EmailInvitationID == id);
	}

	#endregion
}
