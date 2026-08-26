namespace OMS.Shared.Implementations;

public sealed class OMSTicketCreator(
	IOMSRepository repository,
	ILogger<OMSTicketCreator> logger) : IOMSTicketCreator
{
	public async Task<OMSTicketCreated> CreateTicketAsync(
		CreateOMSTicketRequest request,
		CancellationToken cancellationToken,
		string referenceNumber = "")
	{
		request = request with
		{
			FirstName = NormalizeName(request.FirstName),
			MiddleName = string.IsNullOrWhiteSpace(request.MiddleName)
				? request.MiddleName
				: NormalizeName(request.MiddleName),
			LastName = NormalizeName(request.LastName)
		};

		var isRequestorValid = await repository.ValidateRequestorAsync(
			request.RequestorFirstName,
			request.RequestorLastName,
			request.Site,
			cancellationToken);

		if (!isRequestorValid)
		{
			throw new BadRequestException("Requestor is invalid");
		}

		var isPONumberValid = await repository.ValidatePONumberAsync(
			request.RequestorFirstName,
			request.RequestorLastName,
			request.Site,
			request.TurnAroundTimeID,
			request.ReportTypeID,
			cancellationToken);

		if (!isPONumberValid)
		{
			throw new BadRequestException("PO is insufficient or invalid, please contact your manager");
		}

		var ticket = await repository.CreateTicketAsync(
			request,
			referenceNumber,
			cancellationToken);

		if (ticket is null)
		{
			logger.LogError(
				"The OMS create ticket procedure returned no ticket row for report type {ReportTypeID}.",
				request.ReportTypeID);

			throw new InternalServerException("Ticket creation failed.");
		}

		return ticket;
	}

	private static string NormalizeName(string value) =>
		string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
