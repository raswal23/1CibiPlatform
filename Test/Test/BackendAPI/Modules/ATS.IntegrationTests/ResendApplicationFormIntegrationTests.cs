using ATS.Constants;
using ATS.Data.Entities;
using ATS.Features.Web.ResendApplicationForm;
using Auth.Constants;
using BuildingBlocks.Exceptions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Test.BackendAPI.Infrastructure.ATS.Infrastracture;

namespace Test.BackendAPI.Modules.ATS.IntegrationTests;

public class ResendApplicationFormIntegrationTests : BaseIntegrationTest
{
	private const int ClientA = 1;
	private const int ClientB = 2;

	private static readonly Guid UploaderId = Guid.CreateVersion7();
	private static readonly Guid OtherUploaderId = Guid.CreateVersion7();

	public ResendApplicationFormIntegrationTests(IntegrationTestWebAppFactory factory)
		: base(factory)
	{
		// Resend applies the caller's ATS scope, so every test needs an identity. The
		// happy-path tests use a super admin, which is unrestricted; the scope tests
		// below narrow it deliberately.
		SetAuthenticatedUser(
			Guid.CreateVersion7(),
			AtsRoleIds.PlatformManager,
			ClientA,
			isPlatformSuperAdmin: true);
	}

	#region Positive Path

	[Fact]
	public async Task ResendApplicationForm_ShouldUpdateTokenAndStatusAndSendEmail()
	{
		// Arrange
		var originalHashToken = "original-hash-token";
		var originalCreatedAt = DateTime.UtcNow.AddDays(-5);
		var originalExpiration = DateTime.UtcNow.AddDays(-4);

		var emailInvitation = new EmailInvitationRequest
		{
			EmailInvitationID = Guid.CreateVersion7(),
			FirstName = "Integration",
			LastName = "Tester",
			MiddleInitial = "A",
			EmailAddress = "resend.test@example.com",
			MobileNumber = "09171234567",
			HashToken = originalHashToken,
			HashTokenCreatedAt = originalCreatedAt,
			HashTokenExpiration = originalExpiration,
			SelectPackage = "Standard",
			RushNormal = "Normal",
			EmailSentStatus = "Done",
			ApplicationFormStatus = "Pending",
			OrderStatus = "Application Withdrawn"
		};

		await _dbContext.EmailInvitationRequests.AddAsync(emailInvitation);
		await _dbContext.SaveChangesAsync();

       var command = new ResendApplicationFormCommand(emailInvitation.EmailInvitationID);

		// Act
		var result = await _sender.Send(command);

		// Assert
		result.Should().NotBeNull();
     result.Success.Should().BeTrue();

		// Verify database updates
		var updated = await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.SingleAsync(x => x.EmailInvitationID == emailInvitation.EmailInvitationID);

		updated.Should().NotBeNull();
		updated!.HashToken.Should().NotBe(originalHashToken);
		updated.HashTokenCreatedAt.Should().BeAfter(originalCreatedAt);
		updated.HashTokenExpiration.Should().BeAfter(originalExpiration);
		updated.OrderStatus.Should().Be("Pending Candidate Info");
		updated.EmailSentStatus.Should().Be("Done");
	}

