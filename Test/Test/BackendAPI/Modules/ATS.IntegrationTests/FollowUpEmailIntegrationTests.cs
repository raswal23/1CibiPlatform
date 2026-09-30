using ATS.Data.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Test.BackendAPI.Infrastructure.ATS.Infrastracture;

namespace Test.BackendAPI.Modules.ATS.IntegrationTests;

/// <summary>
/// The package follow-up chaser: one reminder a day for N days, anchored to the order's own time
/// of day, reusing the candidate's original link. See docs/ats-package-follow-up-email.md.
/// </summary>
/// <remarks>
/// These exercise the release query directly rather than the Quartz job, because the job is a
/// four-line wrapper around it and everything worth asserting - who gets chased, who does not,
/// and that nobody gets chased twice in a day - lives in the SQL.
/// </remarks>
public class FollowUpEmailIntegrationTests : BaseIntegrationTest
{
	public FollowUpEmailIntegrationTests(IntegrationTestWebAppFactory factory)
		: base(factory)
	{
	}

	private const string SeededHashToken = "follow-up-hash-token";

	// The release compares and stamps in Manila, so the tests have to reason in it too.
	// Seeding "2 days ago" in UTC and asserting against a Manila date is how a suite ends up
	// green locally and red for eight hours a day on a UTC build agent.
	private static readonly TimeZoneInfo ManilaZone =
		TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");

