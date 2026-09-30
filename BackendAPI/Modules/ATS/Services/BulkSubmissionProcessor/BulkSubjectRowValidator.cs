namespace ATS.Services.BulkSubmissionProcessor;

/// <summary>
/// Validates one parsed CSV row before it becomes an order. Pure and static so the rules
/// can be tested without a database, a file or the Quartz job.
///
/// The rules deliberately mirror EmailInvitationRequestCommandValidator, which guards the
/// single-endorsement path. Before this existed the bulk path validated only the header
/// row, so a blank email or a malformed mobile number was inserted unchecked and failed
/// much later - at email send, or at OMS ticketing where it parked as an opaque error.
/// </summary>
public static class BulkSubjectRowValidator
{
	private const int MaxNameLength = 50;
	private const int MobileNumberLength = 11;

	/// <summary>
	/// Validates a row and returns the values to store. <c>Failure</c> is null when the
	/// row is usable; otherwise it is the reason it was rejected and the other values
	/// are undefined.
	///
	/// The mobile number is normalised rather than merely checked: an integrator may
	/// reasonably write +639171234567, and that is the same number as 09171234567. The
	/// local form is what gets stored, so every row reads the same downstream.
	/// </summary>
	public static (string? Failure, string MobileNumber) Validate(BulkUploadCsvRecord row)
	{
		var failure = ValidateCore(row, out var mobileNumber);

		return (failure, mobileNumber);
	}

	private static string? ValidateCore(BulkUploadCsvRecord row, out string mobileNumber)
	{
		mobileNumber = string.Empty;

		if (string.IsNullOrWhiteSpace(row.FirstName))
		{
			return "First name is required.";
		}

		if (row.FirstName.Trim().Length > MaxNameLength)
		{
			return $"First name must not exceed {MaxNameLength} characters.";
		}

		if (string.IsNullOrWhiteSpace(row.LastName))
		{
			return "Last name is required.";
		}

		if (row.LastName.Trim().Length > MaxNameLength)
		{
			return $"Last name must not exceed {MaxNameLength} characters.";
		}

		// MiddleInitial is deliberately unvalidated. Plenty of subjects have no middle
		// name, so a blank value is expected and must never reject the row.

		if (string.IsNullOrWhiteSpace(row.EmailAddress))
		{
			return "Email address is required.";
		}

		if (!IsValidEmail(row.EmailAddress.Trim()))
		{
			return "Email address is not a valid email.";
		}

		if (string.IsNullOrWhiteSpace(row.MobileNumber))
		{
			return "Mobile number is required.";
		}

		if (NormalizeMobileNumber(row.MobileNumber) is not { } normalized)
		{
			return $"Mobile number must be {MobileNumberLength} digits.";
		}

		mobileNumber = normalized;

		return null;
	}

	/// <summary>
	/// Reduces a written number to the local 11-digit form, tolerating spaces, dashes,
	/// the +63 country code and a missing trunk zero. Returns null when the result is
	/// not a plausible mobile number.
	///
	/// Mirrors OMSTicketPayloadMapper.NormalizePhoneNumber, which does the same job on
	/// the way out to OMS. Normalising here as well means the stored value is already
	/// correct rather than being fixed up at every consumer.
	/// </summary>
	public static string? NormalizeMobileNumber(string? value)
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

		return digits.Length == MobileNumberLength
			? digits
			: null;
	}

	/// <summary>
	/// Validates the identity columns a data-screening row must carry: the candidate
	/// receives no application form, so the birth date, SSS and TIN cannot be
	/// collected later. The rules mirror EmailInvitationRequestCommandValidator's
	/// Data-mode block, so a CSV row is held to the same standard as the web form.
	/// Only called for data-screening files; manual files never carry the columns.
	/// </summary>
	public static (string? Failure, DateOnly? DateOfBirth) ValidateIdentity(BulkUploadCsvRecord row)
	{
		if (string.IsNullOrWhiteSpace(row.DateOfBirth))
		{
			return ("Date of birth is required for data screening.", null);
		}

		// The template's date format, matching the web form's picker display.
		if (!DateOnly.TryParseExact(row.DateOfBirth.Trim(), "MM/dd/yyyy", out var dateOfBirth))
		{
			return ("Date of birth must be in MM/dd/yyyy format.", null);
		}

		if (dateOfBirth >= DateOnly.FromDateTime(DateTime.UtcNow))
		{
			return ("Date of birth must be in the past.", null);
		}

		var sss = row.SSSNumber?.Trim();
		if (string.IsNullOrEmpty(sss))
		{
			return ("SSS number is required for data screening.", null);
		}

		if (sss.Length != 10 || !sss.All(char.IsDigit))
		{
			return ("SSS number must be 10 digits.", null);
		}

		var tin = row.TINNumber?.Trim();
		if (string.IsNullOrEmpty(tin))
		{
			return ("TIN number is required for data screening.", null);
		}

		if (tin.Length is < 9 or > 12 || !tin.All(char.IsDigit))
		{
			return ("TIN number must be 9 to 12 digits.", null);
		}

		return (null, dateOfBirth);
	}

	// MailAddress accepts the same shape FluentValidation's EmailAddress() rule does,
	// without pulling the validator stack into the parsing job. Public because the
	// upload-time BulkEmailValidation shares this rule, so the two tiers cannot drift.
	public static bool IsValidEmail(string value)
	{
		if (value.Contains(' ', StringComparison.Ordinal))
		{
			return false;
		}

		return MailAddress.TryCreate(value, out var address)
			&& address.Address == value
			&& address.Host.Contains('.', StringComparison.Ordinal);
	}
}
