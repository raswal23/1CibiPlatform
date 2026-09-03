using ATS.Constants;
using ATS.Data.Entities;
using ATS.Data.Repository.OMSTicketing;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Test.BackendAPI.Infrastructure.ATS.Infrastracture;

namespace Test.BackendAPI.Modules.ATS.IntegrationTests;

public class OMSTicketingRepositoryIntegrationTests : BaseIntegrationTest
{
	private readonly OMSTicketingRepository _repository;

	public OMSTicketingRepositoryIntegrationTests(IntegrationTestWebAppFactory factory)
		: base(factory)
	{
		_repository = new OMSTicketingRepository(_dbContext);
	}

	private async Task<EmailInvitationRequest> SeedQueuedOrderAsync(
		string ticketStatus = TicketStatus.Pending,
		int ticketAttempts = 0,
		bool isTicketed = false)
	{
		var order = new EmailInvitationRequest
		{
			EmailInvitationID = Guid.CreateVersion7(),
			FirstName = "Juan",
			LastName = "Dela Cruz",
			EmailAddress = "juan@example.com",
			MobileNumber = "09171234567",
			PackageId = DefaultPackageId,
			SelectPackage = "CRIMINAL RECORDS CHECK",
			RushNormal = "Normal",
			HashToken = Guid.NewGuid().ToString("N"),
			HashTokenCreatedAt = DateTime.UtcNow,
			HashTokenExpiration = DateTime.UtcNow.AddHours(24),
			EmailSentStatus = "Pending",
			ApplicationFormStatus = "Pending",
			OrderStatus = "Pending Candidate Info",
			OrderCreatedAt = DateTime.UtcNow,
			TicketStatus = ticketStatus,
			TicketAttempts = ticketAttempts,
			IsTicketed = isTicketed
		};

		_dbContext.EmailInvitationRequests.Add(order);
		await _dbContext.SaveChangesAsync();

		return order;
	}

	// The reason orders reference their package by id. Before that change the ticketing
	// join matched on the package *name*, so renaming a package silently orphaned every
	// order that referenced it: the order kept the old string, stopped matching, and
	// parked as an error nobody could explain.
	[Fact]
	public async Task GetTicketPayloadsAsync_ShouldStillResolveThePackage_AfterItHasBeenRenamed()
	{
		var order = await SeedQueuedOrderAsync();

		var package = await _dbContext.PackageDetails
			.FirstAsync(x => x.PackageId == DefaultPackageId);

		package.PackageName = "Renamed After The Order Was Placed";
		await _dbContext.SaveChangesAsync();

		var payloads = await _repository.GetTicketPayloadsAsync(
			[order.EmailInvitationID],
			CancellationToken.None);

		var payload = payloads.Should().ContainSingle().Subject;

		// Resolved through the foreign key, so the report type is still found.
		payload.PackageDescription.Should().Be("182");
	}

	[Fact]
	public async Task GetTicketPayloadsAsync_ShouldReturnTheOrder_EvenWhenItsPackageIsInactive()
	{
		var order = await SeedQueuedOrderAsync();

		var package = await _dbContext.PackageDetails
			.FirstAsync(x => x.PackageId == DefaultPackageId);

		package.IsActive = false;
		await _dbContext.SaveChangesAsync();

		var payloads = await _repository.GetTicketPayloadsAsync(
			[order.EmailInvitationID],
			CancellationToken.None);

		// An order already placed is still ticketed; deactivating a package stops new
		// orders being created against it, not existing ones from completing.
		payloads.Should().ContainSingle();
	}

	// Regression: the OMS delivery date arrives from SQL Server with
	// Kind=Unspecified, which Npgsql refuses to write to a timestamptz column
	// ("Cannot write DateTime with Kind=Unspecified to PostgreSQL type
	// 'timestamp with time zone'"). The repository must normalise it.
	[Fact]
	public async Task MarkTicketedAsync_ShouldPersist_WhenTheDeliveryDateKindIsUnspecified()
	{
		var order = await SeedQueuedOrderAsync();

		var unspecifiedDeliveryDate = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Unspecified);

		var act = async () => await _repository.MarkTicketedAsync(
			order.EmailInvitationID,
			"202608260001",
			unspecifiedDeliveryDate,
			CancellationToken.None);

		await act.Should().NotThrowAsync();

		var saved = await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.FirstAsync(x => x.EmailInvitationID == order.EmailInvitationID);

