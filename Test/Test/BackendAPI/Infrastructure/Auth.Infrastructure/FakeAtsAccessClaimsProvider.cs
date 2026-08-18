using Auth.Shared.Contracts;

namespace Test.BackendAPI.Infrastructure.Auth.Infrastructure;

// Auth integration tests only spin up the Auth test database. The real
// AtsAccessClaimsProvider queries the ATS DbContext, whose connection string
// points at infrastructure that does not exist in the test environment, so
// logins return no ATS claims here.
public class FakeAtsAccessClaimsProvider : IAtsAccessClaimsProvider
{
	public Task<AtsAccessClaims?> GetClaimsAsync(
		Guid userId,
		CancellationToken cancellationToken = default)
	{
		return Task.FromResult<AtsAccessClaims?>(null);
	}
}
