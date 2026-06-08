using BuildingBlocks.Exceptions;
using FluentAssertions;
using PhilSys.DTO;
using PhilSys.Services;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using Test.BackendAPI.Infrastructure.PhilSys.Infrastracture;
using Test.BackendAPI.Modules.PhilSys.UnitTests.Fixture;

namespace Test.BackendAPI.Modules.PhilSys.UnitTests
{
	public class PhilSysServiceTests : IClassFixture<PhilSysServiceFixture>
	{
		private readonly PhilSysServiceFixture _fixture;

		public PhilSysServiceTests(PhilSysServiceFixture fixture)
		{
			_fixture = fixture;
		}


		// Get Token PhilSys Tests
		[Fact]
		public async Task GetPhilsysTokenAsync_ShouldThrow_WhenRequestFails()
		{
			// Arrange
			var client_id = Guid.CreateVersion7().ToString();
			var client_secret = "YnQpGs34mdlH24234234EhRc0pJXAjQASDdASdjvihbujtuLxHt51";
			_fixture.MockHttpClientFactory.Setup(f => f.CreateClient("PhilSys")).Returns(() =>
			{
				var handlerStub = new PhilSysTestHandler((request, ct) =>
				{
					return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
					{
						Content = new StringContent("{\"data\": null}", Encoding.UTF8, "application/json")
					});
				});
				return new HttpClient(handlerStub)
				{
					BaseAddress = new Uri("https://ws.everify.gov.ph/api/auth")
				};
			});
			var service = new PhilSysService(
							_fixture.MockHttpClientFactory.Object,
							_fixture.MockPhilSysServiceLogger.Object
						);

			// Act
			Func<Task> act = async () => await service.GetPhilsysTokenAsync(client_id, client_secret);

			// Assert
			await act.Should().ThrowAsync<InternalServerException>().WithMessage("PhilSys token request failed.");
		}

		[Fact]
		public async Task GetPhilsysTokenAsync_ShouldReturnToken_WhenResponseIsSuccessful()
		{
			// Arrange
			var client_id = Guid.CreateVersion7().ToString();
			var client_secret = "YnQpGs34mdlH24234234EhRc0pJXAjQASDdASdjvihbujtuLxHt51";
			var expectedToken = "fake-token";
			_fixture.MockHttpClientFactory.Setup(f => f.CreateClient("PhilSys")).Returns(() =>
			{
				var handlerStub = new PhilSysTestHandler((request, ct) =>
				{
					return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
					{
						Content = JsonContent.Create(new { data = new { access_token = expectedToken } })
					});
				});
				return new HttpClient(handlerStub)
				{
					BaseAddress = new Uri("https://ws.everify.gov.ph/api/auth")
				};
			});
			var service = new PhilSysService(
								_fixture.MockHttpClientFactory.Object,
								_fixture.MockPhilSysServiceLogger.Object
							);

			// Act
			var token = await service.GetPhilsysTokenAsync(client_id, client_secret);

