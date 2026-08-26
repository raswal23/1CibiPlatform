using FluentAssertions;
using Moq;
using OMS.Features.Tickets.Command.CreateTicket;
using OMS.Shared.Contracts;
using Test.BackendAPI.Modules.OMS.UnitTests.Fixture;

namespace Test.BackendAPI.Modules.OMS.UnitTests;

public class CreateTicketHandlerTests
{
	[Fact]
	public async Task Handle_ShouldReturnServiceResult_WhenCommandIsValid()
	{
		var request = OMSServiceFixture.CreateValidRequest();
		var expected = new OMSTicketCreated("202608260001", new DateTime(2026, 8, 31));
		var mockTicketCreator = new Mock<IOMSTicketCreator>();
		using var cancellationSource = new CancellationTokenSource();
		mockTicketCreator
			.Setup(ticketCreator => ticketCreator.CreateTicketAsync(
				request,
				cancellationSource.Token))
			.ReturnsAsync(expected);
		var handler = new CreateTicketHandler(mockTicketCreator.Object);

		var result = await handler.Handle(
			new CreateTicketCommand(request),
			cancellationSource.Token);

		result.Should().Be(expected);
		mockTicketCreator.Verify(
			ticketCreator => ticketCreator.CreateTicketAsync(
				request,
				cancellationSource.Token),
			Times.Once);
	}
}
