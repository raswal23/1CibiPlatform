using BuildingBlocks.Exceptions;
using FluentAssertions;
using Moq;
using OMS.Shared.Contracts;
using Test.BackendAPI.Modules.OMS.UnitTests.Fixture;

namespace Test.BackendAPI.Modules.OMS.UnitTests;

public class OMSTicketCreatorTests : IClassFixture<OMSServiceFixture>
{
	private readonly OMSServiceFixture _fixture;

	public OMSTicketCreatorTests(OMSServiceFixture fixture)
	{
		_fixture = fixture;
		_fixture.MockOMSRepository.Reset();
	}

	private void SetupValidRequestor(bool isValid = true) =>
		_fixture.MockOMSRepository
			.Setup(repository => repository.ValidateRequestorAsync(
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(isValid);

	private void SetupValidPONumber(bool isValid = true) =>
		_fixture.MockOMSRepository
			.Setup(repository => repository.ValidatePONumberAsync(
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<int>(),
				It.IsAny<int>(),
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(isValid);

	private void SetupCreateTicket(OMSTicketCreated? ticket) =>
		_fixture.MockOMSRepository
			.Setup(repository => repository.CreateTicketAsync(
				It.IsAny<CreateOMSTicketRequest>(),
				It.IsAny<string>(),
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(ticket);

	[Fact]
	public async Task CreateTicketAsync_ShouldReturnTicket_WhenAllChecksPass()
	{
		var expected = new OMSTicketCreated("202608260001", new DateTime(2026, 8, 31));
		SetupValidRequestor();
		SetupValidPONumber();
		SetupCreateTicket(expected);

		var result = await _fixture.TicketCreator.CreateTicketAsync(
			OMSServiceFixture.CreateValidRequest(),
			CancellationToken.None);

		result.Should().Be(expected);
	}

	[Fact]
	public async Task CreateTicketAsync_ShouldThrowBadRequest_WhenRequestorIsInvalid()
	{
		SetupValidRequestor(isValid: false);

		var act = () => _fixture.TicketCreator.CreateTicketAsync(
			OMSServiceFixture.CreateValidRequest(),
			CancellationToken.None);

		await act.Should().ThrowAsync<BadRequestException>()
			.WithMessage("Requestor is invalid");

		_fixture.MockOMSRepository.Verify(
			repository => repository.ValidatePONumberAsync(
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<int>(),
				It.IsAny<int>(),
				It.IsAny<CancellationToken>()),
			Times.Never);
		_fixture.MockOMSRepository.Verify(
			repository => repository.CreateTicketAsync(
				It.IsAny<CreateOMSTicketRequest>(),
				It.IsAny<string>(),
				It.IsAny<CancellationToken>()),
			Times.Never);
	}

	[Fact]
	public async Task CreateTicketAsync_ShouldThrowBadRequest_WhenPOIsInvalid()
	{
		SetupValidRequestor();
		SetupValidPONumber(isValid: false);

		var act = () => _fixture.TicketCreator.CreateTicketAsync(
			OMSServiceFixture.CreateValidRequest(),
			CancellationToken.None);

		await act.Should().ThrowAsync<BadRequestException>()
			.WithMessage("PO is insufficient or invalid, please contact your manager");

		_fixture.MockOMSRepository.Verify(
			repository => repository.CreateTicketAsync(
				It.IsAny<CreateOMSTicketRequest>(),
				It.IsAny<string>(),
				It.IsAny<CancellationToken>()),
			Times.Never);
	}

	[Fact]
	public async Task CreateTicketAsync_ShouldThrowInternalServerException_WhenCreateReturnsNull()
	{
		SetupValidRequestor();
		SetupValidPONumber();
		SetupCreateTicket(ticket: null);

		var act = () => _fixture.TicketCreator.CreateTicketAsync(
			OMSServiceFixture.CreateValidRequest(),
			CancellationToken.None);

		await act.Should().ThrowAsync<InternalServerException>()
			.WithMessage("Ticket creation failed.");
	}

	[Fact]
	public async Task CreateTicketAsync_ShouldNormalizeNames_BeforePersistence()
	{
		CreateOMSTicketRequest? persistedRequest = null;
		SetupValidRequestor();
		SetupValidPONumber();
		_fixture.MockOMSRepository
			.Setup(repository => repository.CreateTicketAsync(
				It.IsAny<CreateOMSTicketRequest>(),
				It.IsAny<string>(),
				It.IsAny<CancellationToken>()))
			.Callback<CreateOMSTicketRequest, string, CancellationToken>(
				(request, _, _) => persistedRequest = request)
			.ReturnsAsync(new OMSTicketCreated("202608260001", new DateTime(2026, 8, 31)));

		var request = OMSServiceFixture.CreateValidRequest() with
		{
			FirstName = "  TEST   Enzo ",
			MiddleName = " TEST  ",
			LastName = " TEST  Evangelista "
		};

		await _fixture.TicketCreator.CreateTicketAsync(request, CancellationToken.None);

		persistedRequest.Should().NotBeNull();
		persistedRequest!.FirstName.Should().Be("TEST Enzo");
		persistedRequest.MiddleName.Should().Be("TEST");
		persistedRequest.LastName.Should().Be("TEST Evangelista");
	}

	[Fact]
	public async Task CreateTicketAsync_ShouldPassEmptyReferenceNumber_WhenCreatingTicket()
	{
		string? persistedReferenceNumber = null;
		SetupValidRequestor();
		SetupValidPONumber();
		_fixture.MockOMSRepository
			.Setup(repository => repository.CreateTicketAsync(
				It.IsAny<CreateOMSTicketRequest>(),
				It.IsAny<string>(),
				It.IsAny<CancellationToken>()))
			.Callback<CreateOMSTicketRequest, string, CancellationToken>(
				(_, referenceNumber, _) => persistedReferenceNumber = referenceNumber)
			.ReturnsAsync(new OMSTicketCreated("202608260001", new DateTime(2026, 8, 31)));

		await _fixture.TicketCreator.CreateTicketAsync(
			OMSServiceFixture.CreateValidRequest(),
			CancellationToken.None);

		persistedReferenceNumber.Should().Be(string.Empty);
	}

	[Fact]
	public async Task CreateTicketAsync_ShouldPropagateCancellation_WhenRepositoryIsCancelled()
	{
		_fixture.MockOMSRepository
			.Setup(repository => repository.ValidateRequestorAsync(
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<CancellationToken>()))
			.ThrowsAsync(new OperationCanceledException());

		var act = () => _fixture.TicketCreator.CreateTicketAsync(
			OMSServiceFixture.CreateValidRequest(),
			CancellationToken.None);

		await act.Should().ThrowAsync<OperationCanceledException>();
	}
}
