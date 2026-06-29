using FluentAssertions;
using PhilSys.DTO;
using PhilSys.Features.PartnerSystemQuery;
using Test.BackendAPI.Infrastructure.PhilSys.Infrastracture;

namespace Test.BackendAPI.Modules.PhilSys.IntegrationTests;

public class PartnerSystemIntegrationTests : BaseIntegrationTest
{
	public PartnerSystemIntegrationTests(IntegrationTestWebAppFactory factory) : base(factory)
	{
	}

	[Fact]
	public async Task PartnerSystem_ShouldReturnLivenessLink_WhenSuccessful()
	{
		// Arrange
		var identity_data = new IdentityData(FirstName: "Juan", MiddleName: "Bitaw", LastName: "Dela Cruz", Suffix: string.Empty, BirthDate: "2001-08-20", PCN: string.Empty, ATSSession:string.Empty);
		var command = new PartnerSystemCommand("/", "name_dob", identity_data);

		// Act
		var result = await _sender.Send(command);

		// Assert
		result.Should().NotBeNull();
		result.PartnerSystemResponseDTO.liveness_link.Should().NotBeNullOrEmpty();
		result.PartnerSystemResponseDTO.idv_session_id.Should().NotBeNullOrEmpty();
	}
}