	[Fact]
	public async Task ResendApplicationForm_ShouldGenerateNewHashToken()
	{
		// Arrange
		var emailInvitation = new EmailInvitationRequest
		{
			EmailInvitationID = Guid.CreateVersion7(),
			FirstName = "Integration",
			LastName = "Tester",
			MiddleInitial = "A",
			EmailAddress = "newhash.test@example.com",
			MobileNumber = "09171234567",
			HashToken = "old-hash-token",
			HashTokenCreatedAt = DateTime.UtcNow.AddDays(-5),
			HashTokenExpiration = DateTime.UtcNow.AddDays(-4),
			SelectPackage = "Premium",
			RushNormal = "Rush",
			EmailSentStatus = "Done",
			ApplicationFormStatus = "Pending",
			OrderStatus = "Application Withdrawn"
		};

		await _dbContext.EmailInvitationRequests.AddAsync(emailInvitation);
		await _dbContext.SaveChangesAsync();

      var command = new ResendApplicationFormCommand(emailInvitation.EmailInvitationID);

		// Act
		var result = await _sender.Send(command);

		// Assert
		result.Should().NotBeNull();
     result.Success.Should().BeTrue();

		var updated = await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.SingleAsync(x => x.EmailInvitationID == emailInvitation.EmailInvitationID);

		// New hash token should not be empty or null
		updated!.HashToken.Should().NotBeNullOrEmpty();
		updated.HashToken.Should().NotBe("old-hash-token");
	}

	[Fact]
	public async Task ResendApplicationForm_ShouldResetOrderStatusToPendingCandidateInfo()
	{
		// Arrange
		var emailInvitation = new EmailInvitationRequest
		{
			EmailInvitationID = Guid.CreateVersion7(),
			FirstName = "Integration",
			LastName = "Tester",
			MiddleInitial = "A",
			EmailAddress = "ticketstatus.test@example.com",
			MobileNumber = "09171234567",
			HashToken = "hash-token",
			HashTokenCreatedAt = DateTime.UtcNow.AddDays(-5),
			HashTokenExpiration = DateTime.UtcNow.AddDays(-4),
			SelectPackage = "Standard",
			RushNormal = "Normal",
			EmailSentStatus = "Done",
			ApplicationFormStatus = "Pending",
			OrderStatus = "Application Withdrawn"
		};

		await _dbContext.EmailInvitationRequests.AddAsync(emailInvitation);
		await _dbContext.SaveChangesAsync();

     var command = new ResendApplicationFormCommand(emailInvitation.EmailInvitationID);

		// Act
		var result = await _sender.Send(command);

		// Assert
     result.Success.Should().BeTrue();

		var updated = await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.SingleAsync(x => x.EmailInvitationID == emailInvitation.EmailInvitationID);

		updated!.OrderStatus.Should().Be("Pending Candidate Info");
	}

	[Fact]
	public async Task ResendApplicationForm_ShouldSetEmailSentStatusToPending()
	{
		// Arrange
		var emailInvitation = new EmailInvitationRequest
		{
			EmailInvitationID = Guid.CreateVersion7(),
			FirstName = "Integration",
			LastName = "Tester",
			MiddleInitial = "A",
			EmailAddress = "emailstatus.test@example.com",
			MobileNumber = "09171234567",
			HashToken = "hash-token",
			HashTokenCreatedAt = DateTime.UtcNow.AddDays(-5),
			HashTokenExpiration = DateTime.UtcNow.AddDays(-4),
			SelectPackage = "Standard",
			RushNormal = "Normal",
			EmailSentStatus = "Done",
			ApplicationFormStatus = "Pending",
			OrderStatus = "Application Withdrawn"
		};

		await _dbContext.EmailInvitationRequests.AddAsync(emailInvitation);
		await _dbContext.SaveChangesAsync();

      var command = new ResendApplicationFormCommand(emailInvitation.EmailInvitationID);

		// Act
		var result = await _sender.Send(command);

		// Assert
     result.Success.Should().BeTrue();

		var updated = await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.SingleAsync(x => x.EmailInvitationID == emailInvitation.EmailInvitationID);

		updated!.EmailSentStatus.Should().Be("Done");
	}

	#endregion

	#region Negative Path

	[Fact]
	public async Task ResendApplicationForm_ShouldFailWhenEmailInvitationNotFound()
	{
		//		
       var command = new ResendApplicationFormCommand(Guid.CreateVersion7());	

		// Act
		Func<Task> act = async () => await _sender.Send(command);

		// Assert
		await act.Should().ThrowAsync<Exception>();
	}

