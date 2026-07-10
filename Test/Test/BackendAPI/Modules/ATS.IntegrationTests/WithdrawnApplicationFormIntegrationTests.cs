using ATS.Data.Entities;
using ATS.Features.WithdrawnApplicationForm;
using BuildingBlocks.Exceptions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Test.BackendAPI.Infrastructure.ATS.Infrastracture;

namespace Test.BackendAPI.Modules.ATS.IntegrationTests;

public class WithdrawnApplicationFormIntegrationTests : BaseIntegrationTest
{
	public WithdrawnApplicationFormIntegrationTests(IntegrationTestWebAppFactory factory)
		: base(factory)
	{
	}

	#region Negative Path
	[Fact]
	public async Task WithdrawnApplicationForm_ShouldThrowNotFoundException_WhenHashTokenDoesNotExist()
	{
		// Arrange
		var command = new WithdrawnApplicationFormCommand("invalid-hash-token");

		// Act
		Func<Task> act = async () => await _sender.Send(command);

		// Assert
		await act.Should()
			.ThrowAsync<NotFoundException>()
			.WithMessage("No record found for the provided hash token.");
	}

	[Fact]
	public async Task WithdrawnApplicationForm_ShouldReturnTrueAndUpdateRecord_WhenHashTokenExists()
	{
		// Arrange
		var application = new EmailInvitationRequest
		{
			EmailInvitationID = Guid.CreateVersion7(),
			FirstName = "Integration",
			LastName = "Tester",
			MiddleInitial = "A",
			EmailAddress = "integration@example.com",
			MobileNumber = "09171234567",
			HashToken = "valid-hash-token",
			HashTokenCreatedAt = DateTime.UtcNow,
			HashTokenExpiration = DateTime.UtcNow.AddDays(1),
			SelectPackage = "Standard",
			RushNormal = "Normal",
			EmailSentStatus = "Done",
			ApplicationFormStatus = "Pending" // replace with your actual column
		};

		await _dbContext.EmailInvitationRequests.AddAsync(application);
		await _dbContext.SaveChangesAsync();

		var command = new WithdrawnApplicationFormCommand(application.HashToken);

		// Act
		var result = await _sender.Send(command);

		// Assert
		var updated = await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.SingleAsync(x => x.EmailInvitationID == application.EmailInvitationID);

		updated.Should().NotBeNull();
		updated!.ApplicationFormStatus.Should().Be("Withdrawn"); 
	}
	#endregion
}