using ATS.DTO;
using ATS.Features.EmailInvitationRequest;
using FluentAssertions;
using FluentValidation;
using Test.BackendAPI.Infrastructure.ATS.Infrastracture;

namespace Test.BackendAPI.Modules.ATS.IntegrationTests;

public class InsertEmailInvitationRequestIntegrationTests : BaseIntegrationTest
{
	public InsertEmailInvitationRequestIntegrationTests(IntegrationTestWebAppFactory factory) : base(factory)
	{

	}

	#region Positive Path
	[Fact]
	public async Task InsertEmailInvitationRequest_ShouldReturnCreatedIdAndPersist()
	{
		// Arrange
		var dto = new EmailInvitationRequestDTO
		{
			FirstName = "Integration",
			LastName = "Tester",
			MiddleInitial = "A",
			EmailAddress = "integration.tester@example.com",
			MobileNumber = "+639171234567",
			SelectPackage = "Standard",
			RushNormal = "Normal"
		};

		var command = new EmailInvitationRequestCommand(dto);

		// Act
		var result = await _sender.Send(command);

		// Assert
		result.Should().NotBeNull();
		result.isAdded.Should().BeTrue();

		var persisted = _dbContext.EmailInvitationRequests
			.SingleOrDefault(x => x.EmailAddress == dto.EmailAddress);
		persisted.Should().NotBeNull();
		persisted!.EmailAddress.Should().Be(dto.EmailAddress);
		persisted.FirstName.Should().Be(dto.FirstName);
		persisted.LastName.Should().Be(dto.LastName);
	}
	#endregion

	#region Negative Path

	[Fact]
	public async Task InsertEmailInvitationRequest_ShouldThrowValidationException_WhenFirstNameIsEmpty()
	{
		// Arrange
		var dto = new EmailInvitationRequestDTO
		{
			FirstName = "",
			LastName = "Tester",
			EmailAddress = "integration.tester@example.com",
			MobileNumber = "+639171234567",
			SelectPackage = "Standard",
			RushNormal = "Normal"
		};

		var command = new EmailInvitationRequestCommand(dto);

		// Act
		Func<Task> act = async () => await _sender.Send(command);

		// Assert
		await act.Should().ThrowAsync<ValidationException>();
	}

	[Fact]
	public async Task InsertEmailInvitationRequest_ShouldThrowValidationException_WhenEmailIsInvalid()
	{
		var dto = new EmailInvitationRequestDTO
		{
			FirstName = "Integration",
			LastName = "Tester",
			EmailAddress = "invalid-email",
			MobileNumber = "+639171234567",
			SelectPackage = "Standard",
			RushNormal = "Normal"
		};

		var command = new EmailInvitationRequestCommand(dto);

		Func<Task> act = async () => await _sender.Send(command);

		await act.Should().ThrowAsync<ValidationException>();
	}

	[Fact]
	public async Task InsertEmailInvitationRequest_ShouldThrow_WhenEmailIsMissing()
	{
		// Arrange
		var dto = new EmailInvitationRequestDTO
		{
			FirstName = "Integration",
			LastName = "Tester",
			EmailAddress = "",
			MobileNumber = "+123456789",
			SelectPackage = "Standard",
			RushNormal = "Normal"
		};

		var command = new EmailInvitationRequestCommand(dto);

		// Act
		Func<Task> act = async () => await _sender.Send(command);

		// Assert
		await act.Should().ThrowAsync<ValidationException>();
	}

	[Fact]
	public async Task InsertEmailInvitationRequest_ShouldThrowValidationException_WhenMobileNumberIsEmpty()
	{
		var dto = new EmailInvitationRequestDTO
		{
			FirstName = "Integration",
			LastName = "Tester",
			EmailAddress = "integration.tester@example.com",
			MobileNumber = "",
			SelectPackage = "Standard",
			RushNormal = "Normal"
		};

		var command = new EmailInvitationRequestCommand(dto);

		Func<Task> act = async () => await _sender.Send(command);

		await act.Should().ThrowAsync<ValidationException>();
	}

	[Fact]
	public async Task InsertEmailInvitationRequest_ShouldThrow_WhenDtoIsNull()
	{
		// Arrange
		var command = new EmailInvitationRequestCommand(null!);

		// Act
		Func<Task> act = async () => await _sender.Send(command);

		// Assert
		await act.Should().ThrowAsync<NullReferenceException>();
	}

	#endregion
}
