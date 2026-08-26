using ATS.Data.DTO;
using ATS.Services.OMSTicketing;

namespace Test.BackendAPI.Modules.ATS.UnitTests;

public class OMSTicketPayloadMapperTests
{
	private const string RequestorFirstName = "John";
	private const string RequestorLastName = "Doe";

	// An order as it exists at enrolment: no PersonalDetails row yet, so no DOB,
	// SSS or TIN. This is the common case the mapper has to handle.
	private static TicketablePayloadDTO NewlyEnrolledOrder() => new()
	{
		EmailInvitationID = Guid.CreateVersion7(),
		FirstName = "Juan",
		MiddleInitial = "P",
		LastName = "Dela Cruz",
		EmailAddress = "juan.delacruz@example.com",
		MobileNumber = "09171234567",
		SelectPackage = "CRIMINAL RECORDS CHECK",
		PackageDescription = "182",
		Site = "24 - 7 INTOUCH- CEBU",
		RequestorEmail = "john.doe@example.com"
	};

	[Fact]
	public void TryMap_ShouldProduceTheAgreedPayload_WhenTheOrderIsComplete()
	{
		var payload = NewlyEnrolledOrder();

		var (request, failure) = OMSTicketPayloadMapper.TryMap(
			payload,
			RequestorFirstName,
			RequestorLastName);

		Assert.Null(failure);
		Assert.NotNull(request);

		Assert.Equal("Juan", request!.FirstName);
		Assert.Equal("P", request.MiddleName);
		Assert.Equal("Dela Cruz", request.LastName);
		Assert.Equal("juan.delacruz@example.com", request.EmailAddress);
		Assert.Equal("09171234567", request.PhoneNumber);
		Assert.Equal(RequestorFirstName, request.RequestorFirstName);
		Assert.Equal(RequestorLastName, request.RequestorLastName);
		Assert.Equal("john.doe@example.com", request.RequestorEmailAddress);
		Assert.Equal("24 - 7 INTOUCH- CEBU", request.Site);
		Assert.Equal(182, request.ReportTypeID);

		// The agreed constants for auto-ticketing.
		Assert.Equal("Remarks", request.Remarks);
		Assert.Equal(2, request.TurnAroundTimeID);
		Assert.Equal(0, request.CountryID);
		Assert.Equal(0, request.ProvinceID);
		Assert.Equal(0, request.CityID);
		Assert.Equal(string.Empty, request.Address);
		Assert.Equal(string.Empty, request.PostalCode);
	}

	[Fact]
	public void TryMap_ShouldLeaveBirthDateAndGovernmentIdsBlank_WhenTheFormIsNotSubmittedYet()
	{
		var payload = NewlyEnrolledOrder();

		var (request, failure) = OMSTicketPayloadMapper.TryMap(
			payload,
			RequestorFirstName,
			RequestorLastName);

		Assert.Null(failure);

		// OMS only applies its 10/12-digit rules when these are non-empty, so an order
		// ticketed before the applicant fills the form is still valid.
		Assert.Null(request!.DateOfBirth);
		Assert.Null(request.SSSIDNumber);
		Assert.Null(request.TIN);
	}

	[Fact]
	public void TryMap_ShouldUseTheApplicantSuppliedDetails_WhenTheFormHasBeenSubmitted()
	{
		var payload = NewlyEnrolledOrder();
		payload.DOB = new DateOnly(1990, 5, 17);
		payload.SSS = "1111111110";
		payload.TIN = "123456789234";
		payload.PersonalMobileNumber = "09998887777";

		var (request, failure) = OMSTicketPayloadMapper.TryMap(
			payload,
			RequestorFirstName,
			RequestorLastName);

		Assert.Null(failure);
		Assert.Equal(new DateTime(1990, 5, 17), request!.DateOfBirth);
		Assert.Equal("1111111110", request.SSSIDNumber);
		Assert.Equal("123456789234", request.TIN);

		// The subject's own number wins over the one captured at enrolment.
		Assert.Equal("09998887777", request.PhoneNumber);
	}

