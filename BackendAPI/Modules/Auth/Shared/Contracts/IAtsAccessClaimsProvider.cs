namespace Auth.Shared.Contracts;

public sealed record AtsAccessClaims(int AtsRoleId, int? AtsClientId);

public interface IAtsAccessClaimsProvider
{
	Task<AtsAccessClaims?> GetClaimsAsync(
		Guid userId,
		CancellationToken cancellationToken = default);
}
