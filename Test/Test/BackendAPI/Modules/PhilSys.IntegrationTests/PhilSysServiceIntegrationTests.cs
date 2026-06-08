using BuildingBlocks.Exceptions;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using PhilSys.DTO;
using PhilSys.Features.GetPhilSysToken;
using PhilSys.Features.PostBasicInformation;
using PhilSys.Features.PostPCN;
using System.Net;
using System.Text;
using System.Text.Json;
using Test.BackendAPI.Infrastructure.PhilSys.Infrastracture;

namespace Test.BackendAPI.Modules.PhilSys.IntegrationTests;

public class PhilSysServiceIntegrationTests : BaseIntegrationTest
{
	private readonly IntegrationTestWebAppFactory _factory;

	public PhilSysServiceIntegrationTests(IntegrationTestWebAppFactory factory) : base(factory)
	{
		_factory = factory;
	}

	[Fact]
	public async Task GetPhilsysTokenAsync_ShouldReturnToken_WhenResponseIsSuccessful()
	{
		// Arrange - override only the handler registered in the factory
		var expectedToken = "integration-fake-token";
		var customFactory = _factory.CreateFactoryWithHandler((req, ct) =>
		{
			var body = new { data = new { access_token = expectedToken } };
			var json = JsonSerializer.Serialize(body);
			return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent(json, Encoding.UTF8, "application/json")
			});
		});

		using var scope = customFactory.Services.CreateScope();
		var sender = scope.ServiceProvider.GetRequiredService<ISender>();

		var command = new GetPhilSysTokenCommand(Guid.CreateVersion7().ToString(), "secret");

		// Act
		var result = await sender.Send(command);

		// Assert
		result.Should().NotBeNull();
		result.AccessToken.Should().Be(expectedToken);
	}

	[Fact]
	public async Task GetPhilsysToken_ShouldThrow_WhenRequestFails()
	{
		// Arrange - configure handler to return non-success
		var customFactory = _factory.CreateFactoryWithHandler((req, ct) =>
		{
			return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
			{
				Content = new StringContent("{\"data\": null}", Encoding.UTF8, "application/json")
			});
		});

		using var scope = customFactory.Services.CreateScope();
		var sender = scope.ServiceProvider.GetRequiredService<ISender>();

		var command = new GetPhilSysTokenCommand(Guid.CreateVersion7().ToString(), "secret");

		// Act
		Func<Task> act = async () => await sender.Send(command);

		// Assert
		await act.Should().ThrowAsync<InternalServerException>().WithMessage("PhilSys token request failed.");
	}

	[Fact]
	public async Task PostBasicInformationAsync_ShouldReturnData_WhenResponseIsSuccessful()
	{
		// Arrange - return success body for query endpoint
		var expected = new BasicInformationOrPCNResponseDTO(
			code: "C123", token: "T123", reference: "R123", face_url: "https://example",
			full_name: "FULL", first_name: "FIRST", middle_name: "MIDDLE", last_name: "LAST",
			suffix: null, gender: "Male", marital_status: "Single", blood_type: "Unknown",
			email: "n/a", mobile_number: "0919", birth_date: "1990-01-01", full_address: "addr",
			address_line_1: "addr1", address_line_2: null, barangay: "b", municipality: "m",
			province: "p", country: "Philippines", postal_code: "1101", present_full_address: "paddr",
			present_address_line_1: "paddr1", present_address_line_2: null, present_barangay: "pb",
			present_municipality: "pm", present_province: "pp", present_country: "Philippines",
			present_postal_code: "1210", residency_status: "Filipino", place_of_birth: "POB",
			pob_municipality: "pm", pob_province: "pp", pob_country: "Philippines"
		);

		var customFactory = _factory.CreateFactoryWithHandler((req, ct) =>
		{
			// Always return success body for query endpoints in this test
			var responseBody = new { data = expected, meta = new { tier_level = "Tier II", result_grade = 1 }, error = (object?)null };
			var json = JsonSerializer.Serialize(responseBody);
			return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent(json, Encoding.UTF8, "application/json")
			});
		});

		using var scope = customFactory.Services.CreateScope();
		var sender = scope.ServiceProvider.GetRequiredService<ISender>();

		var command = new PostBasicInformationCommand(
			first_name: "first",
			middle_name: "mid",
			last_name: "last",
			suffix: null,
			birth_date: "1990-01-01",
			bearer_token: "bearer",
			face_liveness_session_id: "session");

		// Act
		var result = await sender.Send(command);

		// Assert
		result.Should().NotBeNull();
		result.BasicInformationResponseDTO.Should().BeEquivalentTo(expected);
	}

	[Fact]
	public async Task PostBasicInformationAsync_ShouldThrow_WhenRequestFails()
	{
		// Arrange - query endpoint returns BadRequest
		var customFactory = _factory.CreateFactoryWithHandler((req, ct) =>
		{
			return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
			{
				Content = new StringContent("{\"data\": null}", Encoding.UTF8, "application/json")
			});
		});

		using var scope = customFactory.Services.CreateScope();
		var sender = scope.ServiceProvider.GetRequiredService<ISender>();

		var command = new PostBasicInformationCommand(
			first_name: "first",
			middle_name: "mid",
			last_name: "last",
			suffix: null,
			birth_date: "1990-01-01",
			bearer_token: "bearer",
			face_liveness_session_id: "session");

		// Act
		Func<Task> act = async () => await sender.Send(command);

		// Assert
		await act.Should().ThrowAsync<InternalServerException>().WithMessage("Basic Information request failed. Please contact the administrator.");
	}

	[Fact]
	public async Task PostPCNAsync_ShouldReturnData_WhenResponseSucceeds()
	{
		// Arrange - success response for PCN
		var expected = new BasicInformationOrPCNResponseDTO(
			code: "PCNCODE", token: "PCNTOKEN", reference: "PCNREF", face_url: "https://example/pcn.png",
			full_name: "PCN FULL", first_name: "PCN FIRST", middle_name: null, last_name: "PCN LAST",
			suffix: null, gender: "Male", marital_status: "Single", blood_type: "Unknown",
			email: "n/a", mobile_number: "0919", birth_date: "1990-01-01", full_address: "addr",
			address_line_1: "addr1", address_line_2: null, barangay: "b", municipality: "m",
			province: "p", country: "Philippines", postal_code: "1101", present_full_address: "paddr",
			present_address_line_1: "paddr1", present_address_line_2: null, present_barangay: "pb",
			present_municipality: "pm", present_province: "pp", present_country: "Philippines",
			present_postal_code: "1210", residency_status: "Filipino", place_of_birth: "POB",
			pob_municipality: "pm", pob_province: "pp", pob_country: "Philippines"
		);

		var customFactory = _factory.CreateFactoryWithHandler((req, ct) =>
		{
			var responseBody = new { data = expected, meta = new { tier_level = "Tier II", result_grade = 1 }, error = (object?)null };
			var json = JsonSerializer.Serialize(responseBody);
			return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent(json, Encoding.UTF8, "application/json")
			});
		});

		using var scope = customFactory.Services.CreateScope();
		var sender = scope.ServiceProvider.GetRequiredService<ISender>();

		var command = new PostPCNCommand(
			value: "11111111111111111111",
			bearer_token: "bearer",
			face_liveness_session_id: "session");

		// Act
		var result = await sender.Send(command);

		// Assert
		result.Should().NotBeNull();
		result.PCNResponseDTO.Should().BeEquivalentTo(expected);
	}

	[Fact]
	public async Task PostPCNAsync_ShouldThrow_WhenRequestFails()
	{
		// Arrange - PCN endpoint returns BadRequest
		var customFactory = _factory.CreateFactoryWithHandler((req, ct) =>
		{
			return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
			{
				Content = new StringContent("{\"data\": null}", Encoding.UTF8, "application/json")
			});
		});

		using var scope = customFactory.Services.CreateScope();
		var sender = scope.ServiceProvider.GetRequiredService<ISender>();

		var command = new PostPCNCommand(
			value: "11111111111111111111",
			bearer_token: "bearer",
			face_liveness_session_id: "session");

		// Act
		Func<Task> act = async () => await sender.Send(command);

		// Assert
		await act.Should().ThrowAsync<InternalServerException>().WithMessage("PCN request failed. Please contact the administrator.");
	}
}