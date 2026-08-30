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
}
