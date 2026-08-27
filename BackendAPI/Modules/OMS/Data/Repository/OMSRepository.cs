namespace OMS.Data.Repository;

public sealed class OMSRepository(
	IOMSSqlConnectionFactory connectionFactory,
	ILogger<OMSRepository> logger) : IOMSRepository
{
	private const string ValidateRequestorProcedure = "[dbo].[validate_requestor_api]";
	private const string ValidatePONumberProcedure = "[dbo].[validate_ponumber_api]";
	private const string CreateTicketProcedure = "[dbo].[create_ticket_api_oms]";

	public async Task<bool> ValidateRequestorAsync(
		string requestorFirstName,
		string requestorLastName,
		string site,
		CancellationToken cancellationToken)
	{
		try
		{
			await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
			await using var command = new SqlCommand(ValidateRequestorProcedure, connection)
			{
				CommandType = CommandType.StoredProcedure
			};

			command.Parameters.Add("@p_requestor_firstname", SqlDbType.NVarChar).Value = requestorFirstName;
			command.Parameters.Add("@p_requestor_lastname", SqlDbType.NVarChar).Value = requestorLastName;
			command.Parameters.Add("@p_site", SqlDbType.NVarChar).Value = site;

			return await ReadIsValidAsync(command, cancellationToken);
		}
		catch (SqlException ex)
		{
			logger.LogError(ex, "Requestor validation failed against the OMS database.");

			throw new InternalServerException("An error occurred while contacting the OMS database.");
		}
	}

	public async Task<bool> ValidatePONumberAsync(
		string requestorFirstName,
		string requestorLastName,
		string site,
		int turnAroundTimeId,
		int reportTypeId,
		CancellationToken cancellationToken)
	{
		try
		{
			await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
			await using var command = new SqlCommand(ValidatePONumberProcedure, connection)
			{
				CommandType = CommandType.StoredProcedure
			};

			command.Parameters.Add("@p_requestor_firstname", SqlDbType.NVarChar).Value = requestorFirstName;
			command.Parameters.Add("@p_requestor_lastname", SqlDbType.NVarChar).Value = requestorLastName;
			command.Parameters.Add("@p_site", SqlDbType.NVarChar).Value = site;
			command.Parameters.Add("@p_tat_id", SqlDbType.Int).Value = turnAroundTimeId;
			command.Parameters.Add("@p_report_type_id", SqlDbType.Int).Value = reportTypeId;

			return await ReadIsValidAsync(command, cancellationToken);
		}
		catch (SqlException ex)
		{
			logger.LogError(ex, "PO number validation failed against the OMS database.");

			throw new InternalServerException("An error occurred while contacting the OMS database.");
		}
	}

	public async Task<OMSTicketCreated?> CreateTicketAsync(
		CreateOMSTicketRequest request,
		string referenceNumber,
		CancellationToken cancellationToken)
	{
		try
		{
			await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
			await using var command = new SqlCommand(CreateTicketProcedure, connection)
			{
				CommandType = CommandType.StoredProcedure
			};

			command.Parameters.Add("@p_first_name", SqlDbType.NVarChar).Value = request.FirstName;
			command.Parameters.Add("@p_middle_name", SqlDbType.NVarChar).Value = (object?)request.MiddleName ?? string.Empty;
			command.Parameters.Add("@p_last_name", SqlDbType.NVarChar).Value = request.LastName;
			command.Parameters.Add("@p_birthdate", SqlDbType.DateTime).Value = (object?)request.DateOfBirth ?? DBNull.Value;
			command.Parameters.Add("@p_emailaddress", SqlDbType.NVarChar).Value = request.EmailAddress;
			command.Parameters.Add("@p_phonenumber", SqlDbType.NVarChar).Value = request.PhoneNumber;
			command.Parameters.Add("@p_sss_id_number", SqlDbType.NVarChar).Value = (object?)request.SSSIDNumber ?? string.Empty;
			command.Parameters.Add("@p_tin_id_number", SqlDbType.NVarChar).Value = (object?)request.TIN ?? string.Empty;
			command.Parameters.Add("@p_remarks", SqlDbType.NVarChar).Value = (object?)request.Remarks ?? string.Empty;
			command.Parameters.Add("@p_requestor_firstname", SqlDbType.NVarChar).Value = request.RequestorFirstName;
			command.Parameters.Add("@p_requestor_lastname", SqlDbType.NVarChar).Value = request.RequestorLastName;
			command.Parameters.Add("@p_site", SqlDbType.NVarChar).Value = request.Site;
			command.Parameters.Add("@p_tat_id", SqlDbType.Int).Value = request.TurnAroundTimeID;
			command.Parameters.Add("@p_report_type_id", SqlDbType.Int).Value = request.ReportTypeID;
			// The legacy stored procedure declares this parameter with the
			// "coutry" misspelling; the name must match it exactly.
			command.Parameters.Add("@p_coutry_id", SqlDbType.Int).Value = request.CountryID;
			command.Parameters.Add("@p_province_id", SqlDbType.Int).Value = request.ProvinceID;
			command.Parameters.Add("@p_city_id", SqlDbType.Int).Value = request.CityID;
			command.Parameters.Add("@p_street_address", SqlDbType.NVarChar).Value = (object?)request.Address ?? string.Empty;
			command.Parameters.Add("@p_postal_code", SqlDbType.NVarChar).Value = (object?)request.PostalCode ?? string.Empty;
			command.Parameters.Add("@p_reference_no", SqlDbType.NVarChar).Value = referenceNumber;

			await using var reader = await command.ExecuteReaderAsync(cancellationToken);

			if (await reader.ReadAsync(cancellationToken))
			{
				return new OMSTicketCreated(
					reader["ticket_no"]?.ToString() ?? string.Empty,
					Convert.ToDateTime(reader["delivery_date"]));
			}

			return null;
		}
		catch (SqlException ex)
		{
			logger.LogError(ex, "Ticket creation failed against the OMS database.");

			throw new InternalServerException("An error occurred while contacting the OMS database.");
		}
	}

	private static async Task<bool> ReadIsValidAsync(
		SqlCommand command,
		CancellationToken cancellationToken)
	{
		await using var reader = await command.ExecuteReaderAsync(cancellationToken);

		if (await reader.ReadAsync(cancellationToken))
		{
			// The legacy procedures surface the verdict as an "isValid" column;
			// Convert tolerates bit, int and string representations alike.
			return Convert.ToBoolean(reader["isValid"]);
		}

		return false;
	}
}
