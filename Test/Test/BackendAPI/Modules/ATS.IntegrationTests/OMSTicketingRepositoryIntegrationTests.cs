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

	private async Task<EmailInvitationRequest> SeedQueuedOrderAsync()
	{
		var order = new EmailInvitationRequest
		{
			EmailInvitationID = Guid.CreateVersion7(),
			FirstName = "Juan",
			LastName = "Dela Cruz",
			EmailAddress = "juan@example.com",
			MobileNumber = "09171234567",
			SelectPackage = "CRIMINAL RECORDS CHECK",
			RushNormal = "Normal",
			HashToken = Guid.NewGuid().ToString("N"),
			HashTokenCreatedAt = DateTime.UtcNow,
			HashTokenExpiration = DateTime.UtcNow.AddHours(24),
			EmailSentStatus = "Pending",
			ApplicationFormStatus = "Pending",
			OrderStatus = "Pending Candidate Info",
			OrderCreatedAt = DateTime.UtcNow,
			TicketStatus = TicketStatus.Pending,
			IsTicketed = false
		};

		_dbContext.EmailInvitationRequests.Add(order);
		await _dbContext.SaveChangesAsync();

		return order;
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