			// Assert
			token.Should().Be(expectedToken);
		}


		// Post Basic Information Test
		[Fact]
		public async Task PostBasicInformationAsync_ShouldThrow_WhenRequestFails()
		{
			// Arrange
			var first_name = "Juan";
			var middle_name = "Dela";
			var last_name = "Cruz";
			var suffix = "Jr.";
			var birth_date = "1990-01-01";
			var bearer_token = "valid-bearer-token";
			var face_liveness_session_id = "valid-session-id";
			_fixture.MockHttpClientFactory.Setup(f => f.CreateClient("PhilSys")).Returns(() =>
			{
				var handlerStub = new PhilSysTestHandler((request, ct) =>
				{
					return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
					{
						Content = new StringContent("{\"data\": null}", Encoding.UTF8, "application/json")
					});
				});
				return new HttpClient(handlerStub)
				{
					BaseAddress = new Uri("https://ws.everify.gov.ph/api/query")
				};
			});
			var service = new PhilSysService(
							_fixture.MockHttpClientFactory.Object,
							_fixture.MockPhilSysServiceLogger.Object
						);

			// Act
			Func<Task> act = async () => await service.PostBasicInformationAsync(
				first_name,
				middle_name,
				last_name,
				suffix,
				birth_date,
				bearer_token,
				face_liveness_session_id
			);

			// Assert
			await act.Should().ThrowAsync<InternalServerException>().WithMessage("Basic Information request failed. Please contact the administrator.");
		}

		[Fact]
		public async Task PostBasicInformationAsync_ShouldReturnData_WhenResponseIsSuccessful()
		{
			// Arrange
			var first_name = "Juan";
			var middle_name = "Dela";
			var last_name = "Cruz";
			var suffix = "Jr.";
			var birth_date = "1990-01-01";
			var bearer_token = "valid-bearer-token";
			var face_liveness_session_id = "valid-session-id";
			var expectedResponse = new BasicInformationOrPCNResponseDTO
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
			_fixture.MockHttpClientFactory.Setup(f => f.CreateClient("PhilSys")).Returns(() =>
			{
				var handlerStub = new PhilSysTestHandler((request, ct) =>
				{
					var responseBody = new
					{
						data = expectedResponse,
						meta = new { tier_level = "Tier II", result_grade = 1 },
						error = (object?)null
					};
					var jsonResponse = System.Text.Json.JsonSerializer.Serialize(responseBody);
					return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
					{
						Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
					});
				});
				return new HttpClient(handlerStub)
				{
					BaseAddress = new Uri("https://ws.everify.gov.ph/api/query")
				};
			});

			var service = new PhilSysService(
							_fixture.MockHttpClientFactory.Object,
							_fixture.MockPhilSysServiceLogger.Object
						);

			// Act
			var result = await service.PostBasicInformationAsync(
				first_name,
				middle_name,
				last_name,
				suffix,
				birth_date,
				bearer_token,
				face_liveness_session_id
			);
			// Assert
			result.Should().BeEquivalentTo(expectedResponse);
		}

		// Post PhilSys Card Number Test
		[Fact]
		public async Task PostPCNAsync_ShouldThrow_WhenRequestFails()
		{
			// Arrange
			var pcn = "11111111111111111111";
			var bearer_token = "valid-bearer-token";
			var face_liveness_session_id = "valid-session-id";
			_fixture.MockHttpClientFactory.Setup(f => f.CreateClient("PhilSys")).Returns(() =>
			{
				var handlerStub = new PhilSysTestHandler((request, ct) =>
				{
					return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
					{
						Content = new StringContent("{\"data\": null}", Encoding.UTF8, "application/json")
					});
				});
				return new HttpClient(handlerStub)
				{
					BaseAddress = new Uri("https://ws.everify.gov.ph/api/query/qr")
				};
			});
			var service = new PhilSysService(
							_fixture.MockHttpClientFactory.Object,
							_fixture.MockPhilSysServiceLogger.Object
						);

			// Act
			Func<Task> act = async () => await service.PostPCNAsync(
				pcn,
				bearer_token,
				face_liveness_session_id
				);

			// Assert
			await act.Should().ThrowAsync<InternalServerException>().WithMessage("PCN request failed. Please contact the administrator.");
		}

		[Fact]
		public async Task PostPCNAsync_ShouldReturnData_WhenRequestSucceeds()
		{
			// Arrange
			var pcn = "11111111111111111111";
			var bearer_token = "valid-bearer-token";
			var face_liveness_session_id = "valid-session-id";
			var expectedResponse = new BasicInformationOrPCNResponseDTO
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
			_fixture.MockHttpClientFactory.Setup(f => f.CreateClient("PhilSys")).Returns(() =>
			{
				var handlerStub = new PhilSysTestHandler((request, ct) =>
				{
					var responseBody = new
					{
						data = expectedResponse,
						meta = new { tier_level = "Tier II", result_grade = 1 },
						error = (object?)null
					};
					var jsonResponse = System.Text.Json.JsonSerializer.Serialize(responseBody);
					return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
					{
						Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
					});
				});
				return new HttpClient(handlerStub)
				{
					BaseAddress = new Uri("https://ws.everify.gov.ph/api/query")
				};
			});

			var service = new PhilSysService(
							_fixture.MockHttpClientFactory.Object,
							_fixture.MockPhilSysServiceLogger.Object
						);

			// Act
			var result = await service.PostPCNAsync(
				pcn,
				bearer_token,
				face_liveness_session_id
				);

			// Assert
			result.Should().BeEquivalentTo(expectedResponse);
		}
	}
}
