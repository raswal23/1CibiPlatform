using Test.BackendAPI.Modules.ATS.UnitTests.Fixture;

namespace Test.BackendAPI.Modules.ATS.UnitTests;

public class BulkSubmissionProcessorServiceTests : IClassFixture<ATSServiceFixture>
{
	private readonly ATSServiceFixture _fixture;
	public BulkSubmissionProcessorServiceTests(ATSServiceFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task ProcessAsync_ShouldReturn_WhenNoPendingFiles()
	{

	}

	[Fact]
	public async Task ProcessAsync_ShouldThrow_WhenGenerateTokenFails()
	{

	}

	[Fact]
	public async Task ProcessAsync_ShouldThrow_WhenHashTokenFails()
	{

	}

	[Fact]
	public async Task ProcessAsync_ShouldProcessFile_WhenSuccessful()
	{

	}

	[Fact]
	public async Task ProcessAsync_ShouldThrow_WhenDownloadFails()
	{

	}

}
