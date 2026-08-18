using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Configuration;
using FluentAssertions;
using Auth.Services;
using Auth.DTO;
using System.Security.Claims;
using Auth.Constants;

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

	[Fact]
	public void GetAccessToken_ShouldIncludeAtsClaims_WhenAssignmentIsPresent()
	{
		var cfg = BuildConfiguration();
		var service = new JWTService(cfg);
		var dto = new LoginDTO(
			Guid.CreateVersion7(),
			"hash",
			"user@example.com",
			"First",
			"Last",
			null,
			true,
			[],
			[],
			[1, 2, 1],
			AtsClientId: 42,
			AtsRoleId: 2);

		var token = service.GetAccessToken(dto);
		var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

		jwt.Claims.Single(claim => claim.Type == AuthClaimTypes.AtsRoleId).Value.Should().Be("2");
		jwt.Claims.Single(claim => claim.Type == AuthClaimTypes.AtsClientId).Value.Should().Be("42");
		jwt.Claims
			.Where(claim => claim.Type == AuthClaimTypes.PlatformRoleId)
			.Select(claim => claim.Value)
			.Should().Equal("1", "2");
	}

	[Fact]
	public void GetAccessToken_ShouldIncludeUniqueJtiAndProvidedSessionId()
	{
		var service = new JWTService(BuildConfiguration());
		var dto = new LoginDTO(Guid.CreateVersion7(), "hash", "user@example.com", "First", "Last", null, true, [], [], []);

		var firstToken = new JwtSecurityTokenHandler().ReadJwtToken(service.GetAccessToken(dto, 42));
		var secondToken = new JwtSecurityTokenHandler().ReadJwtToken(service.GetAccessToken(dto, 42));

		firstToken.Claims.Single(claim => claim.Type == JwtRegisteredClaimNames.Sid).Value.Should().Be("42");
		firstToken.Claims.Single(claim => claim.Type == JwtRegisteredClaimNames.Jti).Value
			.Should().NotBe(secondToken.Claims.Single(claim => claim.Type == JwtRegisteredClaimNames.Jti).Value);
	}
}
