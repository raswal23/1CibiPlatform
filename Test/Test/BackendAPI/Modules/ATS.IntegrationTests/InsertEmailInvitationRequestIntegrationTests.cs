using ATS.DTO;
using ATS.Features.Web.EmailInvitationRequest;
using BuildingBlocks.Exceptions;
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
		// The package must be assigned to the caller's client, or the order is rejected.
		// Manual screening: the identity fields stay optional.
		var package = await SeedAssignedPackageAsync("Manual Screening Package", autoChasing: true);

		var dto = new EmailInvitationRequestDTO
		{
			FirstName = "Integration",
			LastName = "Tester",
			MiddleInitial = "A",
			EmailAddress = "integration.tester@example.com",
			MobileNumber = "09171234567",
			SelectPackage = package,
			RushNormal = "Normal",
			AutoChasing = true
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
		persisted.AutoChasing.Should().BeTrue();
		persisted.DateOfBirth.Should().BeNull();
		persisted.SSSNumber.Should().BeNull();
		persisted.TINNumber.Should().BeNull();
	}

	[Fact]
	public async Task InsertEmailInvitationRequest_ShouldPersistIdentityFields_ForDataScreening()
	{
		// Arrange
		var package = await SeedAssignedPackageAsync("Data Screening Package", autoChasing: false);

		var dto = new EmailInvitationRequestDTO
		{
			FirstName = "Data",
			LastName = "Tester",
			EmailAddress = "data.tester@example.com",
			MobileNumber = "09171234567",
			SelectPackage = package,
			RushNormal = "Normal",
			AutoChasing = false,
			DateOfBirth = new DateOnly(1990, 1, 15),
			SSSNumber = "0123456789",
			TINNumber = "123456789012"
		};

		var command = new EmailInvitationRequestCommand(dto);

		// Act
		var result = await _sender.Send(command);

		// Assert
		result.isAdded.Should().BeTrue();

		var persisted = _dbContext.EmailInvitationRequests
			.SingleOrDefault(x => x.EmailAddress == dto.EmailAddress);
		persisted.Should().NotBeNull();
		persisted!.AutoChasing.Should().BeFalse();
		persisted.DateOfBirth.Should().Be(new DateOnly(1990, 1, 15));
		persisted.SSSNumber.Should().Be("0123456789");
		persisted.TINNumber.Should().Be("123456789012");
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

	[Fact]
	public async Task InsertEmailInvitationRequest_ShouldThrowValidationException_WhenScreeningTypeIsMissing()
	{
		var dto = new EmailInvitationRequestDTO
		{
			FirstName = "Integration",
			LastName = "Tester",
			EmailAddress = "no.screening.type@example.com",
			MobileNumber = "09171234567",
			SelectPackage = "Standard",
			RushNormal = "Normal",
			AutoChasing = null
		};

		var command = new EmailInvitationRequestCommand(dto);

		Func<Task> act = async () => await _sender.Send(command);

		await act.Should().ThrowAsync<ValidationException>();
	}

	[Fact]
	public async Task InsertEmailInvitationRequest_ShouldThrowValidationException_WhenDataScreeningIdentityFieldsAreMissing()
	{
		var dto = new EmailInvitationRequestDTO
		{
			FirstName = "Data",
			LastName = "Tester",
			EmailAddress = "data.missing@example.com",
			MobileNumber = "09171234567",
			SelectPackage = "Standard",
			RushNormal = "Normal",
			AutoChasing = false
			// DateOfBirth, SSSNumber and TINNumber deliberately absent.
		};

		var command = new EmailInvitationRequestCommand(dto);

		Func<Task> act = async () => await _sender.Send(command);

		await act.Should().ThrowAsync<ValidationException>();
	}

	[Fact]
	public async Task InsertEmailInvitationRequest_ShouldThrowBadRequest_WhenScreeningTypeMismatchesThePackage()
	{
		// The package is classified as manual, but the caller claims data screening.
		var package = await SeedAssignedPackageAsync("Mismatch Screening Package", autoChasing: true);

		var dto = new EmailInvitationRequestDTO
		{
			FirstName = "Mismatch",
			LastName = "Tester",
			EmailAddress = "mismatch.tester@example.com",
			MobileNumber = "09171234567",
			SelectPackage = package,
			RushNormal = "Normal",
			AutoChasing = false,
			DateOfBirth = new DateOnly(1990, 1, 15),
			SSSNumber = "0123456789",
			TINNumber = "123456789"
		};

		var command = new EmailInvitationRequestCommand(dto);

		Func<Task> act = async () => await _sender.Send(command);

		await act.Should()
			.ThrowAsync<BadRequestException>()
			.WithMessage("The selected package does not match the chosen screening type.");
	}

	#endregion
}
