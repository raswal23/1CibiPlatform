using Test.BackendAPI.Modules.ATS.UnitTests.Fixture;

namespace Test.BackendAPI.Modules.ATS.UnitTests;

public class EmailNotificationProcessorServiceTests :IClassFixture<ATSServiceFixture>
{
	private readonly ATSServiceFixture _fixture;

	public EmailNotificationProcessorServiceTests(ATSServiceFixture fixture)
	{
		_fixture = fixture;
	}
}
