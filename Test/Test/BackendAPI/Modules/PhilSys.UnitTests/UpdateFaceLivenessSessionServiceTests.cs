using BuildingBlocks.Exceptions;
using FluentAssertions;
using Moq;
using PhilSys.Data.Entities;
using PhilSys.DTO;
using Test.BackendAPI.Modules.PhilSys.UnitTests.Fixture;

namespace Test.BackendAPI.Modules.PhilSys.UnitTests
{
	public class UpdateFaceLivenessSessionServiceTests : IClassFixture<PhilSysServiceFixture>
	{
		private readonly PhilSysServiceFixture _fixture;

		private const string HashToken = "hash-token";
		private const string FaceLivenessSessionId = "valid-session-id";

		public UpdateFaceLivenessSessionServiceTests(PhilSysServiceFixture fixture)
		{
			_fixture = fixture;
		}

		private static PhilSysTransaction CreateTransaction(string inquiryType) => new PhilSysTransaction
		{
			Tid = Guid.CreateVersion7(),
			InquiryType = inquiryType,
			FirstName = "JUAN",
			MiddleName = "BITAW",
			LastName = "DELA CRUZ",
			Suffix = null,
			BirthDate = "2001-08-20",
			PCN = "6786785465456459",
			FaceLivenessSessionId = FaceLivenessSessionId,
			HashToken = HashToken,
			WebHookUrl = "/",
			CreatedAt = DateTime.UtcNow,
			ExpiresAt = DateTime.UtcNow.AddMinutes(5)
		};

		private static BasicInformationOrPCNResponseDTO CreateResponse(string? reference = "11111111111111111111") => new BasicInformationOrPCNResponseDTO
		(
			code: "GASHJDG123",
			token: "111111111111111111111111111111",
			reference: reference!,
			face_url: "https://ekycbucket/link",
			full_name: "JUAN BITAW DELA CRUZ",
			first_name: "JUAN",
			middle_name: "BITAW",
			last_name: "DELA CRUZ",
			suffix: null,
			gender: "Male",
			marital_status: "Single",
			blood_type: "Unknown",
			email: "N/A",
			mobile_number: "09194224524",
			birth_date: "2001-08-20",
			full_address: "123 PUROK 7, BAGONG SILANG, QUEZON CITY, METRO MANILA, PHILIPPINES, 1101",
			address_line_1: "123 PUROK 7",
			address_line_2: null,
			barangay: "Bagong Silang",
			municipality: "Quezon City",
			province: "Metro Manila",
			country: "Philippines",
			postal_code: "1101",
			present_full_address: "45 PUROK 3, SAN ISIDRO, MAKATI CITY, METRO MANILA, PHILIPPINES, 1210",
			present_address_line_1: "45 PUROK 3",
			present_address_line_2: null,
			present_barangay: "San Isidro",
			present_municipality: "Makati City",
			present_province: "Metro Manila",
			present_country: "Philippines",
			present_postal_code: "1210",
			residency_status: "Filipino",
			place_of_birth: "BACOLOD CITY, NEGROS OCCIDENTAL",
			pob_municipality: "Bacolod City",
			pob_province: "Negros Occidental",
			pob_country: "Philippines"
		);

		// The fixture is shared across tests in this class, so every test must clear
		// recorded invocations and re-setup every mock member it depends on.
		private void SetupFullFlow(PhilSysTransaction transaction, BasicInformationOrPCNResponseDTO response)
		{
			_fixture.MockPhilSysRepository.Invocations.Clear();
			_fixture.MockPhilSysService.Invocations.Clear();
			_fixture.MockUnitOfWork.Invocations.Clear();

			_fixture.MockPhilSysRepository
				.Setup(x => x.UpdateFaceLivenessSessionAsync(HashToken, FaceLivenessSessionId))
				.ReturnsAsync(transaction);
			_fixture.MockPhilSysRepository
				.Setup(x => x.GetTransactionDataByHashTokenAsync(It.IsAny<string>()))
				.ReturnsAsync(transaction);
			_fixture.MockPhilSysRepository
				.Setup(x => x.UpdateTransactionDataAsync(It.IsAny<PhilSysTransaction>()))
				.ReturnsAsync(transaction);
			_fixture.MockPhilSysRepository
				.Setup(x => x.AddTransactionResultDataAsync(It.IsAny<PhilSysTransactionResult>()))
				.ReturnsAsync(true);
			_fixture.MockPhilSysService
				.Setup(x => x.GetPhilsysTokenAsync(It.IsAny<string>(), It.IsAny<string>()))
				.ReturnsAsync("fake-access-token");
			_fixture.MockPhilSysService
				.Setup(x => x.PostBasicInformationAsync(It.IsAny<string>(),
					It.IsAny<string>(),
					It.IsAny<string>(),
					It.IsAny<string>(),
					It.IsAny<string>(),
					It.IsAny<string>(),
					It.IsAny<string>()))
				.ReturnsAsync(response);
			_fixture.MockPhilSysService
				.Setup(x => x.PostPCNAsync(It.IsAny<string>(),
					It.IsAny<string>(),
					It.IsAny<string>()))
				.ReturnsAsync(response);
		}

