using FluentAssertions;
using Moq;
using PhilSys.Data.Entities;
using PhilSys.DTO;
using Test.BackendAPI.Modules.PhilSys.UnitTests.Fixture;

namespace Test.BackendAPI.Modules.PhilSys.UnitTests
{
	public class PartnerSystemServiceTest : IClassFixture<PhilSysServiceFixture>
	{
		private readonly PhilSysServiceFixture _fixture;

		public PartnerSystemServiceTest(PhilSysServiceFixture fixture)
		{
			_fixture = fixture;
		}

		[Fact]
		public async Task PartnerSystemService_ShouldThrow_WhenFailedToGenerateToken()
		{
			// Assert
			var service = _fixture.PartnerSystemService;
			var identity_data = new IdentityData ( FirstName: "Juan", MiddleName: "Bitaw", LastName: "Dela Cruz", Suffix: string.Empty, BirthDate: "2001-08-20", PCN: string.Empty, ATSSession: string.Empty );
			_fixture.MockSecureToken.Setup(x => x.GenerateSecureToken()).Throws(new Exception("Failed to generate Token."));

			// Act
			Func<Task> act = async () => await service.PartnerSystemQueryAsync("/", "pcn", identity_data);

			//Assert 
			await act.Should().ThrowAsync<Exception>().WithMessage("Failed to generate Token.");
		}

		[Fact]
		public async Task PartnerSystemService_ShouldThrow_WhenFailedToHashedToken()
		{
			// Assert
			var service = _fixture.PartnerSystemService;
			var identity_data = new IdentityData(FirstName: "Juan", MiddleName: "Bitaw", LastName: "Dela Cruz", Suffix: string.Empty, BirthDate: "2001-08-20", PCN: string.Empty, ATSSession: string.Empty);
			_fixture.MockSecureToken.Setup(x => x.GenerateSecureToken()).Returns("token");
			_fixture.MockHashService.Setup(x => x.Hash(It.IsAny<string>())).Throws(new Exception("Failed to hash Token."));

			// Act
			Func<Task> act = async () => await service.PartnerSystemQueryAsync("/", "pcn", identity_data);

			//Assert 
			await act.Should().ThrowAsync<Exception>().WithMessage("Failed to hash Token.");
		}

		[Fact]
		public async Task PartnerSystemService_ShouldReturnLivenessLink_WhenSuccessful()
		{
			// Assert
			var service = _fixture.PartnerSystemService;
			var identity_data = new IdentityData(FirstName: "Juan", MiddleName: "Bitaw", LastName: "Dela Cruz", Suffix: string.Empty, BirthDate: "2001-08-20", PCN: string.Empty, ATSSession: string.Empty);
			var Tid = "c2f95e6a-ff97-4ce1-9f33-73c13451def5";
			var philsysTransaction = new PhilSysTransaction
			{
				Tid = Guid.Parse(Tid),
				InquiryType = "pcn",
				PCN = "6786785465456459",
				HashToken = "hash-token",
				WebHookUrl = "/",
				CreatedAt = DateTime.UtcNow,
				ExpiresAt = DateTime.UtcNow.AddMinutes(5)
			};
			var partnerSystemResponse = new PartnerSystemResponseDTO(idv_session_id: Tid, liveness_link: $"http://localhost:5134/philsys/idv/liveness/{philsysTransaction.HashToken}");
			_fixture.MockSecureToken.Setup(x => x.GenerateSecureToken()).Returns("token");
			_fixture.MockHashService.Setup(x => x.Hash(It.IsAny<string>())).Returns("hash-token");
			_fixture.MockPhilSysRepository.Setup(x => x.AddTransactionDataAsync(philsysTransaction)).ReturnsAsync(true);

			// Act
			var result = await service.PartnerSystemQueryAsync("/", "pcn", identity_data);

			//Assert 
			result.Should().NotBeNull();
		}
	}
}
