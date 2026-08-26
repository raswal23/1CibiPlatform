namespace OMS.Data.Repository;

public interface IOMSRepository
{
	Task<bool> ValidateRequestorAsync(
		string requestorFirstName,
		string requestorLastName,
		string site,
		CancellationToken cancellationToken);

	Task<bool> ValidatePONumberAsync(
		string requestorFirstName,
		string requestorLastName,
		string site,
		int turnAroundTimeId,
		int reportTypeId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Runs [dbo].[create_ticket_api_oms]. Returns null when the stored
	/// procedure produced no ticket row.
	/// </summary>
	Task<OMSTicketCreated?> CreateTicketAsync(
		CreateOMSTicketRequest request,
		string referenceNumber,
		CancellationToken cancellationToken);
}