		[Fact]
		public async Task UpdateFaceLivenessSessionAsync_ShouldThrow_WhenUpdateFaceLivenessSessionFails()
		{
			// Arrange
			var service = _fixture.UpdateFaceLivenessSessionService;
			SetupFullFlow(CreateTransaction("pcn"), CreateResponse());
			_fixture.MockPhilSysRepository
				.Setup(x => x.UpdateFaceLivenessSessionAsync(HashToken, FaceLivenessSessionId))
				.ReturnsAsync((PhilSysTransaction?)null);

			// Act
			Func<Task> act = async () => await service.UpdateFaceLivenessSessionAsync(
				HashToken,
				FaceLivenessSessionId
			);

			// Assert
			await act.Should().ThrowAsync<InternalServerException>().WithMessage("No transaction record found for your Token. Face Liveness Session update aborted.");
		}

		[Fact]
		public async Task UpdateFaceLivenessSessionAsync_ShouldThrowAndRollback_WhenTransactionNotFoundDuringStatusUpdate()
		{
			// Arrange
			var service = _fixture.UpdateFaceLivenessSessionService;
			SetupFullFlow(CreateTransaction("pcn"), CreateResponse());
			_fixture.MockPhilSysRepository
				.Setup(x => x.GetTransactionDataByHashTokenAsync(It.IsAny<string>()))
				.ReturnsAsync((PhilSysTransaction?)null);

			// Act
			Func<Task> act = async () => await service.UpdateFaceLivenessSessionAsync(
				HashToken,
				FaceLivenessSessionId
			);

			// Assert
			await act.Should().ThrowAsync<InternalServerException>().WithMessage("Failed to add transaction.*");
			_fixture.MockUnitOfWork.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
			_fixture.MockUnitOfWork.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
		}

		[Fact]
		public async Task UpdateFaceLivenessSessionAsync_ShouldThrowAndRollback_WhenFailedToUpdateTransactionData()
		{
			// Arrange
			var service = _fixture.UpdateFaceLivenessSessionService;
			SetupFullFlow(CreateTransaction("pcn"), CreateResponse());
			_fixture.MockPhilSysRepository
				.Setup(x => x.UpdateTransactionDataAsync(It.IsAny<PhilSysTransaction>()))
				.ReturnsAsync((PhilSysTransaction?)null);

			// Act
			Func<Task> act = async () => await service.UpdateFaceLivenessSessionAsync(
				HashToken,
				FaceLivenessSessionId
			);

			// Assert
			await act.Should().ThrowAsync<InternalServerException>().WithMessage("Failed to add transaction.*");
			_fixture.MockUnitOfWork.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
			_fixture.MockUnitOfWork.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
		}

		[Fact]
		public async Task UpdateFaceLivenessSessionAsync_ShouldMarkTransacted_WhenPcnIsSuccessful()
		{
			// Arrange
			var service = _fixture.UpdateFaceLivenessSessionService;
			SetupFullFlow(CreateTransaction("pcn"), CreateResponse());

			// Act
			var result = await service.UpdateFaceLivenessSessionAsync(
				HashToken,
				FaceLivenessSessionId
			);

			// Assert
			result!.verified.Should().BeTrue();
			_fixture.MockPhilSysRepository.Verify(x => x.UpdateTransactionDataAsync(It.IsAny<PhilSysTransaction>()), Times.Once);
			_fixture.MockUnitOfWork.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
		}

