namespace ATS.Services.OMSTicketing;

/// <summary>
/// Projects a claimed ATS order onto the OMS ticket payload. Pure and static so the
/// mapping rules can be tested without a database or a live OMS connection.
/// </summary>
public static class OMSTicketPayloadMapper
{
	// OMS defaults agreed for auto-ticketing. Address is not collected at enrolment,
	// so the location ids stay 0 and the free-text fields stay empty.
	internal const string DefaultRemarks = "Remarks";
	internal const int DefaultTurnAroundTimeId = 2;
	internal const int DefaultCountryId = 0;
	internal const int DefaultProvinceId = 0;
	internal const int DefaultCityId = 0;

	/// <summary>
	/// Returns the OMS request, or a reason why the order cannot be ticketed at all.
	/// A failure here is never retryable: none of these inputs change on their own.
	/// </summary>
	public static (CreateOMSTicketRequest? Request, string? Failure) TryMap(
		TicketablePayloadDTO payload,
		string? requestorFirstName,
		string? requestorLastName)
	{
		if (string.IsNullOrWhiteSpace(payload.FirstName) || string.IsNullOrWhiteSpace(payload.LastName))
		{
			return (null, "The order has no subject first or last name.");
		}

		if (string.IsNullOrWhiteSpace(payload.EmailAddress))
		{
			return (null, "The order has no subject email address.");
		}

		// The package is matched by name because SelectPackage is free text with no
		// foreign key, so a renamed or deleted package silently stops matching.
		if (string.IsNullOrWhiteSpace(payload.PackageDescription))
		{
			return (null, $"No active package matches \"{payload.SelectPackage}\", so the OMS report type is unknown.");
		}

		if (!TryParseReportTypeId(payload.PackageDescription, out var reportTypeId))
		{
			return (null,
				$"The package description for \"{payload.SelectPackage}\" is not a valid OMS report type id.");
		}

		if (string.IsNullOrWhiteSpace(payload.Site))
		{
			return (null, "The requestor has no ATS site, which OMS requires to validate the PO.");
		}

		if (string.IsNullOrWhiteSpace(requestorFirstName) || string.IsNullOrWhiteSpace(requestorLastName))
		{
			return (null, "The requestor's name could not be resolved.");
		}

		if (string.IsNullOrWhiteSpace(payload.RequestorEmail))
		{
			return (null, "The requestor has no email address.");
		}

		// The subject's own number is preferred once the application form supplies it;
		// before that the number captured at enrolment is the only one available.
		var phoneNumber = NormalizePhoneNumber(payload.PersonalMobileNumber)
			?? NormalizePhoneNumber(payload.MobileNumber);

		if (phoneNumber is null)
		{
			return (null, "The subject has no phone number OMS will accept.");
		}

		var request = new CreateOMSTicketRequest(
			FirstName: payload.FirstName.Trim(),
			MiddleName: string.IsNullOrWhiteSpace(payload.MiddleInitial) ? null : payload.MiddleInitial.Trim(),
			LastName: payload.LastName.Trim(),

			// Null until the applicant submits the form: orders are ticketed at
			// enrolment, and the stored procedure takes DBNull for an absent birthdate.
			DateOfBirth: payload.DOB?.ToDateTime(TimeOnly.MinValue),
			EmailAddress: payload.EmailAddress.Trim(),
			PhoneNumber: phoneNumber,

			// Blank rather than invalid: OMS only applies its 10/12-digit rules when
			// these are non-empty, and neither is collected at enrolment.
			SSSIDNumber: NormalizeGovernmentId(payload.SSS, 10),
			TIN: NormalizeGovernmentId(payload.TIN, 12),
			Remarks: DefaultRemarks,
			RequestorFirstName: requestorFirstName.Trim(),
			RequestorLastName: requestorLastName.Trim(),
			RequestorEmailAddress: payload.RequestorEmail.Trim(),
			Site: payload.Site.Trim(),
			TurnAroundTimeID: DefaultTurnAroundTimeId,
			ReportTypeID: reportTypeId,
			CountryID: DefaultCountryId,
			ProvinceID: DefaultProvinceId,
			CityID: DefaultCityId,
			Address: string.Empty,
			PostalCode: string.Empty);

		return (request, null);
	}

	// The report type id is stored in the package description, which is a free-text
	// column, so tolerate surrounding text and take the leading number.
	private static bool TryParseReportTypeId(string packageDescription, out int reportTypeId)
	{
		reportTypeId = 0;

		var digits = packageDescription.Trim();
		var end = 0;

		while (end < digits.Length && char.IsDigit(digits[end]))
		{
			end++;
		}

		return end > 0
			&& int.TryParse(digits[..end], NumberStyles.None, CultureInfo.InvariantCulture, out reportTypeId)
			&& reportTypeId > 0;
	}

	/// <summary>
	/// Reduces a stored number to the 11-digit local form OMS validates (09XXXXXXXXX),
	/// tolerating spaces, dashes and the +63 country code. Returns null when the value
	/// cannot be made acceptable, so the caller parks the order instead of being
	/// rejected by OMS validation.
	/// </summary>
	public static string? NormalizePhoneNumber(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return null;
		}

		var digits = new string(value.Where(char.IsDigit).ToArray());

		// +63 917... and 63917... both denote the same local 0917... number.
		if (digits.Length == 12 && digits.StartsWith("63", StringComparison.Ordinal))
		{
			digits = string.Concat("0", digits.AsSpan(2));
		}

		// A bare 9-prefixed mobile number is missing only its trunk zero.
		if (digits.Length == 10 && digits.StartsWith('9'))
		{
			digits = "0" + digits;
		}

		// The OMS validator accepts 11 or 12 digits; anything else it would reject.
		return digits.Length is 11 or 12
			? digits
			: null;
	}

	/// <summary>
	/// SSS and TIN are optional. OMS applies an exact-length rule only when the value
	/// is non-empty, so anything that would fail that rule is sent as blank rather
	/// than failing the whole ticket.
	/// </summary>
	public static string? NormalizeGovernmentId(string? value, int requiredLength)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return null;
		}

		var digits = new string(value.Where(char.IsDigit).ToArray());

		return digits.Length == requiredLength
			? digits
			: null;
	}
}
