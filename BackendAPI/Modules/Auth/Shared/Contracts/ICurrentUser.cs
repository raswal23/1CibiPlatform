namespace Auth.Shared.Contracts;

public interface ICurrentUser
{
	bool IsAuthenticated { get; }
	Guid? UserId { get; }
	string? Email { get; }
	string? FullName { get; }
	IReadOnlySet<int> PlatformRoleIds { get; }
	bool IsPlatformSuperAdmin { get; }
	int? AtsClientId { get; }
	int? AtsRoleId { get; }
}
