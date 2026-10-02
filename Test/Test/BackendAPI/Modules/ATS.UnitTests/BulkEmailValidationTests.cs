using ATS.Features.Web.InsertBulkSubject;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace Test.BackendAPI.Modules.ATS.UnitTests;

public class BulkEmailValidationTests
{
	private const string Header = "LastName,FirstName,MiddleInitial,EmailAddress,MobileNumber";

	private static IFormFile CreateCsvFile(string content)
	{
		var bytes = System.Text.Encoding.UTF8.GetBytes(content);
		var stream = new MemoryStream(bytes);
		return new FormFile(stream, 0, bytes.Length, "file", "bulkfile.csv")
		{
			Headers = new HeaderDictionary(),
			ContentType = "text/csv"
		};
	}

	private static string CsvWithEmails(params string[] emails)
	{
		var rows = emails.Select(email => $"Dela Cruz,Juan,S,{email},09171234567");
		return Header + "\n" + string.Join("\n", rows);
	}

	[Fact]
	public async Task ValidateEmailAddressesAsync_ShouldReturnNoRows_WhenAllEmailsAreValid()
	{
		var file = CreateCsvFile(CsvWithEmails("juan@example.com", "maria@test.org"));

		var invalidRows = await BulkEmailValidation.ValidateEmailAddressesAsync(file);

		invalidRows.Should().BeEmpty();
	}

	// The same shapes BulkSubjectRowValidator rejects per-row; this check reports
	// them at upload time instead.
	[Theory]
	[InlineData("not-an-email")]
	[InlineData("juan@")]
	[InlineData("@example.com")]
	[InlineData("juan example@test.com")]
	[InlineData("juan@localhost")]
	public async Task ValidateEmailAddressesAsync_ShouldReturnRow_WhenEmailIsMalformed(string email)
	{
		var file = CreateCsvFile(CsvWithEmails(email));

		var invalidRows = await BulkEmailValidation.ValidateEmailAddressesAsync(file);

		invalidRows.Should().Equal(2);
	}

	[Fact]
	public async Task ValidateEmailAddressesAsync_ShouldReturnEveryBadRow_WhenSeveralEmailsAreMalformed()
	{
		var file = CreateCsvFile(CsvWithEmails("juan@example.com", "not-an-email", "maria@test.org", "juan@"));

		var invalidRows = await BulkEmailValidation.ValidateEmailAddressesAsync(file);

		invalidRows.Should().Equal(3, 5);
	}

	// A blank cell must not fail the upload: the background parser rejects only
	// that row, so one missing email never blocks the rest of the file.
	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public async Task ValidateEmailAddressesAsync_ShouldIgnoreRow_WhenEmailIsBlank(string email)
	{
		var file = CreateCsvFile(CsvWithEmails(email));

		var invalidRows = await BulkEmailValidation.ValidateEmailAddressesAsync(file);

		invalidRows.Should().BeEmpty();
	}

	[Fact]
	public async Task ValidateEmailAddressesAsync_ShouldReturnNoRows_WhenEmailColumnIsMissing()
	{
		var file = CreateCsvFile("LastName,FirstName\nDela Cruz,Juan\nSantos,not-an-email");

		var invalidRows = await BulkEmailValidation.ValidateEmailAddressesAsync(file);

		invalidRows.Should().BeEmpty();
	}

	[Fact]
	public async Task ValidateEmailAddressesAsync_ShouldReturnNoRows_WhenFileIsEmpty()
	{
		var file = CreateCsvFile(string.Empty);

		var invalidRows = await BulkEmailValidation.ValidateEmailAddressesAsync(file);

		invalidRows.Should().BeEmpty();
	}

	[Fact]
	public async Task ValidateEmailAddressesAsync_ShouldMatchHeaderCaseInsensitivelyAndTrimmed()
	{
		var file = CreateCsvFile("LastName, emailaddress \nDela Cruz,not-an-email");

		var invalidRows = await BulkEmailValidation.ValidateEmailAddressesAsync(file);

		invalidRows.Should().Equal(2);
	}
}
