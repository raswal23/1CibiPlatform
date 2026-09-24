using EmploymentVerification.DTO;
using EmploymentVerification.Features.ContactDirectory.Command.AddContact;
using EmploymentVerification.Features.ContactDirectory.Command.EditContact;
using EmploymentVerification.Features.ContactDirectory.Query.GetContacts;
using FluentAssertions;

namespace EmploymentVerification.UnitTests;

/// <summary>
/// The validators run in ValidationBehavior before the handler, so anything they reject
/// never reaches PostgreSQL as a DbUpdateException. The delimiter rule is the
/// non-obvious one and is pinned here in both commands.
/// </summary>
public class ContactDirectoryValidatorTests
{
	private static AddContactCommand AddCommand(
		string companyName = "ACME",
		string emailAddress = "hr@acme.test") =>
		new(new AddEmploymentVerificationContactDTO(companyName, emailAddress, true));

	private static EditContactCommand EditCommand(
		string companyName = "ACME",
		string emailAddress = "hr@acme.test",
		Guid? id = null) =>
		new(new EditEmploymentVerificationContactDTO(
			id ?? Guid.NewGuid(),
			companyName,
			emailAddress,
			true));

	[Fact]
	public void AddValidator_ShouldReject_WhenCompanyNameContainsTheCursorDelimiter()
	{
		// The keyset cursor is base64("company|id") and CursorCodec splits on '|', so a
		// name carrying one makes every cursor minted from that row decode as null -
		// silently pinning the user to the first page from that row onward.
		var result = new AddContactCommandValidator().Validate(AddCommand("ACME | GROUP"));

		result.IsValid.Should().BeFalse();
		result.Errors.Should().Contain(error => error.ErrorMessage.Contains("'|'"));
	}

	[Fact]
	public void EditValidator_ShouldReject_WhenCompanyNameContainsTheCursorDelimiter()
	{
		var result = new EditContactCommandValidator().Validate(EditCommand("ACME | GROUP"));

		result.IsValid.Should().BeFalse();
		result.Errors.Should().Contain(error => error.ErrorMessage.Contains("'|'"));
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("not-an-email")]
	[InlineData("two@at@signs.test")]
	public void AddValidator_ShouldReject_WhenEmailIsNotAnAddress(string emailAddress)
	{
		new AddContactCommandValidator()
			.Validate(AddCommand(emailAddress: emailAddress))
			.IsValid.Should().BeFalse();
	}

	[Fact]
	public void AddValidator_ShouldAccept_WhenDomainHasNoDot()
	{
		// Pins what the server actually enforces. FluentValidation's EmailAddress()
		// accepts a dotless domain, so the dialog's client-side check must not be
		// stricter - a client that blocks what the API would have taken is worse than
		// one that lets the 400 through.
		new AddContactCommandValidator()
			.Validate(AddCommand(emailAddress: "hr@localdomain"))
			.IsValid.Should().BeTrue();
	}

	[Fact]
	public void AddValidator_ShouldReject_WhenCompanyNameExceedsTheColumnLength()
	{
		// Must agree with the 150-character column, or an over-long name reaches
		// PostgreSQL as a DbUpdateException instead of a 400.
		new AddContactCommandValidator()
			.Validate(AddCommand(new string('A', 151)))
			.IsValid.Should().BeFalse();
	}

	[Fact]
	public void AddValidator_ShouldAccept_WhenCompanyAndEmailAreWellFormed()
	{
		new AddContactCommandValidator().Validate(AddCommand()).IsValid.Should().BeTrue();
	}

	[Fact]
	public void EditValidator_ShouldReject_WhenIdIsEmpty()
	{
		new EditContactCommandValidator()
			.Validate(EditCommand(id: Guid.Empty))
			.IsValid.Should().BeFalse();
	}

	[Theory]
	[InlineData(0)]
	[InlineData(101)]
	public void GetValidator_ShouldReject_WhenPageSizeIsOutsideTheClampRange(int pageSize)
	{
		new GetContactsQueryValidator()
			.Validate(new GetContactsQuery(PageSize: pageSize))
			.IsValid.Should().BeFalse();
	}

	[Fact]
	public void GetValidator_ShouldAccept_WhenPageSizeIsOmitted()
	{
		new GetContactsQueryValidator()
			.Validate(new GetContactsQuery(PageSize: null))
			.IsValid.Should().BeTrue();
	}
}