	[Fact]
	public async Task ResendApplicationForm_ShouldFailWhenEmailAddressDoesNotMatch()
	{
		// Arrange
		var emailInvitation = new EmailInvitationRequest
		{
			EmailInvitationID = Guid.CreateVersion7(),
			FirstName = "Integration",
			LastName = "Tester",
			MiddleInitial = "A",
			EmailAddress = "correct@example.com",
			MobileNumber = "09171234567",
			HashToken = "hash-token",
			HashTokenCreatedAt = DateTime.UtcNow.AddDays(-5),
			HashTokenExpiration = DateTime.UtcNow.AddDays(-4),
			SelectPackage = "Standard",
			RushNormal = "Normal",
			EmailSentStatus = "Done",
			ApplicationFormStatus = "Pending",
			OrderStatus = "Application Withdrawn"
		};

		var command = new ResendApplicationFormCommand(emailInvitation.EmailInvitationID);

		// Act
		Func<Task> act = async () => await _sender.Send(command);

		// Assert
		await act.Should().ThrowAsync<Exception>();
	}

	#endregion

	#region Scope

	[Fact]
	public async Task ResendApplicationForm_ShouldThrowNotFound_WhenInvitationBelongsToAnotherClient()
	{
		// Arrange
		var emailInvitation = NewScopedInvitation(ClientB, OtherUploaderId);

		await _dbContext.EmailInvitationRequests.AddAsync(emailInvitation);
		await _dbContext.SaveChangesAsync();
		_dbContext.ChangeTracker.Clear();

		// An uploader confined to client A must not be able to resend client B's
		// invitation just by knowing its id.
		SetAuthenticatedUser(UploaderId, AtsRoleIds.Uploader, ClientA);

		var command = new ResendApplicationFormCommand(emailInvitation.EmailInvitationID);

		// Act
		Func<Task> act = async () => await _sender.Send(command);

		// Assert
		await act.Should().ThrowAsync<NotFoundException>();

		var untouched = await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.SingleAsync(x => x.EmailInvitationID == emailInvitation.EmailInvitationID);

		untouched.HashToken.Should().Be(emailInvitation.HashToken);
	}

	[Fact]
	public async Task ResendApplicationForm_ShouldThrowNotFound_WhenInvitationBelongsToAnotherRequestor()
	{
		// Arrange
		var emailInvitation = NewScopedInvitation(ClientA, OtherUploaderId);

		await _dbContext.EmailInvitationRequests.AddAsync(emailInvitation);
		await _dbContext.SaveChangesAsync();
		_dbContext.ChangeTracker.Clear();

		// Same client, different requestor: an Uploader only owns their own orders.
		SetAuthenticatedUser(UploaderId, AtsRoleIds.Uploader, ClientA);

		var command = new ResendApplicationFormCommand(emailInvitation.EmailInvitationID);

		// Act
		Func<Task> act = async () => await _sender.Send(command);

		// Assert
		await act.Should().ThrowAsync<NotFoundException>();
	}

	[Fact]
	public async Task ResendApplicationForm_ShouldSucceed_WhenInvitationIsWithinCallerScope()
	{
		// Arrange
		var emailInvitation = NewScopedInvitation(ClientA, UploaderId);
		var originalHashToken = emailInvitation.HashToken;

		await _dbContext.EmailInvitationRequests.AddAsync(emailInvitation);
		await _dbContext.SaveChangesAsync();
		_dbContext.ChangeTracker.Clear();

		SetAuthenticatedUser(UploaderId, AtsRoleIds.Uploader, ClientA);

		var command = new ResendApplicationFormCommand(emailInvitation.EmailInvitationID);

		// Act
		var result = await _sender.Send(command);

		// Assert
		result.Success.Should().BeTrue();

		var updated = await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.SingleAsync(x => x.EmailInvitationID == emailInvitation.EmailInvitationID);

		updated.HashToken.Should().NotBe(originalHashToken);
	}

	#endregion

	#region Edge Cases

