using ATS.Constants;
using ATS.Data.DTO;
using ATS.Data.Entities;
using ATS.Data.Repository.OMSTicketing;
using ATS.Services.OMSTicketing;
using Auth.DTO;
using Auth.Shared.Contracts;
using BuildingBlocks.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using OMS.Shared.Contracts;

namespace Test.BackendAPI.Modules.ATS.UnitTests;

public class OMSTicketingProcessorServiceTests
{
	private readonly Mock<IOMSTicketingRepository> _repository = new();
	private readonly Mock<IOMSTicketCreator> _ticketCreator = new();
	private readonly Mock<IAuthQueries> _authQueries = new();
	private readonly OMSTicketingProcessorService _service;

	private static readonly Guid RequestorId = Guid.CreateVersion7();

	public OMSTicketingProcessorServiceTests()
	{
		var scope = new Mock<IServiceScope>();
		var provider = new Mock<IServiceProvider>();
		var scopeFactory = new Mock<IServiceScopeFactory>();

		provider.Setup(x => x.GetService(typeof(IOMSTicketingRepository))).Returns(_repository.Object);
		provider.Setup(x => x.GetService(typeof(IOMSTicketCreator))).Returns(_ticketCreator.Object);
		provider.Setup(x => x.GetService(typeof(IAuthQueries))).Returns(_authQueries.Object);

		scope.Setup(x => x.ServiceProvider).Returns(provider.Object);
		scopeFactory.Setup(x => x.CreateScope()).Returns(scope.Object);

		_authQueries
			.Setup(x => x.GetATSAssignedUserAsync(RequestorId, It.IsAny<CancellationToken>()))
			.ReturnsAsync(new ATSUserLookupDTO
			{
				UserId = RequestorId,
				FirstName = "John",
				LastName = "Doe",
				UserEmail = "john.doe@example.com"
			});

		_service = new OMSTicketingProcessorService(
			new Mock<ILogger<OMSTicketingProcessorService>>().Object,
			_repository.Object,
			scopeFactory.Object);
	}

	private static EmailInvitationRequest ClaimedOrder(Guid id) => new()
	{
		EmailInvitationID = id,
		TicketStatus = TicketStatus.Processing
	};

	private static TicketablePayloadDTO TicketablePayload(Guid id) => new()
	{
		EmailInvitationID = id,
		FirstName = "Juan",
		LastName = "Dela Cruz",
		EmailAddress = "juan@example.com",
		MobileNumber = "09171234567",
		SelectPackage = "CRIMINAL RECORDS CHECK",
		PackageDescription = "182",
		Site = "24 - 7 INTOUCH- CEBU",
		RequestorId = RequestorId,
		RequestorEmail = "john.doe@example.com"
	};

	private void GivenClaimed(params TicketablePayloadDTO[] payloads)
	{
		_repository
			.Setup(x => x.ClaimPendingTicketsAsync(It.IsAny<CancellationToken>()))
			.ReturnsAsync(payloads.Select(p => ClaimedOrder(p.EmailInvitationID)).ToList());

		_repository
			.Setup(x => x.GetTicketPayloadsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(payloads.ToList());
	}

	[Fact]
	public async Task ProcessAsync_ShouldDoNothing_WhenNoOrderIsClaimed()
	{
		_repository
			.Setup(x => x.ClaimPendingTicketsAsync(It.IsAny<CancellationToken>()))
			.ReturnsAsync([]);

		await _service.ProcessAsync(CancellationToken.None);

		_ticketCreator.Verify(
			x => x.CreateTicketAsync(It.IsAny<CreateOMSTicketRequest>(), It.IsAny<CancellationToken>(), It.IsAny<string>()),
			Times.Never);
	}

	[Fact]
	public async Task ProcessAsync_ShouldReleaseStaleClaims_BeforeClaimingTheNextBatch()
	{
		_repository
			.Setup(x => x.ClaimPendingTicketsAsync(It.IsAny<CancellationToken>()))
			.ReturnsAsync([]);

		await _service.ProcessAsync(CancellationToken.None);

		_repository.Verify(
			x => x.ReleaseStaleTicketClaimsAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
			Times.Once);
	}

	[Fact]
	public async Task ProcessAsync_ShouldRecordTheTicket_WhenOMSAcceptsTheOrder()
	{
		var id = Guid.CreateVersion7();
		GivenClaimed(TicketablePayload(id));

		var deliveryDate = new DateTime(2026, 9, 1);

		_ticketCreator
			.Setup(x => x.CreateTicketAsync(
				It.IsAny<CreateOMSTicketRequest>(),
				It.IsAny<CancellationToken>(),
				It.IsAny<string>()))
			.ReturnsAsync(new OMSTicketCreated("202608260001", deliveryDate));

		await _service.ProcessAsync(CancellationToken.None);

		_repository.Verify(
			x => x.MarkTicketedAsync(id, "202608260001", deliveryDate, It.IsAny<CancellationToken>()),
			Times.Once);
	}

	[Fact]
	public async Task ProcessAsync_ShouldSendTheInvitationIdAsTheReferenceNumber()
	{
		var id = Guid.CreateVersion7();
		GivenClaimed(TicketablePayload(id));

		_ticketCreator
			.Setup(x => x.CreateTicketAsync(
				It.IsAny<CreateOMSTicketRequest>(),
				It.IsAny<CancellationToken>(),
				It.IsAny<string>()))
			.ReturnsAsync(new OMSTicketCreated("202608260001", DateTime.UtcNow));

		await _service.ProcessAsync(CancellationToken.None);

		// Ties the OMS ticket back to the ATS order, and lets OMS recognise a ticket a
		// retry already created.
		_ticketCreator.Verify(
			x => x.CreateTicketAsync(
				It.IsAny<CreateOMSTicketRequest>(),
				It.IsAny<CancellationToken>(),
				id.ToString("D")),
			Times.Once);
	}

	[Fact]
	public async Task ProcessAsync_ShouldParkWithoutCallingOMS_WhenTheOrderCannotBeMapped()
	{
		var id = Guid.CreateVersion7();
		var payload = TicketablePayload(id);

		// A renamed or deleted package no longer resolves to an OMS report type.
		payload.PackageDescription = null;

		GivenClaimed(payload);

		await _service.ProcessAsync(CancellationToken.None);

		_ticketCreator.Verify(
			x => x.CreateTicketAsync(It.IsAny<CreateOMSTicketRequest>(), It.IsAny<CancellationToken>(), It.IsAny<string>()),
			Times.Never);

		_repository.Verify(
			x => x.MarkTicketFailedAsync(
				It.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(id)),
				It.IsAny<string>(),
				false,
				It.IsAny<CancellationToken>()),
			Times.Once);
	}

