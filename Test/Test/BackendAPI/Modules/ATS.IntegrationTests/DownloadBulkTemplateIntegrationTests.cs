using ATS.Features.DownloadBulkTemplate;
using FluentAssertions;
using Test.BackendAPI.Infrastructure.ATS.Infrastracture;

namespace Test.BackendAPI.Modules.ATS.IntegrationTests;

public class DownloadBulkTemplateIntegrationTests : BaseIntegrationTest
{

	public DownloadBulkTemplateIntegrationTests(IntegrationTestWebAppFactory factory) : base(factory)
	{

	}

	[Fact]
	public async Task DownloadBulkTemplate_ShouldReturnTemplateLink()
	{
		// Arrange
		var command = new DownloadBulkTemplateHandlerRequest();

		// Act
		var result = await _sender.Send(command);

		// Assert
		result.Should().NotBeNull();
		result.templateLink.Should().NotBeNullOrWhiteSpace();
	}
}
