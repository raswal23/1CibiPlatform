using ATS.Constants;
using ATS.Data.Entities;
using ATS.Features.Web.ResendApplicationForm;
using ATS.Features.Web.ResendApplicationForms;
using ATS.Services.EndorsementSubmission;
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
			PackageId = DefaultPackageId,
			SelectPackage = "Standard",
			RushNormal = "Normal",
			// Manual screening: the only type that has an application form to resend.
			AutoChasing = true,
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
		updated.OrderStatus.Should().Be("Pending Candidate Info");

		// Queued, not sent inline. The row goes back on the email job's queue so the send
		// runs through the pooled, rate-limited path like any other invitation.
		//
		// This assertion previously expected "Done" - which was the bug. Resend left the
		// email status untouched, so a successful retry still displayed as its previous
		// state, and a row that had exhausted its attempts was never picked up again.
		updated.EmailSentStatus.Should().Be("Pending");

		// The attempt budget resets, otherwise the claim query skips the row and the
		// resend silently does nothing.
		updated.EmailSendAttempts.Should().Be(0);
		updated.EmailClaimedAt.Should().BeNull();
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
			PackageId = DefaultPackageId,
			SelectPackage = "Premium",
			RushNormal = "Rush",
			AutoChasing = true,
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
			PackageId = DefaultPackageId,
			SelectPackage = "Standard",
			RushNormal = "Normal",
			AutoChasing = true,
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
			PackageId = DefaultPackageId,
			SelectPackage = "Standard",
			RushNormal = "Normal",
			AutoChasing = true,
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

		updated!.EmailSentStatus.Should().Be("Pending");
	}

	[Fact]
	public async Task ResendApplicationForm_ShouldResetAnExhaustedAttemptBudget()
	{
		// Arrange: the reported bug. An invitation that used up its retries sat at
		// Error/5, and because the claim query only re-claims Error rows while attempts
		// are under the ceiling, resending it changed nothing that made it send again.
		var emailInvitation = new EmailInvitationRequest
		{
			EmailInvitationID = Guid.CreateVersion7(),
			FirstName = "Exhausted",
			LastName = "Tester",
			MiddleInitial = "A",
			EmailAddress = "exhausted.retry@example.com",
			MobileNumber = "09171234567",
			HashToken = "exhausted-hash-token",
			HashTokenCreatedAt = DateTime.UtcNow.AddDays(-5),
			HashTokenExpiration = DateTime.UtcNow.AddDays(-4),
			PackageId = DefaultPackageId,
			SelectPackage = "Standard",
			RushNormal = "Normal",
			// Manual screening: the only type that has an application form to resend.
			AutoChasing = true,
			EmailSentStatus = "Error",
			EmailSendAttempts = 5,
			ApplicationFormStatus = "Pending",
			OrderStatus = "Pending Candidate Info"
		};

		await _dbContext.EmailInvitationRequests.AddAsync(emailInvitation);
		await _dbContext.SaveChangesAsync();
		_dbContext.ChangeTracker.Clear();

		var command = new ResendApplicationFormCommand(emailInvitation.EmailInvitationID);

		// Act
		var result = await _sender.Send(command);

		// Assert
		result.Success.Should().BeTrue();

		var updated = await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.SingleAsync(x => x.EmailInvitationID == emailInvitation.EmailInvitationID);

		// Both halves matter: Pending alone would still be skipped if the count stayed at
		// the ceiling, and a reset count alone would leave the row reading "Error".
		updated.EmailSentStatus.Should().Be("Pending");
		updated.EmailSendAttempts.Should().Be(0);
		updated.HashToken.Should().NotBe("exhausted-hash-token");
	}

	[Fact]
	public async Task ResendApplicationForm_ShouldMakeTheInvitationClaimableAgain()
	{
		// Arrange: proves the end-to-end effect of the reset - the email job can actually
		// pick the row up. Asserting the status alone would not catch a future change that
		// leaves the row in a state the claim query filters out.
		var emailInvitation = new EmailInvitationRequest
		{
			EmailInvitationID = Guid.CreateVersion7(),
			FirstName = "Claimable",
			LastName = "Tester",
			MiddleInitial = "A",
			EmailAddress = "claimable.retry@example.com",
			MobileNumber = "09171234567",
			HashToken = "claimable-hash-token",
			HashTokenCreatedAt = DateTime.UtcNow.AddDays(-5),
			HashTokenExpiration = DateTime.UtcNow.AddDays(-4),
			PackageId = DefaultPackageId,
			SelectPackage = "Standard",
			RushNormal = "Normal",
			// Manual screening: the only type that has an application form to resend.
			AutoChasing = true,
			EmailSentStatus = "Error",
			EmailSendAttempts = 5,
			ApplicationFormStatus = "Pending",
			OrderStatus = "Pending Candidate Info",
			OrderCreatedAt = DateTime.UtcNow
		};

		await _dbContext.EmailInvitationRequests.AddAsync(emailInvitation);
		await _dbContext.SaveChangesAsync();
		_dbContext.ChangeTracker.Clear();

		await _sender.Send(new ResendApplicationFormCommand(emailInvitation.EmailInvitationID));

		// Act
		var claimed = await _atsRepository.GetPendingEmailInvitationRequestsAsync();

		// Assert
		claimed.Should().Contain(x => x.EmailInvitationID == emailInvitation.EmailInvitationID);
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
			PackageId = DefaultPackageId,
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

	[Fact]
	public async Task ResendApplicationForm_ShouldThrowBadRequest_WhenOrderUsesDataScreening()
	{
		// Arrange
		// A data order is never emailed, so there is nothing to resend - and resending
		// would deliver the very application form this screening type exists to avoid.
		// The dialog hides the button, but the endpoint takes a caller-supplied id.
		var emailInvitation = new EmailInvitationRequest
		{
			EmailInvitationID = Guid.CreateVersion7(),
			FirstName = "Data",
			LastName = "Tester",
			EmailAddress = "data.resend@example.com",
			MobileNumber = "09171234567",
			HashToken = "data-hash-token",
			HashTokenCreatedAt = DateTime.UtcNow.AddDays(-5),
			HashTokenExpiration = DateTime.UtcNow.AddDays(-4),
			PackageId = DefaultPackageId,
			SelectPackage = "Standard",
			RushNormal = "Normal",
			AutoChasing = false,
			// Exactly how the create path leaves a data order: no email state at all.
			EmailSentStatus = null,
			ApplicationFormStatus = "Pending",
			OrderStatus = "Pending Candidate Info"
		};

		await _dbContext.EmailInvitationRequests.AddAsync(emailInvitation);
		await _dbContext.SaveChangesAsync();
		_dbContext.ChangeTracker.Clear();

		var command = new ResendApplicationFormCommand(emailInvitation.EmailInvitationID);

		// Act
		Func<Task> act = async () => await _sender.Send(command);

		// Assert
		await act.Should()
			.ThrowAsync<BadRequestException>()
			.WithMessage("This order does not use manual screening, so no application form is sent to the candidate.");

		// The rejection must leave no trace: no reissued token, and no email queued.
		var untouched = await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.SingleAsync(x => x.EmailInvitationID == emailInvitation.EmailInvitationID);

		untouched.HashToken.Should().Be("data-hash-token");
		untouched.EmailSentStatus.Should().BeNull();
	}

	[Fact]
	public async Task ResendApplicationForm_ShouldThrowBadRequest_WhenScreeningTypeIsUnknown()
	{
		// Arrange
		// Null is neither Manual nor Data, and an unclassified order cannot prove it is
		// manual - the same reasoning the email worker's "AutoChasing" IS TRUE claim uses.
		var emailInvitation = new EmailInvitationRequest
		{
			EmailInvitationID = Guid.CreateVersion7(),
			FirstName = "Unclassified",
			LastName = "Tester",
			EmailAddress = "unclassified.resend@example.com",
			MobileNumber = "09171234567",
			HashToken = "unclassified-hash-token",
			HashTokenCreatedAt = DateTime.UtcNow.AddDays(-5),
			HashTokenExpiration = DateTime.UtcNow.AddDays(-4),
			PackageId = DefaultPackageId,
			SelectPackage = "Standard",
			RushNormal = "Normal",
			AutoChasing = null,
			EmailSentStatus = "Done",
			ApplicationFormStatus = "Pending",
			OrderStatus = "Application Withdrawn"
		};

		await _dbContext.EmailInvitationRequests.AddAsync(emailInvitation);
		await _dbContext.SaveChangesAsync();
		_dbContext.ChangeTracker.Clear();

		var command = new ResendApplicationFormCommand(emailInvitation.EmailInvitationID);

		// Act
		Func<Task> act = async () => await _sender.Send(command);

		// Assert
		await act.Should().ThrowAsync<BadRequestException>();
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
			PackageId = DefaultPackageId,
			SelectPackage = "Standard",
			RushNormal = "Normal",
			AutoChasing = true,
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
		afterSecondResend.EmailSentStatus.Should().Be("Pending");
	}

	[Fact]
	public async Task ResendApplicationForm_ShouldThrowConflict_WhenTheInvitationIsMidSend()
	{
		// Arrange: Processing means a worker is holding this row in memory right now and is
		// about to write its outcome. Re-issuing the token would race that write, and the
		// message may already be on its way - so this is the one state a resend is refused
		// in. Pending is deliberately allowed: nothing has been sent yet, so re-queueing
		// duplicates nothing.
		var emailInvitation = new EmailInvitationRequest
		{
			EmailInvitationID = Guid.CreateVersion7(),
			FirstName = "InFlight",
			LastName = "Tester",
			MiddleInitial = "A",
			EmailAddress = "inflight.resend@example.com",
			MobileNumber = "09171234567",
			HashToken = "inflight-hash-token",
			HashTokenCreatedAt = DateTime.UtcNow,
			HashTokenExpiration = DateTime.UtcNow.AddDays(1),
			PackageId = DefaultPackageId,
			SelectPackage = "Standard",
			RushNormal = "Normal",
			// Manual screening: the only type that has an application form to resend.
			AutoChasing = true,
			EmailSentStatus = "Processing",
			EmailClaimedAt = DateTime.UtcNow,
			ApplicationFormStatus = "Pending",
			OrderStatus = "Pending Candidate Info"
		};

		await _dbContext.EmailInvitationRequests.AddAsync(emailInvitation);
		await _dbContext.SaveChangesAsync();
		_dbContext.ChangeTracker.Clear();

		var command = new ResendApplicationFormCommand(emailInvitation.EmailInvitationID);

		// Act
		Func<Task> act = async () => await _sender.Send(command);

		// Assert
		await act.Should().ThrowAsync<ConflictException>();

		// The in-flight send keeps the token it is delivering.
		var untouched = await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.SingleAsync(x => x.EmailInvitationID == emailInvitation.EmailInvitationID);

		untouched.HashToken.Should().Be("inflight-hash-token");
	}

	#endregion

	#region Bulk

	[Fact]
	public async Task ResendApplicationForms_ShouldRequeueEveryInvitation_WithItsOwnToken()
	{
		// Arrange
		var first = NewScopedInvitation(ClientA, UploaderId);
		var second = NewScopedInvitation(ClientA, UploaderId);

		first.EmailSentStatus = "Error";
		first.EmailSendAttempts = 5;
		second.EmailSentStatus = "Error";
		second.EmailSendAttempts = 5;

		await _dbContext.EmailInvitationRequests.AddRangeAsync(first, second);
		await _dbContext.SaveChangesAsync();
		_dbContext.ChangeTracker.Clear();

		var command = new ResendApplicationFormsCommand(
			[first.EmailInvitationID, second.EmailInvitationID]);

		// Act
		var result = await _sender.Send(command);

		// Assert
		result.RequestedCount.Should().Be(2);
		result.RequeuedCount.Should().Be(2);
		result.IsComplete.Should().BeTrue();

		var saved = await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.Where(x => x.EmailInvitationID == first.EmailInvitationID
					 || x.EmailInvitationID == second.EmailInvitationID)
			.ToListAsync();

		saved.Should().OnlyContain(x => x.EmailSentStatus == "Pending");
		saved.Should().OnlyContain(x => x.EmailSendAttempts == 0);

		// Each invitation gets its OWN token. A shared one would let either candidate open
		// the other's application form.
		saved.Select(x => x.HashToken).Should().OnlyHaveUniqueItems();
	}

	[Fact]
	public async Task ResendApplicationForms_ShouldSkipInvitationsThatAreMidSend()
	{
		// Arrange: one retryable, one the job is actively sending.
		var retryable = NewScopedInvitation(ClientA, UploaderId);
		var midSend = NewScopedInvitation(ClientA, UploaderId);

		retryable.EmailSentStatus = "Error";
		retryable.EmailSendAttempts = 5;
		midSend.EmailSentStatus = "Processing";
		midSend.EmailClaimedAt = DateTime.UtcNow;

		await _dbContext.EmailInvitationRequests.AddRangeAsync(retryable, midSend);
		await _dbContext.SaveChangesAsync();
		_dbContext.ChangeTracker.Clear();

		var originalMidSendToken = midSend.HashToken;

		var command = new ResendApplicationFormsCommand(
			[retryable.EmailInvitationID, midSend.EmailInvitationID]);

		// Act
		var result = await _sender.Send(command);

		// Assert: a partly-stale selection is not a failure. The rest still move, and the
		// counts tell the operator what happened.
		result.RequestedCount.Should().Be(2);
		result.RequeuedCount.Should().Be(1);
		result.IsComplete.Should().BeFalse();

		var untouched = await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.SingleAsync(x => x.EmailInvitationID == midSend.EmailInvitationID);

		// The in-flight send keeps the token it is delivering.
		untouched.HashToken.Should().Be(originalMidSendToken);
		untouched.EmailSentStatus.Should().Be("Processing");
	}

	[Fact]
	public async Task ResendApplicationForms_ShouldIgnoreInvitationsOutsideTheCallerScope()
	{
		// Arrange: posting another client's id alongside your own must not reach it.
		var mine = NewScopedInvitation(ClientA, UploaderId);
		var theirs = NewScopedInvitation(ClientB, OtherUploaderId);

		mine.EmailSentStatus = "Error";
		mine.EmailSendAttempts = 5;
		theirs.EmailSentStatus = "Error";
		theirs.EmailSendAttempts = 5;

		await _dbContext.EmailInvitationRequests.AddRangeAsync(mine, theirs);
		await _dbContext.SaveChangesAsync();
		_dbContext.ChangeTracker.Clear();

		var theirOriginalToken = theirs.HashToken;

		SetAuthenticatedUser(UploaderId, AtsRoleIds.Uploader, ClientA);

		var command = new ResendApplicationFormsCommand(
			[mine.EmailInvitationID, theirs.EmailInvitationID]);

		// Act
		var result = await _sender.Send(command);

		// Assert: only the caller's own invitation moved.
		result.RequeuedCount.Should().Be(1);

		var untouched = await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.SingleAsync(x => x.EmailInvitationID == theirs.EmailInvitationID);

		untouched.HashToken.Should().Be(theirOriginalToken);
		untouched.EmailSentStatus.Should().Be("Error");
	}

	[Fact]
	public async Task ResendApplicationForms_ShouldThrowNotFound_WhenNothingIsInScope()
	{
		// Arrange
		var theirs = NewScopedInvitation(ClientB, OtherUploaderId);

		theirs.EmailSentStatus = "Error";
		theirs.EmailSendAttempts = 5;

		await _dbContext.EmailInvitationRequests.AddAsync(theirs);
		await _dbContext.SaveChangesAsync();
		_dbContext.ChangeTracker.Clear();

		SetAuthenticatedUser(UploaderId, AtsRoleIds.Uploader, ClientA);

		var command = new ResendApplicationFormsCommand([theirs.EmailInvitationID]);

		// Act
		Func<Task> act = async () => await _sender.Send(command);

		// Assert: not a 403 - naming the invitation would confirm it exists.
		await act.Should().ThrowAsync<NotFoundException>();
	}

	[Fact]
	public async Task ResendApplicationForms_ShouldRejectABatchOverTheLimit()
	{
		// Arrange: every requeued invitation becomes a message on the deliberately-paced
		// email queue, so an unbounded batch would block every other client behind it.
		var tooMany = Enumerable
			.Range(0, EndorsementSubmissionService.MaxBulkResendSize + 1)
			.Select(_ => Guid.CreateVersion7())
			.ToList();

		var command = new ResendApplicationFormsCommand(tooMany);

		// Act
		Func<Task> act = async () => await _sender.Send(command);

		// Assert
		await act.Should().ThrowAsync<Exception>();
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
			PackageId = DefaultPackageId,
			SelectPackage = "Standard",
			RushNormal = "Normal",
			ClientId = clientId,
			RequestorId = requestorId,
			// The scope tests are about who may resend, not about screening type, so
			// these are manual orders: the resend itself must be otherwise allowed for
			// a NotFound to prove the scope check is what rejected it.
			AutoChasing = true,
			EmailSentStatus = "Done",
			ApplicationFormStatus = "Pending",
			OrderStatus = "Application Withdrawn"
		};
	}

	#endregion
}