	[Theory]
	[InlineData("182", 182)]
	[InlineData("  182  ", 182)]
	[InlineData("182 - Criminal Records Check", 182)]
	public void TryMap_ShouldReadTheReportTypeFromThePackageDescription(
		string packageDescription,
		int expectedReportTypeId)
	{
		var payload = NewlyEnrolledOrder();
		payload.PackageDescription = packageDescription;

		var (request, failure) = OMSTicketPayloadMapper.TryMap(
			payload,
			RequestorFirstName,
			RequestorLastName);

		Assert.Null(failure);
		Assert.Equal(expectedReportTypeId, request!.ReportTypeID);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("Criminal Records Check")]
	[InlineData("0")]
	public void TryMap_ShouldFail_WhenThePackageDoesNotResolveToAReportType(string? packageDescription)
	{
		var payload = NewlyEnrolledOrder();
		payload.PackageDescription = packageDescription;

		var (request, failure) = OMSTicketPayloadMapper.TryMap(
			payload,
			RequestorFirstName,
			RequestorLastName);

		// Parked rather than sent: OMS would reject it, and nothing about the order
		// changes on its own.
		Assert.Null(request);
		Assert.NotNull(failure);
	}

	[Fact]
	public void TryMap_ShouldFail_WhenTheRequestorHasNoSite()
	{
		var payload = NewlyEnrolledOrder();
		payload.Site = null;

		var (request, failure) = OMSTicketPayloadMapper.TryMap(
			payload,
			RequestorFirstName,
			RequestorLastName);

		Assert.Null(request);
		Assert.Contains("site", failure!, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public void TryMap_ShouldFail_WhenTheRequestorNameCannotBeResolved()
	{
		var payload = NewlyEnrolledOrder();

		var (request, failure) = OMSTicketPayloadMapper.TryMap(payload, null, null);

		Assert.Null(request);
		Assert.Contains("requestor", failure!, StringComparison.OrdinalIgnoreCase);
	}

	[Theory]
	[InlineData("09171234567", "09171234567")]
	[InlineData("0917 123 4567", "09171234567")]
	[InlineData("0917-123-4567", "09171234567")]
	[InlineData("+639171234567", "09171234567")]
	[InlineData("639171234567", "09171234567")]
	[InlineData("9171234567", "09171234567")]
	public void NormalizePhoneNumber_ShouldProduceTheLocalElevenDigitForm(string stored, string expected)
	{
		Assert.Equal(expected, OMSTicketPayloadMapper.NormalizePhoneNumber(stored));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("12345")]
	[InlineData("not a number")]
	public void NormalizePhoneNumber_ShouldReturnNull_WhenOMSWouldRejectIt(string? stored)
	{
		Assert.Null(OMSTicketPayloadMapper.NormalizePhoneNumber(stored));
	}

	[Fact]
	public void TryMap_ShouldFail_WhenNoUsablePhoneNumberExists()
	{
		var payload = NewlyEnrolledOrder();
		payload.MobileNumber = "12345";
		payload.PersonalMobileNumber = null;

		var (request, failure) = OMSTicketPayloadMapper.TryMap(
			payload,
			RequestorFirstName,
			RequestorLastName);

		Assert.Null(request);
		Assert.Contains("phone", failure!, StringComparison.OrdinalIgnoreCase);
	}

	[Theory]
	[InlineData("1111111110", 10, "1111111110")]
	[InlineData("11-1111-1110", 10, "1111111110")]
	[InlineData("123456789234", 12, "123456789234")]
	public void NormalizeGovernmentId_ShouldKeepDigitsOfTheRequiredLength(
		string stored,
		int requiredLength,
		string expected)
	{
		Assert.Equal(expected, OMSTicketPayloadMapper.NormalizeGovernmentId(stored, requiredLength));
	}

	[Theory]
	[InlineData("123", 10)]
	[InlineData("", 10)]
	[InlineData(null, 12)]
	public void NormalizeGovernmentId_ShouldReturnNull_WhenItWouldFailOMSValidation(
		string? stored,
		int requiredLength)
	{
		// Sent blank rather than failing the whole ticket: the field is optional.
		Assert.Null(OMSTicketPayloadMapper.NormalizeGovernmentId(stored, requiredLength));
	}
}
