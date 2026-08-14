namespace ATS.Shared.Implementations;

internal sealed class AtsAccessClaimsProvider : IAtsAccessClaimsProvider
{
	private readonly ATSDBContext _dbContext;
	private readonly ILogger<AtsAccessClaimsProvider> _logger;

	public AtsAccessClaimsProvider(
		ATSDBContext dbContext,
		ILogger<AtsAccessClaimsProvider> logger)
	{
		_dbContext = dbContext;
		_logger = logger;
	}

	public async Task<AtsAccessClaims?> GetClaimsAsync(
		Guid userId,
		CancellationToken cancellationToken = default)
	{
		var accessRows = await _dbContext.UserDetails
			.AsNoTracking()
			.Where(user => user.UserId == userId && user.IsActive && user.Role.IsActive)
			.Select(user => new { user.RoleId, user.ClientId })
			.Distinct()
			.ToListAsync(cancellationToken);

		if (accessRows.Count == 0)
			return null;

		var roleIds = accessRows.Select(row => row.RoleId).Distinct().ToArray();
		if (roleIds.Length != 1)
		{
			_logger.LogWarning(
				"ATS claims were omitted because user {UserId} has inconsistent roles",
				userId);
			return null;
		}

		var assignment = await _dbContext.UserClientDetails
			.AsNoTracking()
			.SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
		var clientId = assignment?.ClientId;
		var rowClientIds = accessRows.Select(row => row.ClientId).Distinct().ToArray();
		if (clientId.HasValue && !await _dbContext.ClientDetails
			.AsNoTracking()
			.AnyAsync(client => client.ClientId == clientId.Value && client.IsActive, cancellationToken))
		{
			_logger.LogWarning(
				"ATS client claim was omitted because user {UserId} is assigned to an inactive client",
				userId);
			clientId = null;
		}

		if (clientId.HasValue &&
			(rowClientIds.Length != 1 || rowClientIds[0] != clientId))
		{
			_logger.LogWarning(
				"ATS client claim was omitted because user {UserId} has inconsistent client assignments",
				userId);
			clientId = null;
		}

		return new AtsAccessClaims(roleIds[0], clientId);
	}
}
