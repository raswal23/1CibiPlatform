using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using PhilSys.Data.Entities;
using PhilSys.DTO;
using PhilSys.Features.UpdateFaceLivenessSession;
using System.Net;
using System.Text;
using System.Text.Json;
using Test.BackendAPI.Infrastructure.PhilSys.Infrastracture;

namespace Test.BackendAPI.Modules.PhilSys.IntegrationTests;

public class UpdateFaceLivenessSessionIntegrationTests_CreateFactoryWithHandler : BaseIntegrationTest
{
	private readonly IntegrationTestWebAppFactory _factory;

	public UpdateFaceLivenessSessionIntegrationTests_CreateFactoryWithHandler(IntegrationTestWebAppFactory factory) : base(factory)
	{
		_factory = factory;
	}

	[Fact]
	public async Task UpdateFaceLivenessSession_ShouldReturnVerified_WhenInquiryIsNameDob_UsingCreateFactoryWithHandler()
	{
		// Arrange - seed transaction into a scope of the custom factory
		var plainToken = "valid-token-123";
		var hashed = _hashService.Hash(plainToken);
		var faceSessionId = "valid-session-id";

		var transaction = new PhilSysTransaction
		{
			Tid = Guid.NewGuid(),
			InquiryType = "name_dob",
			FirstName = "Juan",
			MiddleName = "Bitaw",
			LastName = "Dela Cruz",
			Suffix = null,
			BirthDate = "2001-08-20",
			WebHookUrl = "/",
			IsTransacted = false,
			HashToken = hashed,
			ExpiresAt = DateTime.UtcNow.AddMinutes(10),
			CreatedAt = DateTime.UtcNow
		};

		// Prepare expected BasicInformation response used by the handler
		var basicResponse = new BasicInformationOrPCNResponseDTO(
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

		// Create a custom factory that intercepts external HTTP calls and returns deterministic responses
		var customFactory = _factory.CreateFactoryWithHandler((req, ct) =>
		{
			// token endpoint
			if (req.RequestUri != null && req.RequestUri.AbsolutePath.Contains("auth", StringComparison.OrdinalIgnoreCase))
			{
				var tokenBody = new { data = new { access_token = "integration-fake-token" } };
				var tokenJson = JsonSerializer.Serialize(tokenBody);
				return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
				{
					Content = new StringContent(tokenJson, Encoding.UTF8, "application/json")
				});
			}

			// basic information / pcn endpoints
			var responseBody = new { data = basicResponse, meta = new { tier_level = "Tier II", result_grade = 1 }, error = (object?)null };
			var json = JsonSerializer.Serialize(responseBody);
			return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent(json, Encoding.UTF8, "application/json")
			});
		});
		
		_dbContext.PhilSysTransactions.Add(transaction);
		await _dbContext.SaveChangesAsync();

		// Act - resolve the mediator from the custom factory and send command
		using var actionScope = customFactory.Services.CreateScope();
		var sender = actionScope.ServiceProvider.GetRequiredService<ISender>();

		var command = new UpdateFaceLivenessSessionCommand(transaction.HashToken!, faceSessionId);
		var result = await sender.Send(command);

		// Assert
		result.Should().NotBeNull();
		result.VerificationResponseDTO.Should().NotBeNull();
		result.VerificationResponseDTO.verified.Should().BeTrue();
		result.VerificationResponseDTO.idv_session_id.Should().Be(transaction.Tid.ToString());
	}
}