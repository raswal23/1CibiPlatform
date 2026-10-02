using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using PhilSys.Data.Entities;
using PhilSys.Data.Repository;
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

	private const string FaceSessionId = "valid-session-id";

	private PhilSysTransaction CreateTransaction(string inquiryType, string hashedToken) => new PhilSysTransaction
	{
		Tid = Guid.CreateVersion7(),
		InquiryType = inquiryType,
		FirstName = "Juan",
		MiddleName = "Bitaw",
		LastName = "Dela Cruz",
		Suffix = null,
		BirthDate = "2001-08-20",
		PCN = "6786785465456459",
		WebHookUrl = "/",
		IsTransacted = false,
		HashToken = hashedToken,
		ExpiresAt = DateTime.UtcNow.AddMinutes(10),
		CreatedAt = DateTime.UtcNow
	};

	private static BasicInformationOrPCNResponseDTO CreateBasicResponse(string reference = "11111111111111111111") => new BasicInformationOrPCNResponseDTO(
		code: "GASHJDG123",
		token: "111111111111111111111111111111",
		reference: reference,
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

	private Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> CreateSuccessFactory(BasicInformationOrPCNResponseDTO basicResponse)
		=> _factory.CreateFactoryWithHandler((req, ct) =>
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

	[Fact]
	public async Task UpdateFaceLivenessSession_ShouldReturnVerified_WhenInquiryIsNameDob_UsingCreateFactoryWithHandler()
	{
		// Arrange - seed transaction into a scope of the custom factory
		var plainToken = "valid-token-123";
		var hashed = _hashService.Hash(plainToken);
		var transaction = CreateTransaction("name_dob", hashed);

		var customFactory = CreateSuccessFactory(CreateBasicResponse());

		_dbContext.PhilSysTransactions.Add(transaction);
		await _dbContext.SaveChangesAsync();

		// Act - resolve the mediator from the custom factory and send command
		using var actionScope = customFactory.Services.CreateScope();
		var sender = actionScope.ServiceProvider.GetRequiredService<ISender>();

		var command = new UpdateFaceLivenessSessionCommand(transaction.HashToken!, FaceSessionId);
		var result = await sender.Send(command);

		// Assert
		result.Should().NotBeNull();
		result.VerificationResponseDTO.Should().NotBeNull();
		result.VerificationResponseDTO.verified.Should().BeTrue();
		result.VerificationResponseDTO.idv_session_id.Should().Be(transaction.Tid.ToString());

		await _dbContext.Entry(transaction).ReloadAsync();
		transaction.IsTransacted.Should().BeTrue();
		transaction.TransactedAt.Should().NotBeNull();
	}

	[Fact]
	public async Task UpdateFaceLivenessSession_ShouldMarkTransacted_WhenInquiryIsPcn()
	{
		// Arrange
		var plainToken = "valid-token-pcn";
		var hashed = _hashService.Hash(plainToken);
		var transaction = CreateTransaction("pcn", hashed);

		var customFactory = CreateSuccessFactory(CreateBasicResponse());

		_dbContext.PhilSysTransactions.Add(transaction);
		await _dbContext.SaveChangesAsync();

		// Act
		using var actionScope = customFactory.Services.CreateScope();
		var sender = actionScope.ServiceProvider.GetRequiredService<ISender>();

		var command = new UpdateFaceLivenessSessionCommand(transaction.HashToken!, FaceSessionId);
		var result = await sender.Send(command);

		// Assert
		result.VerificationResponseDTO.verified.Should().BeTrue();

		await _dbContext.Entry(transaction).ReloadAsync();
		transaction.IsTransacted.Should().BeTrue();
		transaction.TransactedAt.Should().NotBeNull();
	}

	[Fact]
	public async Task UpdateFaceLivenessSession_ShouldMarkTransacted_WhenNoDataFound()
	{
		// Arrange - PhilSys answers 200 but with an empty reference (no matching record)
		var plainToken = "valid-token-nodata";
		var hashed = _hashService.Hash(plainToken);
		var transaction = CreateTransaction("pcn", hashed);

		var customFactory = CreateSuccessFactory(CreateBasicResponse(reference: ""));

		_dbContext.PhilSysTransactions.Add(transaction);
		await _dbContext.SaveChangesAsync();

		// Act
		using var actionScope = customFactory.Services.CreateScope();
		var sender = actionScope.ServiceProvider.GetRequiredService<ISender>();

		var command = new UpdateFaceLivenessSessionCommand(transaction.HashToken!, FaceSessionId);
		var result = await sender.Send(command);

		// Assert - not verified, but the inquiry completed so the transaction is consumed
		result.VerificationResponseDTO.verified.Should().BeFalse();

		await _dbContext.Entry(transaction).ReloadAsync();
		transaction.IsTransacted.Should().BeTrue();
		transaction.TransactedAt.Should().NotBeNull();
	}

	[Fact]
	public async Task UpdateFaceLivenessSession_ShouldNotMarkTransacted_WhenPhilSysReturnsError()
	{
		// Arrange - auth succeeds, but the query endpoint returns an error
		var plainToken = "valid-token-error";
		var hashed = _hashService.Hash(plainToken);
		var transaction = CreateTransaction("pcn", hashed);

		var customFactory = _factory.CreateFactoryWithHandler((req, ct) =>
		{
			if (req.RequestUri != null && req.RequestUri.AbsolutePath.Contains("auth", StringComparison.OrdinalIgnoreCase))
			{
				var tokenBody = new { data = new { access_token = "integration-fake-token" } };
				var tokenJson = JsonSerializer.Serialize(tokenBody);
				return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
				{
					Content = new StringContent(tokenJson, Encoding.UTF8, "application/json")
				});
			}

			var errorJson = JsonSerializer.Serialize(new { error = "not_found", message = "Record not found", error_description = "The inquiry failed" });
			return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
			{
				Content = new StringContent(errorJson, Encoding.UTF8, "application/json")
			});
		});

		_dbContext.PhilSysTransactions.Add(transaction);
		await _dbContext.SaveChangesAsync();

		// Act
		using var actionScope = customFactory.Services.CreateScope();
		var sender = actionScope.ServiceProvider.GetRequiredService<ISender>();

		var command = new UpdateFaceLivenessSessionCommand(transaction.HashToken!, FaceSessionId);
		Func<Task> act = async () => await sender.Send(command);

		// Assert - the inquiry never completed, so the transaction stays unmarked and retryable
		await act.Should().ThrowAsync<Exception>();

		await _dbContext.Entry(transaction).ReloadAsync();
		transaction.IsTransacted.Should().BeFalse();
		transaction.TransactedAt.Should().BeNull();
	}

	[Fact]
	public async Task UpdateFaceLivenessSession_ShouldEvictCachedLivenessStatus_WhenTransacted()
	{
		// Arrange
		var plainToken = "valid-token-cache";
		var hashed = _hashService.Hash(plainToken);
		var transaction = CreateTransaction("pcn", hashed);

		var customFactory = CreateSuccessFactory(CreateBasicResponse());

		_dbContext.PhilSysTransactions.Add(transaction);
		await _dbContext.SaveChangesAsync();

		// Prime the liveness-status cache from the same factory (each factory has its own in-memory HybridCache)
		using (var primeScope = customFactory.Services.CreateScope())
		{
			var repository = primeScope.ServiceProvider.GetRequiredService<IPhilSysRepository>();
			var statusBefore = await repository.GetLivenessSessionStatusAsync(hashed);
			statusBefore.IsTransacted.Should().BeFalse();
		}

		// Act
		using (var actionScope = customFactory.Services.CreateScope())
		{
			var sender = actionScope.ServiceProvider.GetRequiredService<ISender>();
			await sender.Send(new UpdateFaceLivenessSessionCommand(transaction.HashToken!, FaceSessionId));
		}

		// Assert - the cached status was evicted, so the fresh value is returned instead of the stale one
		using (var assertScope = customFactory.Services.CreateScope())
		{
			var repository = assertScope.ServiceProvider.GetRequiredService<IPhilSysRepository>();
			var statusAfter = await repository.GetLivenessSessionStatusAsync(hashed);
			statusAfter.IsTransacted.Should().BeTrue();
		}
	}
}
