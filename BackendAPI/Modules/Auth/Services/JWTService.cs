namespace Auth.Services;

using System.Globalization;

public class JWTService : IJWTService
{
	private readonly IConfiguration _configuration;

	public JWTService(IConfiguration configuration)
	{
		_configuration = configuration;
	}

	public string GetAccessToken(LoginDTO loginDTO)
	{
		var jwtSettings = _configuration.GetSection("Jwt");
		var key = jwtSettings["Key"];
		var issuer = jwtSettings["Issuer"];
		var audience = jwtSettings["Audience"];
		var expiryInMinutes = int.Parse(jwtSettings["ExpiryInMinutes"]!);

		var tokenHandler = new JwtSecurityTokenHandler();
		var symKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key!));

		var tokenDescriptor = new SecurityTokenDescriptor
		{
			Subject = new ClaimsIdentity(GetClaims(loginDTO)),
			Expires = DateTime.UtcNow.AddMinutes(expiryInMinutes),
			Issuer = issuer,
			Audience = audience,
			SigningCredentials = new SigningCredentials(symKey, SecurityAlgorithms.HmacSha256Signature)
		};

		var token = tokenHandler.CreateToken(tokenDescriptor);

		return tokenHandler.WriteToken(token);
	}

	private IEnumerable<Claim> GetClaims(LoginDTO loginDTO)
	{
		// build a friendly full name and avoid null middle name
		var middle = string.IsNullOrWhiteSpace(loginDTO.MiddleName) ? string.Empty : loginDTO.MiddleName.Trim();
		var fullName = string.Join(' ', new[] { loginDTO.FirstName, middle, loginDTO.LastName }.Where(s => !string.IsNullOrWhiteSpace(s)));

		var claims = new List<Claim>
		{
			// custom claims used by the app/tests
			new Claim(AuthClaimTypes.UserId, loginDTO.Id.ToString()),
			new Claim(AuthClaimTypes.Email, loginDTO.Email),
			new Claim(AuthClaimTypes.FullName, fullName),

			// standard claims for interoperability
			new Claim(ClaimTypes.NameIdentifier, loginDTO.Id.ToString()),
		};

		claims.AddRange(loginDTO.roleId
			.Where(roleId => roleId > 0)
			.Distinct()
			.Select(roleId => new Claim(
				AuthClaimTypes.PlatformRoleId,
				roleId.ToString(CultureInfo.InvariantCulture))));

		if (loginDTO.AtsRoleId is > 0)
			claims.Add(new Claim(AuthClaimTypes.AtsRoleId, loginDTO.AtsRoleId.Value.ToString(CultureInfo.InvariantCulture)));

		if (loginDTO.AtsClientId is > 0)
			claims.Add(new Claim(AuthClaimTypes.AtsClientId, loginDTO.AtsClientId.Value.ToString(CultureInfo.InvariantCulture)));

		return claims;
	}
}