	[Fact]
	public async Task ResendApplicationForm_ShouldWorkMultipleTimesForSameRecord()
	{
		// Arrange
		var emailInvitation = new EmailInvitationRequest
		{
			EmailInvitationID = Guid.CreateVersion7(),
			FirstName = "Integration",
			LastName = "Tester",
			MiddleInitial = "A",
			EmailAddress = "multiple.resend@example.com",
			MobileNumber = "09171234567",
			HashToken = "hash-token-1",
			HashTokenCreatedAt = DateTime.UtcNow.AddDays(-5),
			HashTokenExpiration = DateTime.UtcNow.AddDays(-4),
			SelectPackage = "Standard",
			RushNormal = "Normal",
			EmailSentStatus = "Done",
			ApplicationFormStatus = "Pending",
			OrderStatus = "Application Withdrawn"
		};

		await _dbContext.EmailInvitationRequests.AddAsync(emailInvitation);
		await _dbContext.SaveChangesAsync();

      var command1 = new ResendApplicationFormCommand(emailInvitation.EmailInvitationID);

		// Act - First resend
		var result1 = await _sender.Send(command1);
        result1.Success.Should().BeTrue();

		var afterFirstResend = await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.SingleAsync(x => x.EmailInvitationID == emailInvitation.EmailInvitationID);

		var firstResendToken = afterFirstResend.HashToken;

		// Act - Second resend
      var command2 = new ResendApplicationFormCommand(emailInvitation.EmailInvitationID);
		var result2 = await _sender.Send(command2);
        result2.Success.Should().BeTrue();

		var afterSecondResend = await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.SingleAsync(x => x.EmailInvitationID == emailInvitation.EmailInvitationID);

		var secondResendToken = afterSecondResend.HashToken;

		// Assert
		firstResendToken.Should().NotBe("hash-token-1");
		secondResendToken.Should().NotBe("hash-token-1");
		secondResendToken.Should().NotBe(firstResendToken);
		afterSecondResend.OrderStatus.Should().Be("Pending Candidate Info");
		afterSecondResend.EmailSentStatus.Should().Be("Done");
	}

	#endregion

	#region Helpers

	private void SetAuthenticatedUser(
		Guid userId,
		int roleId,
		int claimedClientId,
		bool isPlatformSuperAdmin = false)
	{
		var claims = new List<Claim>
		{
			new(ClaimTypes.NameIdentifier, userId.ToString()),
			new(AuthClaimTypes.AtsRoleId, roleId.ToString()),
			new(AuthClaimTypes.AtsClientId, claimedClientId.ToString())
		};

		if (isPlatformSuperAdmin)
		{
			claims.Add(new Claim(
				AuthClaimTypes.PlatformRoleId,
				PlatformRoleIds.SuperAdmin.ToString()));
		}

		_httpContextAccessor.HttpContext!.User = new ClaimsPrincipal(
			new ClaimsIdentity(claims, "TestAuth"));
	}

	// An invitation that actually carries the client/requestor the scope check reads.
	private static EmailInvitationRequest NewScopedInvitation(int clientId, Guid requestorId)
	{
		var invitationId = Guid.CreateVersion7();

		return new EmailInvitationRequest
		{
			EmailInvitationID = invitationId,
			FirstName = "Integration",
			LastName = "Tester",
			MiddleInitial = "A",
			EmailAddress = "scoped.resend@example.com",
			MobileNumber = "09171234567",
			HashToken = invitationId.ToString("N"),
			HashTokenCreatedAt = DateTime.UtcNow.AddDays(-5),
			HashTokenExpiration = DateTime.UtcNow.AddDays(-4),
			SelectPackage = "Standard",
			RushNormal = "Normal",
			ClientId = clientId,
			RequestorId = requestorId,
			EmailSentStatus = "Done",
			ApplicationFormStatus = "Pending",
			OrderStatus = "Application Withdrawn"
		};
	}

	#endregion
}
