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
	/// <param name="referenceNumber">
	/// Caller-owned key stored against the OMS ticket, letting an automated
	/// caller tie the ticket back to its originating record and recognise a
	/// ticket a retry already created. Empty for interactive callers.
	/// </param>
	Task<OMSTicketCreated> CreateTicketAsync(
		CreateOMSTicketRequest request,
		CancellationToken cancellationToken,
		string referenceNumber = "");
}
