using FluentAssertions;
using Test.BackendAPI.Modules.PhilSys.UnitTests.Fixture;

namespace Test.BackendAPI.Modules.PhilSys.UnitTests
{
	public class GetLivenessKeyServiceTests : IClassFixture<PhilSysServiceFixture>
	{
		private readonly PhilSysServiceFixture _fixture;

		public GetLivenessKeyServiceTests(PhilSysServiceFixture fixture)
		{
			_fixture = fixture;
		}

		[Fact]
		public async Task GetLivenessKey_ShouldReturnKey_WhenConfigured()
		{
			// Arrange
			var service = _fixture.GetLivenessKeyService;
			var expectedKey = _fixture.Configuration["PhilSys:LivenessSDKPublicKey"];

			// Act
			var result = await service.GetLivenessKey();

			// Assert
			result.Should().Be(expectedKey);
		}

		//[Fact]
		//public async Task GetLivenessKey_ShouldReturnEmpty_WhenKeyIsNotConfigured()
		//{

		//	// Arrange
		//	var service = _fixture.GetLivenessKeyService;
		//	_fixture.Configuration["PhilSys:LivenessSDKPublicKey"] = "";

		//	// Act
		//	var key = await service.GetLivenessKey();

		//	// Assert
		//	key.Should().BeEmpty();
		//}
	}
}
