using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Configuration;
using FluentAssertions;
using Auth.Services;
using Auth.DTO;
using System.Security.Claims;

namespace Test.BackendAPI.Modules.Auth.UnitTests;

public class JWTServiceTests
{
	private IConfiguration BuildConfiguration(string key = "mysupersecretkey-which-is-long-enough", string issuer = "test-issuer", string audience = "test-audience", string expiry = "60")
	{
		var inMemory = new Dictionary<string, string?>
		{
			["Jwt:Key"] = key,
			["Jwt:Issuer"] = issuer,
			["Jwt:Audience"] = audience,
			["Jwt:ExpiryInMinutes"] = expiry
		};

		return new ConfigurationBuilder().AddInMemoryCollection(inMemory).Build();
	}

	[Fact]
	public void GetAccessToken_ShouldReturnValidJwt_WithExpectedClaims()
	{
		// Arrange
		var cfg = BuildConfiguration();
		var service = new JWTService(cfg);
		var dto = new LoginDTO(Guid.CreateVersion7(), "hash", "user@example.com", "First", "Last", null, true, new List<int>(), new List<List<int>>(), new List<int>());

		// Act
		var token = service.GetAccessToken(dto);

		// Assert
		token.Should().NotBeNullOrWhiteSpace();

		var tokenHandler = new JwtSecurityTokenHandler();
		var validationParameters = new TokenValidationParameters
		{
			ValidateIssuerSigningKey = true,
			IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(cfg["Jwt:Key"]!)),
			ValidateIssuer = true,
			ValidIssuer = cfg["Jwt:Issuer"],
			ValidateAudience = true,
			ValidAudience = cfg["Jwt:Audience"],
			ValidateLifetime = true,
			ClockSkew = TimeSpan.Zero
		};

		SecurityToken validatedToken;
		var principal = tokenHandler.ValidateToken(token, validationParameters, out validatedToken);

		// userId: check common variants
		var userIdClaim = principal.FindFirst("userId") ?? principal.FindFirst(ClaimTypes.NameIdentifier) ?? principal.FindFirst(JwtRegisteredClaimNames.Sub);
		userIdClaim.Should().NotBeNull("JWT should contain user id claim (userId, sub or nameidentifier)");
		userIdClaim!.Value.Should().Be(dto.Id.ToString());

		// email: handle mapping to different claim types
		var emailClaim = principal.FindFirst("email") ?? principal.FindFirst(ClaimTypes.Email) ?? principal.FindFirst(JwtRegisteredClaimNames.Email);
		emailClaim.Should().NotBeNull("JWT should contain 'email' claim (email, emailaddress or registered email)");
		emailClaim!.Value.Should().Be(dto.Email);

		// fullName
		var fullNameClaim = principal.FindFirst("fullName") ?? principal.FindFirst(ClaimTypes.Name);
		fullNameClaim.Should().NotBeNull("JWT should contain 'fullName' or Name claim");
		fullNameClaim!.Value.Should().Contain(dto.FirstName).And.Contain(dto.LastName);
	}
}
