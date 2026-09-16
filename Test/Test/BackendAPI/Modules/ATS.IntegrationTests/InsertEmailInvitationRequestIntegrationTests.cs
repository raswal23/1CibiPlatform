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

		// The sent-status update is an ExecuteUpdateAsync, which writes straight to the
		// database and leaves the tracked instance behind. Read fresh or this asserts
		// against the pre-send snapshot.
		_dbContext.ChangeTracker.Clear();

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

		// Manual screening sends the application form inline, so the order is already
		// marked sent by the time the transaction commits.
		persisted.EmailSentStatus.Should().Be("Done");
		persisted.EmailSentAt.Should().NotBeNull();
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

	[Fact]
	public async Task InsertEmailInvitationRequest_ShouldNotSendTheApplicationForm_ForDataScreening()
	{
		// The candidate's identity was captured at order entry, so there is no form to
		// ask them to fill in. This used to email one anyway: the send was inline and
		// unconditional, and the order came out of the transaction marked "Done".
		var package = await SeedAssignedPackageAsync("No Email Data Package", autoChasing: false);

		var dto = new EmailInvitationRequestDTO
		{
			FirstName = "Silent",
			LastName = "Tester",
			EmailAddress = "silent.tester@example.com",
			MobileNumber = "09171234567",
			SelectPackage = package,
			RushNormal = "Normal",
			AutoChasing = false,
			DateOfBirth = new DateOnly(1988, 6, 2),
			SSSNumber = "0123456789",
			TINNumber = "123456789012"
		};

		// Act
		var result = await _sender.Send(new EmailInvitationRequestCommand(dto));

		// Assert
		result.isAdded.Should().BeTrue();

		// Read past the change tracker: the assertion is that the column itself holds
		// NULL, which it could not before this feature made it nullable.
		_dbContext.ChangeTracker.Clear();

		var persisted = _dbContext.EmailInvitationRequests
			.SingleOrDefault(x => x.EmailAddress == dto.EmailAddress);
		persisted.Should().NotBeNull();

		// Every email column stays unset. Null rather than Pending: the order holds no
		// position in the queue the email worker claims from.
		persisted!.EmailSentStatus.Should().BeNull();
		persisted.EmailSentAt.Should().BeNull();
		persisted.EmailClaimedAt.Should().BeNull();
		persisted.EmailSendAttempts.Should().Be(0);

		// The order itself is unaffected - only the candidate-facing email is suppressed.
		persisted.OrderStatus.Should().Be("Pending Candidate Info");
		persisted.ApplicationFormStatus.Should().Be("Pending");

		// Still queued for OMS ticketing, which has no screening-type filter.
		persisted.TicketStatus.Should().Be("Pending");
		persisted.IsTicketed.Should().BeFalse();
	}

	[Fact]
	public async Task InsertEmailInvitationRequest_ShouldLeaveADataOrderUnclaimableByTheEmailWorker()
	{
		// Belt and braces on the rule above: even if a data order somehow reached the
		// queue, the worker's "AutoChasing" IS TRUE claim must step over it.
		var package = await SeedAssignedPackageAsync("Unclaimable Data Package", autoChasing: false);

		var dto = new EmailInvitationRequestDTO
		{
			FirstName = "Unclaimed",
			LastName = "Tester",
			EmailAddress = "unclaimed.tester@example.com",
			MobileNumber = "09171234567",
			SelectPackage = package,
			RushNormal = "Normal",
			AutoChasing = false,
			DateOfBirth = new DateOnly(1991, 4, 9),
			SSSNumber = "0123456789",
			TINNumber = "123456789012"
		};

		await _sender.Send(new EmailInvitationRequestCommand(dto));

		// Act
		var claimed = await _atsRepository.GetPendingEmailInvitationRequestsAsync();

		// Assert
		claimed.Should().NotContain(invitation => invitation.EmailAddress == dto.EmailAddress);
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
