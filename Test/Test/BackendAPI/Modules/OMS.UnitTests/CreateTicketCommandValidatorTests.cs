using FluentAssertions;
using OMS.Features.Tickets.Command.CreateTicket;
using OMS.Shared.Contracts;
using Test.BackendAPI.Modules.OMS.UnitTests.Fixture;

namespace Test.BackendAPI.Modules.OMS.UnitTests;

public class CreateTicketCommandValidatorTests
{
	private readonly CreateTicketCommandValidator _validator = new();

	private static CreateTicketCommand CreateCommand(CreateOMSTicketRequest request) =>
		new(request);

	[Fact]
	public void Validate_ShouldPass_WhenRequestMatchesSamplePayload()
	{
		var result = _validator.Validate(
			CreateCommand(OMSServiceFixture.CreateValidRequest()));

		result.IsValid.Should().BeTrue();
	}

	[Fact]
	public void Validate_ShouldFail_WhenRequestIsNull()
	{
		var result = _validator.Validate(new CreateTicketCommand(null!));

		result.IsValid.Should().BeFalse();
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("Enzo1")]
	[InlineData("Enzo'")]
	[InlineData("Enzo@")]
	public void Validate_ShouldFail_WhenFirstNameIsMissingOrInvalid(string firstName)
	{
		var request = OMSServiceFixture.CreateValidRequest() with { FirstName = firstName };

		var result = _validator.Validate(CreateCommand(request));

		result.IsValid.Should().BeFalse();
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("Evangelista2")]
	[InlineData("Evangelista'")]
	public void Validate_ShouldFail_WhenLastNameIsMissingOrInvalid(string lastName)
	{
		var request = OMSServiceFixture.CreateValidRequest() with { LastName = lastName };

		var result = _validator.Validate(CreateCommand(request));

		result.IsValid.Should().BeFalse();
	}

