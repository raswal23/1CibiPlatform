using ATS.Data.Entities;
using ATS.Features.GetEmailIdAndApplicationFormPath;
using BuildingBlocks.Exceptions;
using FluentAssertions;
using Test.BackendAPI.Infrastructure.ATS.Infrastracture;

namespace Test.BackendAPI.Modules.ATS.IntegrationTests;

public class GetEmailIdAndApplicationFormPathIntegrationTests : BaseIntegrationTest
{
	private readonly string _applicationFormPath;
	public GetEmailIdAndApplicationFormPathIntegrationTests(IntegrationTestWebAppFactory factory) : base(factory)
	{
		_applicationFormPath = _configuration["ATS:ApplicationFormBaseUrl"] = "application-forms";
	}

	[Fact]
	public async Task GetEmailIdAndApplicationFormPath_WithValidHashToken_ShouldReturnEmailIdAndPath()
	{
		// Arrange
		Guid emailId = Guid.CreateVersion7();
		string _hashToken = "valid-hash-token-xyz";

		await SeedEmailInvitationRequestData(emailId, _hashToken);

		var query = new GetEmailIdAndApplicationFormHandlerRequest(_hashToken);
		// Act
		var result = await _sender.Send(query);

		// Assert
		result.Should().NotBeNull();
		result.EmailIdAndApplicationFormPath.Should().NotBeNull();
		result.EmailIdAndApplicationFormPath.EmailId.Should().Be(emailId);
		result.EmailIdAndApplicationFormPath.ApplicationFormPath.Should().Be(_applicationFormPath);
	}

	[Fact]
	public async Task GetEmailIdAndApplicationFormPath_WithInvalidHashToken_ShouldReturnEmptyGuid()
	{
		// Arrange
		var invalidHashToken = "invalid-hash-token-xyz";
		var query = new GetEmailIdAndApplicationFormHandlerRequest(invalidHashToken);

		// Act
		Func<Task> act = () => _sender.Send(query);

		// Assert
		await act.Should().ThrowAsync<NotFoundException>().WithMessage("No record found for the provided hash token.");
	}

	[Fact]
	public async Task GetEmailIdAndApplicationFormPath_WithEmptyHashToken_ShouldReturnEmptyGuid()
	{
		// Arrange
		var emptyHashToken = string.Empty;
		var query = new GetEmailIdAndApplicationFormHandlerRequest(emptyHashToken);

		// Act
		Func<Task> act = () => _sender.Send(query);

		// Assert
		await act.Should().ThrowAsync<NotFoundException>().WithMessage("No record found for the provided hash token.");
	}

	private async Task SeedEmailInvitationRequestData(
		Guid emailInvitationId,
		string hashToken)
	{
		var emailInvitationRequest = new EmailInvitationRequest
		{
			EmailInvitationID = emailInvitationId,
			LastName = "Dela Cruz",
			FirstName = "Juan",
			MiddleInitial = "S",
			EmailAddress = "jsdelacruz@cibi.com.ph",
			MobileNumber = "+639171234567",
			HashToken = hashToken,
			EmailSentStatus = "Pending",
			HashTokenCreatedAt = DateTime.UtcNow,
			IsFormCompleted = false,
			HashTokenExpiration = DateTime.UtcNow.AddDays(1),
			SelectPackage = "Air BnB",
			RushNormal = "Rush"
		};

		await _dbContext.EmailInvitationRequests.AddAsync(emailInvitationRequest);
		await _dbContext.SaveChangesAsync();
	}

}
