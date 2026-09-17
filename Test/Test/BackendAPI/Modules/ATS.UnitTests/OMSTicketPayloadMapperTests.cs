using ATS.Data.DTO;
using ATS.Services.OMSTicketing;

namespace Test.BackendAPI.Modules.ATS.UnitTests;

public class OMSTicketPayloadMapperTests
{
	private const string RequestorFirstName = "John";
	private const string RequestorLastName = "Doe";

	// An order as it exists at enrolment: identity is only captured on data-screening
	// orders, so no DOB, SSS or TIN. This is the common case the mapper has to handle.
	private static TicketablePayloadDTO NewlyEnrolledOrder() => new()
	{
		EmailInvitationID = Guid.CreateVersion7(),
		FirstName = "Juan",
		MiddleInitial = "P",
		LastName = "Dela Cruz",
		EmailAddress = "juan.delacruz@example.com",
		MobileNumber = "09171234567",
		SelectPackage = "CRIMINAL RECORDS CHECK",
		RushNormal = "Rush",
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

		// Rush, so the OMS rush turnaround rather than a fixed one.
		Assert.Equal(2, request.TurnAroundTimeID);

		// The agreed constants for auto-ticketing.
		Assert.Equal("Remarks", request.Remarks);
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
	public void TryMap_ShouldSendTheIdentityFields_WhenTheOrderCapturedThem()
	{
		var payload = NewlyEnrolledOrder();
		payload.DOB = new DateOnly(1990, 5, 17);
		payload.SSS = "1111111110";
		payload.TIN = "123456789234";

		var (request, failure) = OMSTicketPayloadMapper.TryMap(
			payload,
			RequestorFirstName,
			RequestorLastName);

		Assert.Null(failure);
		Assert.Equal(new DateTime(1990, 5, 17), request!.DateOfBirth);
		Assert.Equal("1111111110", request.SSSIDNumber);
		Assert.Equal("123456789234", request.TIN);
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

	[Theory]
	[InlineData("Rush", 2)]
	[InlineData("Normal", 1)]
	[InlineData("rush", 2)]
	[InlineData("  normal  ", 1)]
	public void TryMap_ShouldTakeTheTurnAroundTimeFromTheOrderType(
		string rushNormal,
		int expectedTurnAroundTimeId)
	{
		var payload = NewlyEnrolledOrder();
		payload.RushNormal = rushNormal;

		var (request, failure) = OMSTicketPayloadMapper.TryMap(
			payload,
			RequestorFirstName,
			RequestorLastName);

		Assert.Null(failure);
		Assert.Equal(expectedTurnAroundTimeId, request!.TurnAroundTimeID);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("Express")]
	public void TryMap_ShouldFail_WhenTheOrderTypeIsNotAKnownTurnAround(string? rushNormal)
	{
		var payload = NewlyEnrolledOrder();
		payload.RushNormal = rushNormal;

		var (request, failure) = OMSTicketPayloadMapper.TryMap(
			payload,
			RequestorFirstName,
			RequestorLastName);

		// No fallback: guessing the turnaround would bill the client for one they did
		// not order, so the order is parked for a human instead.
		Assert.Null(request);
		Assert.Contains("turnaround", failure!, StringComparison.OrdinalIgnoreCase);
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

		var (request, failure) = OMSTicketPayloadMapper.TryMap(
			payload,
			RequestorFirstName,
			RequestorLastName);

		Assert.Null(request);
		Assert.Contains("phone", failure!, StringComparison.OrdinalIgnoreCase);
	}

	[Theory]
	[InlineData("1111111110", 10, 10, "1111111110")]
	[InlineData("11-1111-1110", 10, 10, "1111111110")]
	[InlineData("123456789234", 9, 12, "123456789234")]
	// A TIN with no branch code is 9 digits and is issued as such. An exact-12 rule
	// blanked these, so the ticket reached OMS with no TIN and nothing said so.
	[InlineData("123456789", 9, 12, "123456789")]
	[InlineData("123-456-789", 9, 12, "123456789")]
	// Inside the range but neither bound - a TIN is not only ever 9 or 12 long.
	[InlineData("1234567890", 9, 12, "1234567890")]
	public void NormalizeGovernmentId_ShouldKeepDigitsWithinTheAcceptedLengths(
		string stored,
		int minLength,
		int maxLength,
		string expected)
	{
		Assert.Equal(expected, OMSTicketPayloadMapper.NormalizeGovernmentId(stored, minLength, maxLength));
	}

	[Theory]
	[InlineData("123", 10, 10)]
	[InlineData("", 10, 10)]
	[InlineData(null, 9, 12)]
	// Just outside each TIN bound.
	[InlineData("12345678", 9, 12)]
	[InlineData("1234567892345", 9, 12)]
	public void NormalizeGovernmentId_ShouldReturnNull_WhenItWouldFailOMSValidation(
		string? stored,
		int minLength,
		int maxLength)
	{
		// Sent blank rather than failing the whole ticket: the field is optional.
		Assert.Null(OMSTicketPayloadMapper.NormalizeGovernmentId(stored, minLength, maxLength));
	}

	// The bug this range replaced: a 9-digit TIN on a data-screening order reached OMS
	// blank. End to end through TryMap, because the call site's arguments were the half
	// of it that a NormalizeGovernmentId test alone would never have caught.
	[Fact]
	public void TryMap_ShouldSendANineDigitTin()
	{
		var payload = NewlyEnrolledOrder();
		payload.TIN = "123456789";

		var (request, failure) = OMSTicketPayloadMapper.TryMap(
			payload,
			RequestorFirstName,
			RequestorLastName);

		Assert.Null(failure);
		Assert.Equal("123456789", request!.TIN);
	}
}
