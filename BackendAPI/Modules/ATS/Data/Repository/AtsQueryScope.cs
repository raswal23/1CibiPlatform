namespace ATS.Data.Repository;

public enum AtsQueryScopeKind
{
	Denied,
	All,
	Client,
	Requestor
}

public readonly record struct AtsQueryScope
{
	private AtsQueryScope(
		AtsQueryScopeKind kind,
		int? clientId = null,
		Guid? requestorId = null)
	{
		Kind = kind;
		ClientId = clientId;
		RequestorId = requestorId;
	}

	public AtsQueryScopeKind Kind { get; }
	public int? ClientId { get; }
	public Guid? RequestorId { get; }

	public static AtsQueryScope Denied => new(AtsQueryScopeKind.Denied);
	public static AtsQueryScope All => new(AtsQueryScopeKind.All);
	public static AtsQueryScope ForClient(int clientId) =>
		clientId > 0 ? new(AtsQueryScopeKind.Client, clientId) : Denied;
	public static AtsQueryScope ForRequestor(Guid requestorId) =>
		requestorId != Guid.Empty ? new(AtsQueryScopeKind.Requestor, requestorId: requestorId) : Denied;

	public string CacheKey => Kind switch
	{
		AtsQueryScopeKind.All => "all",
		AtsQueryScopeKind.Client => $"client_{ClientId}",
		AtsQueryScopeKind.Requestor => $"requestor_{RequestorId:N}",
		_ => "denied"
	};
}
