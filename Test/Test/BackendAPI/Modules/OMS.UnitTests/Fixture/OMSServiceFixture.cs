using Microsoft.Extensions.Logging;
using Moq;
using OMS.Data.Repository;
using OMS.Shared.Contracts;
using OMS.Shared.Implementations;

namespace Test.BackendAPI.Modules.OMS.UnitTests.Fixture;

public class OMSServiceFixture : IDisposable
{
	public Mock<IOMSRepository> MockOMSRepository { get; private set; }

	public Mock<ILogger<OMSTicketCreator>> MockTicketCreatorLogger { get; private set; }

	public OMSTicketCreator TicketCreator { get; private set; }

	public OMSServiceFixture()
	{
		MockOMSRepository = new Mock<IOMSRepository>();
		MockTicketCreatorLogger = new Mock<ILogger<OMSTicketCreator>>();

		TicketCreator = new OMSTicketCreator(
			MockOMSRepository.Object,
			MockTicketCreatorLogger.Object);
	}

	public static CreateOMSTicketRequest CreateValidRequest() =>
		new(
			FirstName: "TEST Enzo",
			MiddleName: "TEST",
			LastName: "TEST Evangelista",
			DateOfBirth: null,
			EmailAddress: "angel.condensada11@gmail.com",
			PhoneNumber: "09999999999",
			SSSIDNumber: "1111111110",
			TIN: "123456789234",
			Remarks: "Remarks",
			RequestorFirstName: "John",
			RequestorLastName: "Doe",
			RequestorEmailAddress: "john.doe@test.com",
			Site: "24 - 7 INTOUCH- CEBU",
			TurnAroundTimeID: 2,
			ReportTypeID: 182,
			CountryID: 0,
			ProvinceID: 0,
			CityID: 0,
			Address: "",
			PostalCode: "");

	public void Dispose()
	{
		// nothing to dispose currently
	}
}
