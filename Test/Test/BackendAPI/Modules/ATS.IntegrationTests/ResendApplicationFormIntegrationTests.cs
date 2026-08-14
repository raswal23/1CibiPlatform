using System.Security.Claims;
using ATS.Constants;
using ATS.Data.Entities;
using Auth.Constants;
using BuildingBlocks.Exceptions;
using ATS.Features.ResendApplicationForm;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Test.BackendAPI.Infrastructure.ATS.Infrastracture;

namespace Test.BackendAPI.Modules.ATS.IntegrationTests;

public class ResendApplicationFormIntegrationTests : BaseIntegrationTest
{
	private readonly Guid _currentUserId = Guid.CreateVersion7();
	public ResendApplicationFormIntegrationTests(IntegrationTestWebAppFactory factory)
		: base(factory)
	{
		SetDefaultUserScope();
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
			RequestorId = _currentUserId,
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
			RequestorId = _currentUserId,
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
			RequestorId = _currentUserId,
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
			RequestorId = _currentUserId,
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
			RequestorId = _currentUserId,
			OrderStatus = "Application Withdrawn"
		};

		var command = new ResendApplicationFormCommand(emailInvitation.EmailInvitationID);

		// Act
		Func<Task> act = async () => await _sender.Send(command);

		// Assert
		await act.Should().ThrowAsync<Exception>();
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
			RequestorId = _currentUserId,
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

	[Fact]
	public async Task ResendApplicationForm_ShouldRejectInvitationOutsideAuthenticatedScope()
	{
		var invitation = new EmailInvitationRequest
		{
			EmailInvitationID = Guid.CreateVersion7(),
			FirstName = "Unauthorized",
			LastName = "Candidate",
			EmailAddress = "unauthorized@example.com",
			MobileNumber = "09171234567",
			HashToken = "unauthorized-token",
			HashTokenCreatedAt = DateTime.UtcNow.AddDays(-5),
			HashTokenExpiration = DateTime.UtcNow.AddDays(-4),
			SelectPackage = "Standard",
			RushNormal = "Normal",
			EmailSentStatus = "Done",
			ApplicationFormStatus = "Withdrawn",
			OrderStatus = "Application Withdrawn",
			ClientId = 2,
			RequestorId = Guid.CreateVersion7()
		};
		await _dbContext.EmailInvitationRequests.AddAsync(invitation);
		await _dbContext.SaveChangesAsync();
		SetUserScope(Guid.CreateVersion7(), AtsRoleIds.User, clientId: 1);

		Func<Task> act = () => _endorsementSubmissionService.ResendApplicationFormAsync(
			invitation.EmailInvitationID, CancellationToken.None);

		await act.Should().ThrowAsync<ForbiddenException>();
		_dbContext.ChangeTracker.Clear();
		var unchanged = await _dbContext.EmailInvitationRequests.AsNoTracking()
			.SingleAsync(item => item.EmailInvitationID == invitation.EmailInvitationID);
		unchanged.HashToken.Should().Be("unauthorized-token");
		unchanged.OrderStatus.Should().Be("Application Withdrawn");
	}

	private void SetDefaultUserScope()
	{
		_httpContextAccessor.HttpContext!.User = new ClaimsPrincipal(new ClaimsIdentity([
			new Claim(ClaimTypes.NameIdentifier, _currentUserId.ToString()),
			new Claim(AuthClaimTypes.AtsRoleId, AtsRoleIds.User.ToString())
		], "TestAuth"));
	}

	private void SetUserScope(Guid userId, int roleId, int? clientId)
	{
		var claims = new List<Claim>
		{
			new(ClaimTypes.NameIdentifier, userId.ToString()),
			new(AuthClaimTypes.AtsRoleId, roleId.ToString())
		};
		if (clientId.HasValue)
			claims.Add(new Claim(AuthClaimTypes.AtsClientId, clientId.Value.ToString()));
		_httpContextAccessor.HttpContext!.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
	}

	#endregion
}
