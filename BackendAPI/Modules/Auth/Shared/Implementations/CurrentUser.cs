using Auth.Constants;
using System.Globalization;

namespace Auth.Shared.Implementations;

internal sealed class CurrentUser : ICurrentUser
{
	private readonly IHttpContextAccessor _httpContextAccessor;

	public CurrentUser(IHttpContextAccessor httpContextAccessor)
	{
		_httpContextAccessor = httpContextAccessor;
	}

	private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

	public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

	public Guid? UserId => ParseGuid(
		GetClaimValue(ClaimTypes.NameIdentifier, AuthClaimTypes.UserId));

	public string? Email => GetClaimValue(ClaimTypes.Email, AuthClaimTypes.Email);

	public string? FullName => GetClaimValue(ClaimTypes.Name, AuthClaimTypes.FullName);

	public IReadOnlySet<int> PlatformRoleIds => Principal?
		.FindAll(AuthClaimTypes.PlatformRoleId)
		.Select(claim => ParsePositiveInt(claim.Value))
		.Where(roleId => roleId.HasValue)
		.Select(roleId => roleId!.Value)
		.ToHashSet() ?? new HashSet<int>();

	public bool IsPlatformSuperAdmin => PlatformRoleIds.Contains(Auth.Constants.PlatformRoleIds.SuperAdmin);

	public int? AtsClientId => ParsePositiveInt(GetClaimValue(AuthClaimTypes.AtsClientId));

	public int? AtsRoleId => ParsePositiveInt(GetClaimValue(AuthClaimTypes.AtsRoleId));

	private string? GetClaimValue(params string[] claimTypes)
	{
		foreach (var claimType in claimTypes)
		{
			var value = Principal?.FindFirst(claimType)?.Value;
			if (!string.IsNullOrWhiteSpace(value))
				return value;
		}

		return null;
	}

	private static Guid? ParseGuid(string? value) =>
		Guid.TryParse(value, out var parsed) ? parsed : null;

	private static int? ParsePositiveInt(string? value) =>
		int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) && parsed > 0
			? parsed
			: null;
}
