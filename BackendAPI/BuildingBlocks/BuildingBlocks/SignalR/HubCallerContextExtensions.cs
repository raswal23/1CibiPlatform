using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace BuildingBlocks.SignalR;

public static class HubCallerContextExtensions
{
	// The same pair CurrentUser reads, in the same order, so a hub group and an
	// ICurrentUser.UserId always agree on who the caller is.
	private const string UserIdClaim = "userId";

	/// <summary>
	/// The per-user SignalR group name for the authenticated caller, or null when the
	/// connection carries no usable identity.
	/// </summary>
	/// <remarks>
	/// Derived from the validated token only. Never fall back to a query-string value:
	/// the group decides who receives another user's notifications.
	/// </remarks>
	public static string? GetUserGroupName(this HubCallerContext context)
	{
		var value = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

		if (string.IsNullOrWhiteSpace(value))
			value = context.User?.FindFirst(UserIdClaim)?.Value;

		// Round-trip through Guid so the group name is canonically formatted regardless
		// of how the claim was written.
		return Guid.TryParse(value, out var userId) && userId != Guid.Empty
			? userId.ToString()
			: null;
	}
}