	[Theory]
	[InlineData("TEST3")]
	[InlineData("TEST_")]
	public void Validate_ShouldFail_WhenMiddleNameIsInvalid(string middleName)
	{
		var request = OMSServiceFixture.CreateValidRequest() with { MiddleName = middleName };

		var result = _validator.Validate(CreateCommand(request));

		result.IsValid.Should().BeFalse();
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	public void Validate_ShouldPass_WhenMiddleNameIsOmitted(string? middleName)
	{
		var request = OMSServiceFixture.CreateValidRequest() with { MiddleName = middleName };

		var result = _validator.Validate(CreateCommand(request));

		result.IsValid.Should().BeTrue();
	}

	[Theory]
	[InlineData("St. John")]
	[InlineData("Anne-Marie")]
	public void Validate_ShouldPass_WhenNameContainsDotOrHyphen(string firstName)
	{
		var request = OMSServiceFixture.CreateValidRequest() with { FirstName = firstName };

		var result = _validator.Validate(CreateCommand(request));

		result.IsValid.Should().BeTrue();
	}

	[Theory]
	[InlineData("")]
	[InlineData("not-an-email")]
	public void Validate_ShouldFail_WhenEmailAddressIsMissingOrInvalid(string emailAddress)
	{
		var request = OMSServiceFixture.CreateValidRequest() with { EmailAddress = emailAddress };

		var result = _validator.Validate(CreateCommand(request));

		result.IsValid.Should().BeFalse();
	}

	[Theory]
	[InlineData("")]
	[InlineData("0999999999")]
	[InlineData("0999999999999")]
	[InlineData("0999999999a")]
	public void Validate_ShouldFail_WhenPhoneNumberIsMissingOrInvalid(string phoneNumber)
	{
		var request = OMSServiceFixture.CreateValidRequest() with { PhoneNumber = phoneNumber };

		var result = _validator.Validate(CreateCommand(request));

		result.IsValid.Should().BeFalse();
	}

	[Fact]
	public void Validate_ShouldPass_WhenPhoneNumberHasTwelveDigits()
	{
		var request = OMSServiceFixture.CreateValidRequest() with { PhoneNumber = "639999999999" };

		var result = _validator.Validate(CreateCommand(request));

		result.IsValid.Should().BeTrue();
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void Validate_ShouldFail_WhenTurnAroundTimeIDIsNotPositive(int turnAroundTimeId)
	{
		var request = OMSServiceFixture.CreateValidRequest() with { TurnAroundTimeID = turnAroundTimeId };

		var result = _validator.Validate(CreateCommand(request));

		result.IsValid.Should().BeFalse();
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void Validate_ShouldFail_WhenReportTypeIDIsNotPositive(int reportTypeId)
	{
		var request = OMSServiceFixture.CreateValidRequest() with { ReportTypeID = reportTypeId };

		var result = _validator.Validate(CreateCommand(request));

		result.IsValid.Should().BeFalse();
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public void Validate_ShouldFail_WhenRequestorFirstNameIsMissing(string requestorFirstName)
	{
		var request = OMSServiceFixture.CreateValidRequest() with { RequestorFirstName = requestorFirstName };

		var result = _validator.Validate(CreateCommand(request));

		result.IsValid.Should().BeFalse();
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public void Validate_ShouldFail_WhenRequestorLastNameIsMissing(string requestorLastName)
	{
		var request = OMSServiceFixture.CreateValidRequest() with { RequestorLastName = requestorLastName };

		var result = _validator.Validate(CreateCommand(request));

		result.IsValid.Should().BeFalse();
	}

	[Theory]
	[InlineData("")]
	[InlineData("not-an-email")]
	public void Validate_ShouldFail_WhenRequestorEmailAddressIsMissingOrInvalid(string requestorEmailAddress)
	{
		var request = OMSServiceFixture.CreateValidRequest() with { RequestorEmailAddress = requestorEmailAddress };

		var result = _validator.Validate(CreateCommand(request));

		result.IsValid.Should().BeFalse();
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public void Validate_ShouldFail_WhenSiteIsMissing(string site)
	{
		var request = OMSServiceFixture.CreateValidRequest() with { Site = site };

		var result = _validator.Validate(CreateCommand(request));

		result.IsValid.Should().BeFalse();
	}

	[Theory]
	[InlineData("111111111")]
	[InlineData("11111111101")]
	[InlineData("111111111a")]
	public void Validate_ShouldFail_WhenSSSIDNumberIsInvalid(string sssIdNumber)
	{
		var request = OMSServiceFixture.CreateValidRequest() with { SSSIDNumber = sssIdNumber };

		var result = _validator.Validate(CreateCommand(request));

		result.IsValid.Should().BeFalse();
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	public void Validate_ShouldPass_WhenSSSIDNumberIsOmitted(string? sssIdNumber)
	{
		var request = OMSServiceFixture.CreateValidRequest() with { SSSIDNumber = sssIdNumber };

		var result = _validator.Validate(CreateCommand(request));

		result.IsValid.Should().BeTrue();
	}

	[Theory]
	[InlineData("12345678923")]
	[InlineData("1234567892345")]
	[InlineData("12345678923a")]
	public void Validate_ShouldFail_WhenTINIsInvalid(string tin)
	{
		var request = OMSServiceFixture.CreateValidRequest() with { TIN = tin };

		var result = _validator.Validate(CreateCommand(request));

		result.IsValid.Should().BeFalse();
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	public void Validate_ShouldPass_WhenTINIsOmitted(string? tin)
	{
		var request = OMSServiceFixture.CreateValidRequest() with { TIN = tin };

		var result = _validator.Validate(CreateCommand(request));

		result.IsValid.Should().BeTrue();
	}

	[Fact]
	public void Validate_ShouldFail_WhenPostalCodeContainsLetters()
	{
		var request = OMSServiceFixture.CreateValidRequest() with { PostalCode = "12a4" };

		var result = _validator.Validate(CreateCommand(request));

		result.IsValid.Should().BeFalse();
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("1234")]
	public void Validate_ShouldPass_WhenPostalCodeIsOmittedOrDigits(string? postalCode)
	{
		var request = OMSServiceFixture.CreateValidRequest() with { PostalCode = postalCode };

		var result = _validator.Validate(CreateCommand(request));

		result.IsValid.Should().BeTrue();
	}
}