		[Fact]
		public async Task UpdateFaceLivenessSessionAsync_ShouldMarkTransacted_WhenNameDobIsSuccessful()
		{
			// Arrange
			var service = _fixture.UpdateFaceLivenessSessionService;
			SetupFullFlow(CreateTransaction("name_dob"), CreateResponse());

			// Act
			var result = await service.UpdateFaceLivenessSessionAsync(
				HashToken,
				FaceLivenessSessionId
			);

			// Assert
			result!.verified.Should().BeTrue();
			_fixture.MockPhilSysRepository.Verify(x => x.UpdateTransactionDataAsync(It.IsAny<PhilSysTransaction>()), Times.Once);
			_fixture.MockUnitOfWork.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
		}

		[Theory]
		[InlineData("pcn")]
		[InlineData("name_dob")]
		public async Task UpdateFaceLivenessSessionAsync_ShouldMarkTransacted_WhenNoDataFound(string inquiryType)
		{
			// Arrange - PhilSys answered (2xx) but found no matching record: empty reference
			var service = _fixture.UpdateFaceLivenessSessionService;
			SetupFullFlow(CreateTransaction(inquiryType), CreateResponse(reference: null));

			// Act
			var result = await service.UpdateFaceLivenessSessionAsync(
				HashToken,
				FaceLivenessSessionId
			);

			// Assert - not verified, but the inquiry completed so the transaction is still consumed
			result!.verified.Should().BeFalse();
			_fixture.MockPhilSysRepository.Verify(x => x.UpdateTransactionDataAsync(It.IsAny<PhilSysTransaction>()), Times.Once);
			_fixture.MockUnitOfWork.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
		}

		[Theory]
		[InlineData("pcn")]
		[InlineData("name_dob")]
		public async Task UpdateFaceLivenessSessionAsync_ShouldNotMarkTransacted_WhenExternalCallFails(string inquiryType)
		{
			// Arrange - the external PhilSys inquiry throws (non-2xx)
			var service = _fixture.UpdateFaceLivenessSessionService;
			SetupFullFlow(CreateTransaction(inquiryType), CreateResponse());
			_fixture.MockPhilSysService
				.Setup(x => x.PostPCNAsync(It.IsAny<string>(),
					It.IsAny<string>(),
					It.IsAny<string>()))
				.ThrowsAsync(new InternalServerException("PCN request failed. Please contact the administrator."));
			_fixture.MockPhilSysService
				.Setup(x => x.PostBasicInformationAsync(It.IsAny<string>(),
					It.IsAny<string>(),
					It.IsAny<string>(),
					It.IsAny<string>(),
					It.IsAny<string>(),
					It.IsAny<string>(),
					It.IsAny<string>()))
				.ThrowsAsync(new InternalServerException("Basic Information request failed. Please contact the administrator."));

			// Act
			Func<Task> act = async () => await service.UpdateFaceLivenessSessionAsync(
				HashToken,
				FaceLivenessSessionId
			);

			// Assert - nothing written: the transaction stays unmarked and retryable
			await act.Should().ThrowAsync<InternalServerException>();
			_fixture.MockUnitOfWork.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
			_fixture.MockPhilSysRepository.Verify(x => x.UpdateTransactionDataAsync(It.IsAny<PhilSysTransaction>()), Times.Never);
		}

		[Fact]
		public async Task UpdateFaceLivenessSessionAsync_ShouldReturnData_WhenSuccessful()
		{
			// Arrange
			var service = _fixture.UpdateFaceLivenessSessionService;
			SetupFullFlow(CreateTransaction("pcn"), CreateResponse());

			// Act
			var result = await service.UpdateFaceLivenessSessionAsync(
				HashToken,
				FaceLivenessSessionId
			);

			// Assert
			result!.verified.Should().NotBeNull();
			_fixture.MockPhilSysRepository.Verify(x => x.UpdateTransactionDataAsync(It.IsAny<PhilSysTransaction>()), Times.Once);
		}
	}
}