	private static DateTime ManilaNow =>
		TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ManilaZone);

	private static DateOnly ManilaToday => DateOnly.FromDateTime(ManilaNow);

	/// <summary>
	/// A UTC instant that lands <paramref name="days"/> days ago in Manila, at a time of day
	/// safely in the past so "the order's time of day has arrived" is unambiguously true.
	/// </summary>
	/// <remarks>
	/// Anchored to 00:30 local rather than "now minus N days": a test seeding the current time
	/// of day would sit exactly on the >= boundary, and whether it released would depend on
	/// which side of the same second the query evaluated.
	/// </remarks>
	private static DateTime ManilaDaysAgo(int days)
	{
		var localDate = ManilaNow.Date.AddDays(-days).AddMinutes(30);

		return TimeZoneInfo.ConvertTimeToUtc(
			DateTime.SpecifyKind(localDate, DateTimeKind.Unspecified),
			ManilaZone);
	}

	#region Released

	[Fact]
	public async Task ReleaseDueFollowUps_ShouldRequeueTheRow_WhenTheFirstDayHasArrived()
	{
		// Arrange
		await SetFollowUpDaysAsync(3);
		var id = await SeedOrderAsync(orderCreatedAt: ManilaDaysAgo(1));

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

	// The reminder starts the day AFTER the order, never the same day: an order placed at 8am
	// must not be chased at 9am the same morning.
	[Fact]
	public async Task ReleaseDueFollowUps_ShouldSkipTheRow_OnTheDayTheOrderWasPlaced()
	{
		await SetFollowUpDaysAsync(3);
		await SeedOrderAsync(orderCreatedAt: ManilaDaysAgo(0));

		var released = await _atsRepository.ReleaseDueFollowUpInvitationsAsync(CancellationToken.None);

		released.Should().BeEmpty();
	}

	// The core of the feature, and what the fire-once design could not do: a second day due
	// releases the same order again.
	[Fact]
	public async Task ReleaseDueFollowUps_ShouldReleaseTheSameRowAgain_OnTheFollowingDay()
	{
		await SetFollowUpDaysAsync(3);
		var id = await SeedOrderAsync(
			orderCreatedAt: ManilaDaysAgo(2),
			// Yesterday's reminder already went out, so today's is the second of three.
			lastFollowUpSentDate: ManilaToday.AddDays(-1));

		var released = await _atsRepository.ReleaseDueFollowUpInvitationsAsync(CancellationToken.None);

		released.Should().ContainSingle().Which.EmailInvitationID.Should().Be(id);

		var updated = await ReadAsync(id);
		updated.LastFollowUpSentDate.Should().Be(ManilaToday);
	}

	// The whole point of the feature: the email already sitting in the candidate's inbox has to
	// keep working. If this ever fails, the reminder is actively worse than sending nothing -
	// it would retire the link the candidate was about to click.
	[Fact]
	public async Task ReleaseDueFollowUps_ShouldNotRotateTheToken()
	{
		await SetFollowUpDaysAsync(3);
		var createdAt = ManilaDaysAgo(1);
		var id = await SeedOrderAsync(orderCreatedAt: createdAt, hashTokenCreatedAt: createdAt);

		await _atsRepository.ReleaseDueFollowUpInvitationsAsync(CancellationToken.None);

		var updated = await ReadAsync(id);
		updated.HashToken.Should().Be(SeededHashToken);
		updated.HashTokenCreatedAt.Should().BeCloseTo(createdAt, TimeSpan.FromSeconds(1));
	}

	// The column that makes "once a day" true, and the same column the sender reads to choose
	// reminder copy. Stamped in the same UPDATE as the requeue, so a crash cannot leave a row
	// released but undated - which would chase the candidate again on the very next hourly pass
	// AND send that reminder with first-invitation wording.
	[Fact]
	public async Task ReleaseDueFollowUps_ShouldStampLastFollowUpSentDateWithTheManilaDate()
	{
		await SetFollowUpDaysAsync(2);
		var id = await SeedOrderAsync(orderCreatedAt: ManilaDaysAgo(1));

		await _atsRepository.ReleaseDueFollowUpInvitationsAsync(CancellationToken.None);

		var updated = await ReadAsync(id);
		updated.LastFollowUpSentDate.Should().Be(ManilaToday);
	}

	// Once per DAY, not once per pass. The job runs hourly, so without the date guard every
	// remaining pass of the day would chase the same candidate again - up to 24 times.
	[Fact]
	public async Task ReleaseDueFollowUps_ShouldReleaseNothingOnASecondPassWithinTheSameDay()
	{
		await SetFollowUpDaysAsync(3);
		await SeedOrderAsync(orderCreatedAt: ManilaDaysAgo(1));

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
		await SeedOrderAsync(orderCreatedAt: ManilaDaysAgo(30));

		var released = await _atsRepository.ReleaseDueFollowUpInvitationsAsync(CancellationToken.None);

		released.Should().BeEmpty();
	}

	// The stop condition is the COUNT, not the calendar: N=2 stops once two reminders have
	// actually gone out, however long ago that was.
	[Theory]
	[InlineData(3)]
	[InlineData(10)]
	[InlineData(60)]
	public async Task ReleaseDueFollowUps_ShouldSkipTheRow_WhenEveryReminderHasBeenSent(int daysAgo)
	{
		await SetFollowUpDaysAsync(2);
		await SeedOrderAsync(
			orderCreatedAt: ManilaDaysAgo(daysAgo),
			followUpSentCount: 2);

		var released = await _atsRepository.ReleaseDueFollowUpInvitationsAsync(CancellationToken.None);

		released.Should().BeEmpty();
	}

	// The reason the count exists. A reminder is only released for a row that satisfies every
	// rule, so days pass with nothing sent whenever delivery was stuck - and the old
	// time-bounded stop then ended the schedule for exactly the candidates who had received the
	// least. Inside the grace window those sends are still owed and still go out.
	[Theory]
	// Past the nominal 2-day schedule, nothing ever sent.
	[InlineData(3, 0)]
	// Further out, one of the two sent.
	[InlineData(5, 1)]
	public async Task ReleaseDueFollowUps_ShouldStillRelease_WhenSendsWereMissedInsideTheGraceWindow(
		int daysAgo,
		int alreadySent)
	{
		await SetFollowUpDaysAsync(2);
		var id = await SeedOrderAsync(
			orderCreatedAt: ManilaDaysAgo(daysAgo),
			followUpSentCount: alreadySent);

		var released = await _atsRepository.ReleaseDueFollowUpInvitationsAsync(CancellationToken.None);

		released.Should().ContainSingle().Which.EmailInvitationID.Should().Be(id);
	}

	// The backstop. Without an upper bound a never-chased order stays eligible forever, and the
	// daily-reminder migration ships no backfill precisely because the window is what protects
	// the existing backlog. Grace is 7 days, so a 2-reminder package stops at order + 9.
	[Theory]
	[InlineData(10)]
	[InlineData(60)]
	public async Task ReleaseDueFollowUps_ShouldSkipTheRow_WhenItIsPastTheCatchUpGrace(int daysAgo)
	{
		await SetFollowUpDaysAsync(2);
		await SeedOrderAsync(
			orderCreatedAt: ManilaDaysAgo(daysAgo),
			followUpSentCount: 0);

		var released = await _atsRepository.ReleaseDueFollowUpInvitationsAsync(CancellationToken.None);

		released.Should().BeEmpty();
	}

	// The final reminder of the schedule still sends - the boundary belongs to the candidate.
	[Fact]
	public async Task ReleaseDueFollowUps_ShouldReleaseTheRow_OnTheFinalReminderOfTheSchedule()
	{
		await SetFollowUpDaysAsync(2);
		var id = await SeedOrderAsync(
			orderCreatedAt: ManilaDaysAgo(2),
			lastFollowUpSentDate: ManilaToday.AddDays(-1),
			followUpSentCount: 1);

		var released = await _atsRepository.ReleaseDueFollowUpInvitationsAsync(CancellationToken.None);

		released.Should().ContainSingle().Which.EmailInvitationID.Should().Be(id);
	}

	// The counter is incremented in the same UPDATE that stamps the date, so a crash cannot
	// leave a row released but uncounted - which would let the schedule overrun.
	[Fact]
	public async Task ReleaseDueFollowUps_ShouldIncrementFollowUpSentCount()
	{
		await SetFollowUpDaysAsync(3);
		var id = await SeedOrderAsync(
			orderCreatedAt: ManilaDaysAgo(2),
			lastFollowUpSentDate: ManilaToday.AddDays(-1),
			followUpSentCount: 1);

		await _atsRepository.ReleaseDueFollowUpInvitationsAsync(CancellationToken.None);

		var updated = await ReadAsync(id);
		updated.FollowUpSentCount.Should().Be(2);
	}

	// Data screening has no candidate to email. NULL is excluded as well as false: an
	// unclassified order cannot prove it is manual, matching the email claim query.
	[Theory]
	[InlineData(false)]
	[InlineData(null)]
	public async Task ReleaseDueFollowUps_ShouldSkipTheRow_WhenItIsNotManualScreening(bool? autoChasing)
	{
		await SetFollowUpDaysAsync(5);
		await SeedOrderAsync(orderCreatedAt: ManilaDaysAgo(1), autoChasing: autoChasing);

		var released = await _atsRepository.ReleaseDueFollowUpInvitationsAsync(CancellationToken.None);

		released.Should().BeEmpty();
	}

	// Nobody who already dealt with the form gets chased about it.
	[Theory]
	[InlineData("Done")]
	[InlineData("Withdrawn")]
	public async Task ReleaseDueFollowUps_ShouldSkipTheRow_WhenTheFormIsNoLongerPending(string status)
	{
		await SetFollowUpDaysAsync(5);
		await SeedOrderAsync(
			orderCreatedAt: ManilaDaysAgo(1),
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
		await SetFollowUpDaysAsync(5);
		await SeedOrderAsync(
			orderCreatedAt: ManilaDaysAgo(1),
			emailSentStatus: status);

		var released = await _atsRepository.ReleaseDueFollowUpInvitationsAsync(CancellationToken.None);

		released.Should().BeEmpty();
	}

	[Fact]
	public async Task ReleaseDueFollowUps_ShouldSkipTheRow_WhenItWasAlreadyChasedToday()
	{
		await SetFollowUpDaysAsync(5);
		await SeedOrderAsync(
			orderCreatedAt: ManilaDaysAgo(2),
			lastFollowUpSentDate: ManilaToday);

		var released = await _atsRepository.ReleaseDueFollowUpInvitationsAsync(CancellationToken.None);

		released.Should().BeEmpty();
	}

	// An order chased on an earlier day inside its window is due again today. This is the case
	// the retired FollowUpQueuedAt would have blocked, and it is why the date - not a "has ever
	// been queued" stamp - is what the predicate reads.
	[Fact]
	public async Task ReleaseDueFollowUps_ShouldReleaseTheRow_WhenTheLastReminderWasAnEarlierDay()
	{
		await SetFollowUpDaysAsync(5);
		var id = await SeedOrderAsync(
			orderCreatedAt: ManilaDaysAgo(3),
			lastFollowUpSentDate: ManilaToday.AddDays(-2));

		var released = await _atsRepository.ReleaseDueFollowUpInvitationsAsync(CancellationToken.None);

		released.Should().ContainSingle().Which.EmailInvitationID.Should().Be(id);
	}

	#endregion

	#region Helpers

	/// <summary>
	/// Sets the number of daily reminders on the package every seeded order points at. The
	/// default package is created with FollowUpEmail = 0, so a test that wants a chaser has to
	/// say so.
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
		DateTime? hashTokenCreatedAt = null,
		DateOnly? lastFollowUpSentDate = null,
		int followUpSentCount = 0)
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
			LastFollowUpSentDate = lastFollowUpSentDate,
			FollowUpSentCount = followUpSentCount
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