		saved.IsTicketed.Should().BeTrue();
		saved.TicketStatus.Should().Be(TicketStatus.Done);
		saved.TicketNumber.Should().Be("202608260001");
		saved.TicketDeliveryDate.Should().NotBeNull();
		saved.TicketClaimedAt.Should().BeNull();
	}

	[Fact]
	public async Task MarkTicketedAsync_ShouldPersist_WhenTheDeliveryDateIsAlreadyUtc()
	{
		var order = await SeedQueuedOrderAsync();

		var utcDeliveryDate = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);

		await _repository.MarkTicketedAsync(
			order.EmailInvitationID,
			"202608260002",
			utcDeliveryDate,
			CancellationToken.None);

		var saved = await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.FirstAsync(x => x.EmailInvitationID == order.EmailInvitationID);

		// An already-UTC value is stored unchanged.
		saved.TicketDeliveryDate!.Value.ToUniversalTime().Should().Be(utcDeliveryDate);
	}

	[Fact]
	public async Task ClaimPendingTicketsAsync_ShouldClaimAPendingOrderExactlyOnce()
	{
		var order = await SeedQueuedOrderAsync();

		var firstPass = await _repository.ClaimPendingTicketsAsync(CancellationToken.None);
		var secondPass = await _repository.ClaimPendingTicketsAsync(CancellationToken.None);

		firstPass.Should().Contain(x => x.EmailInvitationID == order.EmailInvitationID);

		// The Processing write is the durable claim, so a second pass must not see it.
		secondPass.Should().NotContain(x => x.EmailInvitationID == order.EmailInvitationID);
	}

	[Fact]
	public async Task ClaimPendingTicketsAsync_ShouldNotReclaim_AnOrderThatIsAlreadyTicketed()
	{
		var order = await SeedQueuedOrderAsync();

		await _repository.MarkTicketedAsync(
			order.EmailInvitationID,
			"202608260003",
			new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Unspecified),
			CancellationToken.None);

		var claimed = await _repository.ClaimPendingTicketsAsync(CancellationToken.None);

		claimed.Should().NotContain(x => x.EmailInvitationID == order.EmailInvitationID);
	}

	[Fact]
	public async Task MarkTicketFailedAsync_ShouldExhaustTheBudget_WhenTheFailureIsNotRetryable()
	{
		var order = await SeedQueuedOrderAsync();

		await _repository.MarkTicketFailedAsync(
			[order.EmailInvitationID],
			"No active package matches the order.",
			isRetryable: false,
			CancellationToken.None);

		var saved = await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.FirstAsync(x => x.EmailInvitationID == order.EmailInvitationID);

		saved.TicketStatus.Should().Be(TicketStatus.Error);
		saved.TicketError.Should().Be("No active package matches the order.");

		// A condition that cannot resolve itself must not be retried every tick.
		var claimed = await _repository.ClaimPendingTicketsAsync(CancellationToken.None);
		claimed.Should().NotContain(x => x.EmailInvitationID == order.EmailInvitationID);
	}

	[Fact]
	public async Task MarkTicketFailedAsync_ShouldAllowAnotherAttempt_WhenTheFailureIsRetryable()
	{
		var order = await SeedQueuedOrderAsync();

		await _repository.MarkTicketFailedAsync(
			[order.EmailInvitationID],
			"An error occurred while contacting the OMS database.",
			isRetryable: true,
			CancellationToken.None);

		var saved = await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.FirstAsync(x => x.EmailInvitationID == order.EmailInvitationID);

		saved.TicketStatus.Should().Be(TicketStatus.Error);
		saved.TicketAttempts.Should().Be(1);

		// Still under the cap, so the next tick picks it up again.
		var claimed = await _repository.ClaimPendingTicketsAsync(CancellationToken.None);
		claimed.Should().Contain(x => x.EmailInvitationID == order.EmailInvitationID);
	}

	// The test that actually proves the retry feature: the requeued row must be visible
	// to the real claim SQL again, not merely have different column values.
	[Fact]
	public async Task RequeueExhaustedTicketAsync_ShouldPutAnExhaustedOrderBackOnTheQueue()
	{
		var order = await SeedQueuedOrderAsync(
			TicketStatus.Error,
			ticketAttempts: OMSTicketingRepository.MaxTicketAttempts);

		var requeued = await _repository.RequeueExhaustedTicketAsync(
			order.EmailInvitationID,
			CancellationToken.None);

		requeued.Should().BeTrue();

		var saved = await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.FirstAsync(x => x.EmailInvitationID == order.EmailInvitationID);

		saved.TicketStatus.Should().Be(TicketStatus.Pending);
		saved.TicketAttempts.Should().Be(0);
		saved.TicketError.Should().BeNull();

		var claimed = await _repository.ClaimPendingTicketsAsync(CancellationToken.None);
		claimed.Should().Contain(x => x.EmailInvitationID == order.EmailInvitationID);
	}

	[Fact]
	public async Task RequeueExhaustedTicketAsync_ShouldRefuse_WhenTheOrderIsStillAutoRetrying()
	{
		var order = await SeedQueuedOrderAsync(TicketStatus.Error, ticketAttempts: 2);

		var requeued = await _repository.RequeueExhaustedTicketAsync(
			order.EmailInvitationID,
			CancellationToken.None);

		// Below the cap the job still owns the order, so there is nothing to retry.
		requeued.Should().BeFalse();

		var saved = await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.FirstAsync(x => x.EmailInvitationID == order.EmailInvitationID);

		saved.TicketAttempts.Should().Be(2);
	}

	[Fact]
	public async Task RequeueExhaustedTicketAsync_ShouldRefuse_WhenTheOrderIsAlreadyTicketed()
	{
		var order = await SeedQueuedOrderAsync(
			TicketStatus.Error,
			ticketAttempts: OMSTicketingRepository.MaxTicketAttempts,
			isTicketed: true);

		var requeued = await _repository.RequeueExhaustedTicketAsync(
			order.EmailInvitationID,
			CancellationToken.None);

		// Requeuing a ticketed order would raise a second ticket in OMS.
		requeued.Should().BeFalse();
	}

	[Theory]
	[InlineData(TicketStatus.Pending)]
	[InlineData(TicketStatus.Processing)]
	[InlineData(TicketStatus.Done)]
	public async Task RequeueExhaustedTicketAsync_ShouldRefuse_WhenTheOrderIsNotParked(string ticketStatus)
	{
		var order = await SeedQueuedOrderAsync(
			ticketStatus,
			ticketAttempts: OMSTicketingRepository.MaxTicketAttempts);

		var requeued = await _repository.RequeueExhaustedTicketAsync(
			order.EmailInvitationID,
			CancellationToken.None);

		requeued.Should().BeFalse();
	}

	[Fact]
	public async Task RequeueExhaustedTicketAsync_ShouldBeIdempotent_WhenCalledTwice()
	{
		var order = await SeedQueuedOrderAsync(
			TicketStatus.Error,
			ticketAttempts: OMSTicketingRepository.MaxTicketAttempts);

		var first = await _repository.RequeueExhaustedTicketAsync(
			order.EmailInvitationID,
			CancellationToken.None);

		var second = await _repository.RequeueExhaustedTicketAsync(
			order.EmailInvitationID,
			CancellationToken.None);

		// Two operators clicking at once must not both succeed; the second sees the
		// row is already queued and reports it.
		first.Should().BeTrue();
		second.Should().BeFalse();
	}

	[Fact]
	public async Task RequeueExhaustedTicketAsync_ShouldRestoreTheFullBudget_SoTheOrderCanParkAgain()
	{
		var order = await SeedQueuedOrderAsync(
			TicketStatus.Error,
			ticketAttempts: OMSTicketingRepository.MaxTicketAttempts);

		await _repository.RequeueExhaustedTicketAsync(order.EmailInvitationID, CancellationToken.None);

		// One transient failure after the retry: still well under the cap, so the job
		// keeps it rather than the row being permanently retryable or immediately dead.
		await _repository.MarkTicketFailedAsync(
			[order.EmailInvitationID],
			"contacting OMS failed",
			isRetryable: true,
			CancellationToken.None);

		var saved = await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.FirstAsync(x => x.EmailInvitationID == order.EmailInvitationID);

		saved.TicketAttempts.Should().Be(1);

		var claimed = await _repository.ClaimPendingTicketsAsync(CancellationToken.None);
		claimed.Should().Contain(x => x.EmailInvitationID == order.EmailInvitationID);
	}

	[Fact]
	public async Task GetRetryTargetAsync_ShouldReturnTheScopeIdentityAndOrderStatus()
	{
		var order = await SeedQueuedOrderAsync(
			TicketStatus.Error,
			ticketAttempts: OMSTicketingRepository.MaxTicketAttempts);

		var target = await _repository.GetRetryTargetAsync(
			order.EmailInvitationID,
			CancellationToken.None);

		target.Should().NotBeNull();
		target!.EmailInvitationID.Should().Be(order.EmailInvitationID);
		target.OrderStatus.Should().Be(order.OrderStatus);
	}

	[Fact]
	public async Task GetRetryTargetAsync_ShouldReturnNull_WhenTheOrderDoesNotExist()
	{
		var target = await _repository.GetRetryTargetAsync(
			Guid.CreateVersion7(),
			CancellationToken.None);

		target.Should().BeNull();
	}

	[Fact]
	public async Task MarkTicketFailedAsync_ShouldTruncateAnOverlongReason_ToTheColumnWidth()
	{
		var order = await SeedQueuedOrderAsync();

		await _repository.MarkTicketFailedAsync(
			[order.EmailInvitationID],
			new string('x', 900),
			isRetryable: true,
			CancellationToken.None);

		var saved = await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.FirstAsync(x => x.EmailInvitationID == order.EmailInvitationID);

		// The write that records a failure must not itself fail on column width.
		saved.TicketError.Should().HaveLength(500);
	}
}
