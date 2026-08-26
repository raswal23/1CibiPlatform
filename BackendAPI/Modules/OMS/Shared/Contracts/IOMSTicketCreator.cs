namespace OMS.Shared.Contracts;

public sealed record CreateOMSTicketRequest(
	string FirstName,
	string? MiddleName,
	string LastName,
	DateTime? DateOfBirth,
	string EmailAddress,
	string PhoneNumber,
	string? SSSIDNumber,
	string? TIN,
	string? Remarks,
	string RequestorFirstName,
	string RequestorLastName,
	string RequestorEmailAddress,
	string Site,
	int TurnAroundTimeID,
	int ReportTypeID,
	int CountryID,
	int ProvinceID,
	int CityID,
	string? Address,
	string? PostalCode);

public sealed record OMSTicketCreated(
	string TicketNumber,
	DateTime DeliveryDate);

public interface IOMSTicketCreator
{
	/// <summary>
	/// Validates the requestor and PO entitlement against the legacy OMS
	/// database, then creates the ticket via stored procedure. The ticket
	/// number, delivery date and initial status are produced by the database.
	/// </summary>
	Task<OMSTicketCreated> CreateTicketAsync(
		CreateOMSTicketRequest request,
		CancellationToken cancellationToken);
}