	[Fact]
	public async Task ProcessAsync_ShouldParkPermanently_WhenOMSRejectsTheRequest()
	{
		var id = Guid.CreateVersion7();
		GivenClaimed(TicketablePayload(id));

		_ticketCreator
			.Setup(x => x.CreateTicketAsync(
				It.IsAny<CreateOMSTicketRequest>(),
				It.IsAny<CancellationToken>(),
				It.IsAny<string>()))
			.ThrowsAsync(new BadRequestException("PO is insufficient or invalid, please contact your manager"));

		await _service.ProcessAsync(CancellationToken.None);

		// A business rejection cannot be fixed by retrying, so it must not be retryable.
		_repository.Verify(
			x => x.MarkTicketFailedAsync(
				It.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(id)),
				It.Is<string>(reason => reason.Contains("PO is insufficient")),
				false,
				It.IsAny<CancellationToken>()),
			Times.Once);

		_repository.Verify(
			x => x.MarkTicketedAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
			Times.Never);
	}

	[Fact]
	public async Task ProcessAsync_ShouldAllowARetry_WhenTheOMSCallFailsTransiently()
	{
		var id = Guid.CreateVersion7();
		GivenClaimed(TicketablePayload(id));

		_ticketCreator
			.Setup(x => x.CreateTicketAsync(
				It.IsAny<CreateOMSTicketRequest>(),
				It.IsAny<CancellationToken>(),
				It.IsAny<string>()))
			.ThrowsAsync(new InternalServerException("An error occurred while contacting the OMS database."));

		await _service.ProcessAsync(CancellationToken.None);

		_repository.Verify(
			x => x.MarkTicketFailedAsync(
				It.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(id)),
				It.IsAny<string>(),
				true,
				It.IsAny<CancellationToken>()),
			Times.Once);
	}

	[Fact]
	public async Task ProcessAsync_ShouldTicketTheOtherOrders_WhenOneOrderFails()
	{
		var failing = TicketablePayload(Guid.CreateVersion7());
		var succeeding = TicketablePayload(Guid.CreateVersion7());

		GivenClaimed(failing, succeeding);

		_ticketCreator
			.Setup(x => x.CreateTicketAsync(
				It.Is<CreateOMSTicketRequest>(r => r.EmailAddress == failing.EmailAddress),
				It.IsAny<CancellationToken>(),
				failing.EmailInvitationID.ToString("D")))
			.ThrowsAsync(new InternalServerException("boom"));

		_ticketCreator
			.Setup(x => x.CreateTicketAsync(
				It.IsAny<CreateOMSTicketRequest>(),
				It.IsAny<CancellationToken>(),
				succeeding.EmailInvitationID.ToString("D")))
			.ReturnsAsync(new OMSTicketCreated("202608260002", DateTime.UtcNow));

		await _service.ProcessAsync(CancellationToken.None);

		// One bad order must not poison the rest of the batch.
		_repository.Verify(
			x => x.MarkTicketedAsync(
				succeeding.EmailInvitationID,
				"202608260002",
				It.IsAny<DateTime>(),
				It.IsAny<CancellationToken>()),
			Times.Once);
	}

	[Fact]
	public async Task ProcessAsync_ShouldParkClaimedOrders_WhenTheirPayloadCannotBeLoaded()
	{
		var id = Guid.CreateVersion7();

		_repository
			.Setup(x => x.ClaimPendingTicketsAsync(It.IsAny<CancellationToken>()))
			.ReturnsAsync([ClaimedOrder(id)]);

		// Claimed, but the joined read returned nothing for it.
		_repository
			.Setup(x => x.GetTicketPayloadsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync([]);

		await _service.ProcessAsync(CancellationToken.None);

		// Otherwise the row sits in Processing until the stale sweeper releases it.
		_repository.Verify(
			x => x.MarkTicketFailedAsync(
				It.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(id)),
				It.IsAny<string>(),
				false,
				It.IsAny<CancellationToken>()),
			Times.Once);
	}
}
