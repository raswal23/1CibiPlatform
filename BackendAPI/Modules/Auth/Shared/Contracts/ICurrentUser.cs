namespace Auth.Shared.Contracts;

public interface ICurrentUser
{
	bool IsAuthenticated { get; }
	Guid? UserId { get; }
	string? Email { get; }
	string? FullName { get; }

	/// <summary>
	/// Name parts as stored on the account, for callers that need them separately
	/// rather than as the joined <see cref="FullName"/>. Null on tokens issued
	/// before these claims existed.
	/// </summary>
	string? FirstName { get; }
	string? MiddleName { get; }
	string? LastName { get; }
	IReadOnlySet<int> PlatformRoleIds { get; }
	bool IsPlatformSuperAdmin { get; }
	int? AtsClientId { get; }
	int? AtsRoleId { get; }
}
