namespace ATS.Data.Repository;

public enum AtsQueryScopeKind
{
	Denied,
	All,
	Client,
	Clients,
	ClientRequestor,
	Requestor
}

public readonly record struct AtsQueryScope
{
	private AtsQueryScope(
		AtsQueryScopeKind kind,
		int? clientId = null,
		IReadOnlyList<int>? clientIds = null,
		Guid? requestorId = null)
	{
		Kind = kind;
		ClientId = clientId;
		ClientIds = clientIds ?? [];
		RequestorId = requestorId;
	}

	public AtsQueryScopeKind Kind { get; }
	public int? ClientId { get; }
	public IReadOnlyList<int> ClientIds { get; }
	public Guid? RequestorId { get; }

	public static AtsQueryScope Denied => new(AtsQueryScopeKind.Denied);
	public static AtsQueryScope All => new(AtsQueryScopeKind.All);
	public static AtsQueryScope ForClient(int clientId) =>
		clientId > 0 ? new(AtsQueryScopeKind.Client, clientId) : Denied;
	public static AtsQueryScope ForClients(IEnumerable<int> clientIds)
	{
		var authorizedClientIds = clientIds
			.Where(clientId => clientId > 0)
			.Distinct()
			.OrderBy(clientId => clientId)
			.ToArray();

		return authorizedClientIds.Length switch
		{
			0 => Denied,
			1 => ForClient(authorizedClientIds[0]),
			_ => new AtsQueryScope(AtsQueryScopeKind.Clients, clientIds: authorizedClientIds)
		};
	}
	public static AtsQueryScope ForRequestor(Guid requestorId) =>
		requestorId != Guid.Empty ? new(AtsQueryScopeKind.Requestor, requestorId: requestorId) : Denied;
	public static AtsQueryScope ForClientAndRequestor(int clientId, Guid requestorId) =>
		clientId > 0 && requestorId != Guid.Empty
			? new(AtsQueryScopeKind.ClientRequestor, clientId, requestorId: requestorId)
			: Denied;

	public string CacheKey => Kind switch
	{
		AtsQueryScopeKind.All => "all",
		AtsQueryScopeKind.Client => $"client_{ClientId}",
		AtsQueryScopeKind.Clients => $"clients_{string.Join('_', ClientIds)}",
		AtsQueryScopeKind.ClientRequestor => $"client_{ClientId}_requestor_{RequestorId:N}",
		AtsQueryScopeKind.Requestor => $"requestor_{RequestorId:N}",
		_ => "denied"
	};
}
