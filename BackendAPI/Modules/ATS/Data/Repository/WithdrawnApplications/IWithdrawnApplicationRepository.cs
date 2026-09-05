namespace ATS.Data.Repository;

public interface IWithdrawnApplicationRepository
{
	Task<List<EmailInvitationRequestListDTO>> GetWithdrawnPageAsync(
		string? searchTerm,
		DateTime? afterCreatedAt,
		Guid? afterId,
		int take,
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredRequestorId,
		CancellationToken cancellationToken);
	Task<long> CountWithdrawnAsync(
		string? searchTerm,
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredRequestorId,
		CancellationToken cancellationToken);
}
