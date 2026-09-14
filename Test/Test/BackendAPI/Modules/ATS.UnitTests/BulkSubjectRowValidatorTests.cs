using ATS.Data.DTO;
using ATS.Services.BulkSubmissionProcessor;
using FluentAssertions;

namespace Test.BackendAPI.Modules.ATS.UnitTests;

public class BulkSubjectRowValidatorTests
{
	private static BulkUploadCsvRecord ValidRow() => new()
	{
		FirstName = "Juan",
		LastName = "Dela Cruz",
		MiddleInitial = "S",
		EmailAddress = "juan@example.com",
		MobileNumber = "09171234567"
	};

	[Fact]
	public void Validate_ShouldAcceptACompleteRow()
	{
		BulkSubjectRowValidator.Validate(ValidRow()).Failure.Should().BeNull();
	}

	// The explicit requirement: plenty of subjects have no middle name, so an absent
	// value must never reject the row.
	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void Validate_ShouldAcceptARow_WithNoMiddleName(string? middleInitial)
	{
		var row = ValidRow();
		row.MiddleInitial = middleInitial;

		BulkSubjectRowValidator.Validate(row).Failure.Should().BeNull();
	}

	[Fact]
	public void Validate_ShouldAcceptAFullMiddleName_NotJustAnInitial()
	{
		var row = ValidRow();
		row.MiddleInitial = "Santos";

		// The column is named MiddleInitial but is not constrained to one character.
		BulkSubjectRowValidator.Validate(row).Failure.Should().BeNull();
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void Validate_ShouldRejectARow_WithNoFirstName(string? firstName)
	{
		var row = ValidRow();
		row.FirstName = firstName;

		BulkSubjectRowValidator.Validate(row).Failure.Should().Be("First name is required.");
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	public void Validate_ShouldRejectARow_WithNoLastName(string? lastName)
	{
		var row = ValidRow();
		row.LastName = lastName;

		BulkSubjectRowValidator.Validate(row).Failure.Should().Be("Last name is required.");
	}

	[Fact]
	public void Validate_ShouldRejectAnOverlongName()
	{
		var row = ValidRow();
		row.FirstName = new string('x', 51);

		BulkSubjectRowValidator.Validate(row).Failure.Should().Contain("50 characters");
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	public void Validate_ShouldRejectARow_WithNoEmail(string? email)
	{
		var row = ValidRow();
		row.EmailAddress = email;

		BulkSubjectRowValidator.Validate(row).Failure.Should().Be("Email address is required.");
	}

	[Theory]
	[InlineData("not-an-email")]
	[InlineData("juan@")]
	[InlineData("@example.com")]
	[InlineData("juan example@test.com")]
	[InlineData("juan@localhost")]
	public void Validate_ShouldRejectAMalformedEmail(string email)
	{
		var row = ValidRow();
		row.EmailAddress = email;

		BulkSubjectRowValidator.Validate(row).Failure.Should().Be("Email address is not a valid email.");
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	public void Validate_ShouldRejectARow_WithNoMobileNumber(string? mobileNumber)
	{
		var row = ValidRow();
		row.MobileNumber = mobileNumber;

		BulkSubjectRowValidator.Validate(row).Failure.Should().Be("Mobile number is required.");
	}

	// The same number written several ways. An integrator should not have to reformat
	// their data, and the stored value is the local form either way.
	[Theory]
	[InlineData("09171234567", "09171234567")]
	[InlineData("+639171234567", "09171234567")]
	[InlineData("639171234567", "09171234567")]
	[InlineData("9171234567", "09171234567")]
	[InlineData("0917-123-4567", "09171234567")]
	[InlineData("0917 123 4567", "09171234567")]
	public void Validate_ShouldNormalizeAMobileNumber_ToTheLocalForm(string written, string expected)
	{
		var row = ValidRow();
		row.MobileNumber = written;

		var (failure, mobileNumber) = BulkSubjectRowValidator.Validate(row);

		failure.Should().BeNull();
		mobileNumber.Should().Be(expected);
	}

	[Theory]
	[InlineData("12345")]
	[InlineData("0917123456")]        // too short even after normalising
	[InlineData("0917123456789")]     // too long
	[InlineData("not a number")]
	public void Validate_ShouldRejectAMobileNumber_ThatCannotBeANumber(string mobileNumber)
	{
		var row = ValidRow();
		row.MobileNumber = mobileNumber;

		BulkSubjectRowValidator.Validate(row).Failure.Should().Be("Mobile number must be 11 digits.");
	}

	[Fact]
	public void Validate_ShouldTolerateSurroundingWhitespace()
	{
		var row = new BulkUploadCsvRecord
		{
			FirstName = "  Juan  ",
			LastName = "  Dela Cruz  ",
			MiddleInitial = null,
			EmailAddress = "  juan@example.com  ",
			MobileNumber = "  09171234567  "
		};

		BulkSubjectRowValidator.Validate(row).Failure.Should().BeNull();
	}

	// ValidateIdentity guards the data-screening path, where no application form is sent
	// and so the candidate never gets a chance to supply these values themselves.
	private static BulkUploadCsvRecord ValidIdentityRow()
	{
		var row = ValidRow();
		row.DateOfBirth = "03/15/1990";
		row.SSSNumber = "1234567890";
		row.TINNumber = "123456789012";

		return row;
	}

	[Fact]
	public void ValidateIdentity_ShouldAcceptACompleteRow()
	{
		var (failure, dateOfBirth) = BulkSubjectRowValidator.ValidateIdentity(ValidIdentityRow());

		failure.Should().BeNull();
		dateOfBirth.Should().Be(new DateOnly(1990, 3, 15));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ValidateIdentity_ShouldRejectAMissingDateOfBirth(string? dateOfBirth)
	{
		var row = ValidIdentityRow();
		row.DateOfBirth = dateOfBirth;

		BulkSubjectRowValidator.ValidateIdentity(row)
			.Failure.Should().Be("Date of birth is required for data screening.");
	}

	[Theory]
	[InlineData("1990-03-15")]        // ISO, not the template's format
	[InlineData("15/03/1990")]        // day first
	[InlineData("March 15, 1990")]
	public void ValidateIdentity_ShouldRejectADateOfBirth_InAnotherFormat(string dateOfBirth)
	{
		var row = ValidIdentityRow();
		row.DateOfBirth = dateOfBirth;

		BulkSubjectRowValidator.ValidateIdentity(row)
			.Failure.Should().Be("Date of birth must be in MM/dd/yyyy format.");
	}

	[Fact]
	public void ValidateIdentity_ShouldRejectADateOfBirth_ThatIsNotInThePast()
	{
		var row = ValidIdentityRow();
		row.DateOfBirth = DateTime.UtcNow.AddYears(1).ToString("MM/dd/yyyy");

		BulkSubjectRowValidator.ValidateIdentity(row)
			.Failure.Should().Be("Date of birth must be in the past.");
	}

	[Theory]
	[InlineData("123456789")]         // 9 digits
	[InlineData("12345678901")]       // 11 digits
	[InlineData("12345-6789")]        // punctuation is not stripped
	public void ValidateIdentity_ShouldRejectAnSssNumber_ThatIsNotTenDigits(string sssNumber)
	{
		var row = ValidIdentityRow();
		row.SSSNumber = sssNumber;

		BulkSubjectRowValidator.ValidateIdentity(row)
			.Failure.Should().Be("SSS number must be 10 digits.");
	}

	[Theory]
	[InlineData("12345678")]          // 8 digits
	[InlineData("1234567890123")]     // 13 digits
	[InlineData("123-456-789")]
	public void ValidateIdentity_ShouldRejectATinNumber_OutsideNineToTwelveDigits(string tinNumber)
	{
		var row = ValidIdentityRow();
		row.TINNumber = tinNumber;

		BulkSubjectRowValidator.ValidateIdentity(row)
			.Failure.Should().Be("TIN number must be 9 to 12 digits.");
	}

	[Theory]
	[InlineData("123456789")]         // the 9-digit lower bound
	[InlineData("123456789012")]      // the 12-digit upper bound
	public void ValidateIdentity_ShouldAcceptATinNumber_AtEitherBound(string tinNumber)
	{
		var row = ValidIdentityRow();
		row.TINNumber = tinNumber;

		BulkSubjectRowValidator.ValidateIdentity(row).Failure.Should().BeNull();
	}

	[Fact]
	public void ValidateIdentity_ShouldTolerateSurroundingWhitespace()
	{
		var row = ValidIdentityRow();
		row.DateOfBirth = "  03/15/1990  ";
		row.SSSNumber = "  1234567890  ";
		row.TINNumber = "  123456789012  ";

		BulkSubjectRowValidator.ValidateIdentity(row).Failure.Should().BeNull();
	}
}
