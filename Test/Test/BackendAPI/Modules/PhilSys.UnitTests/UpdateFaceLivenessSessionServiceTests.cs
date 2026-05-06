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

		public UpdateFaceLivenessSessionServiceTests(PhilSysServiceFixture fixture)
		{
			_fixture = fixture;
		}

		[Fact]
		public async Task UpdateFaceLivenessSessionAsync_ShouldThrow_WhenFailedToUpdateTransactionStatus()
		{
			// Arrange
			var service = _fixture.UpdateFaceLivenessSessionService;
			var hash_token = "hash-token";
			var face_liveness_session_id = "valid-session-id";
			byte[] photo = {137, 80, 78, 71, 13, 10, 26, 10};
			_fixture.MockPhilSysRepository.Setup(x => x.GetTransactionDataByHashTokenAsync(
				It.IsAny<string>()
			)).ReturnsAsync((PhilSysTransaction?)null);
		
			// Act
			Func<Task> act = async () => await service.UpdateFaceLivenessSessionAsync(
				hash_token,
				face_liveness_session_id,
				photo
			);

			// Assert
			await act.Should().ThrowAsync<InternalServerException>().WithMessage("No transaction record found for your Token. Face Liveness Session update aborted.");
		}

		[Fact]
		public async Task UpdateFaceLivenessSessionAsync_ShouldThrow_WhenFailedToUpdateTransactionData()
		{
			// Arrange
			var service = _fixture.UpdateFaceLivenessSessionService;
			var hash_token = "hash-token";
			var face_liveness_session_id = "valid-session-id";
			byte[] photo = {137, 80, 78, 71, 13, 10, 26, 10};
			var philsysTransaction = new PhilSysTransaction
			{
				Tid = Guid.NewGuid(),
				InquiryType = "pcn",
				PCN = "6786785465456459",
				HashToken = "hash-token",
				WebHookUrl = "/",
				CreatedAt = DateTime.UtcNow,
				ExpiresAt = DateTime.UtcNow.AddMinutes(5)
			};
			_fixture.MockPhilSysRepository.Setup(x => x.GetTransactionDataByHashTokenAsync(
				It.IsAny<string>()
			)).ReturnsAsync(philsysTransaction);
			_fixture.MockPhilSysRepository.Setup(x => x.UpdateTransactionDataAsync(
				philsysTransaction
			)).ReturnsAsync((PhilSysTransaction?)null);

			// Act
			Func<Task> act = async () => await service.UpdateFaceLivenessSessionAsync(
				hash_token,
				face_liveness_session_id,
				photo
			);

			// Assert
			await act.Should().ThrowAsync<InternalServerException>().WithMessage("No transaction record found for your Token. Face Liveness Session update aborted.");
		}

		[Fact]
		public async Task UpdateFaceLivenessSessionAsync_ShouldThrow_WhenUpdateFaceLivenessSessionFails()
		{
			// Arrange
			var service = _fixture.UpdateFaceLivenessSessionService;
			var hash_token = "hash-token";
			var face_liveness_session_id = "valid-session-id";
			byte[] photo = { 137, 80, 78, 71, 13, 10, 26, 10 };
			_fixture.MockPhilSysRepository.Setup(x => x.UpdateFaceLivenessSessionAsync(
				hash_token,
				face_liveness_session_id,
				photo
			)).ReturnsAsync((PhilSysTransaction?)null); 

			// Act
			Func<Task> act = async () => await service.UpdateFaceLivenessSessionAsync(
				hash_token,
				face_liveness_session_id,
				photo
			);
			// Assert
			await act.Should().ThrowAsync<InternalServerException>().WithMessage("No transaction record found for your Token. Face Liveness Session update aborted.");
		}

		[Fact]
		public async Task UpdateFaceLivenessSessionAsync_ShouldReturnData_WhenSuccessful()
		{
			// Arrange
			var service = _fixture.UpdateFaceLivenessSessionService;
			var hash_token = "hash-token";
			var face_liveness_session_id = "valid-session-id";
			byte[] photo = { 137, 80, 78, 71, 13, 10, 26, 10 };
			var philsysTransaction = new PhilSysTransaction
			{
				Tid = Guid.NewGuid(),
				InquiryType = "pcn",
				PCN = "6786785465456459",
				HashToken = "hash-token",
				WebHookUrl = "/",
				CreatedAt = DateTime.UtcNow,
				ExpiresAt = DateTime.UtcNow.AddMinutes(5)
			};
			var BasicInfoOrResponse = new BasicInformationOrPCNResponseDTO
			(
				code: "GASHJDG123",
				token: "111111111111111111111111111111",
				reference: "11111111111111111111",
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
			_fixture.MockPhilSysRepository.Setup(x => x.GetTransactionDataByHashTokenAsync(
				hash_token
			)).ReturnsAsync(philsysTransaction);
			_fixture.MockPhilSysRepository.Setup(x => x.UpdateTransactionDataAsync(
				It.IsAny<PhilSysTransaction>()
			)).ReturnsAsync(philsysTransaction);
			_fixture.MockPhilSysRepository
			.Setup(x => x.UpdateFaceLivenessSessionAsync(hash_token, face_liveness_session_id, photo))
			.ReturnsAsync(philsysTransaction);
			_fixture.MockPhilSysService
			.Setup(x => x.GetPhilsysTokenAsync(It.IsAny<string>(), It.IsAny<string>()))
			.ReturnsAsync("fake-access-token");
			_fixture.MockPhilSysService.Setup(x => x.PostBasicInformationAsync(It.IsAny<string>(), 
				It.IsAny<string>(), 
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<string>()
				))
			.ReturnsAsync(BasicInfoOrResponse);
			_fixture.MockPhilSysService.Setup(x => x.PostPCNAsync(It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<string>()
				))
			.ReturnsAsync(BasicInfoOrResponse);
			_fixture.MockPhilSysResultRepository.Setup(x => x.AddTransactionResultDataAsync(It.IsAny<PhilSysTransactionResult>())).ReturnsAsync(true);

			// Act
			var result = await service.UpdateFaceLivenessSessionAsync(
				hash_token,
				face_liveness_session_id,
				photo
			);

			// Assert
			result!.verified.Should().NotBeNull();
		}
	}
}
