using ATS.Data.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Test.BackendAPI.Infrastructure.ATS.Infrastracture;

namespace Test.BackendAPI.Modules.ATS.IntegrationTests;

public class EmailNotificationProcessorIntegrationTests : BaseIntegrationTest
{

	public EmailNotificationProcessorIntegrationTests(IntegrationTestWebAppFactory factory)
		: base(factory)
	{
	}

	private async Task<List<EmailInvitationRequest>> SeedEmailInvitationRequestsAsync(
		int count,
		string emailSentStatus = "Pending",
		DateTime? emailClaimedAt = null)
	{
		var invitations = new List<EmailInvitationRequest>();

		for (int i = 0; i < count; i++)
		{
			var invitation = new EmailInvitationRequest
			{
				EmailInvitationID = Guid.CreateVersion7(),
				FirstName = $"FirstName{i}",
				LastName = $"LastName{i}",
				MiddleInitial = "M",
				EmailAddress = $"test{i}@example.com",
				MobileNumber = $"09123456{i:D3}",
				HashToken = _hashService.Hash($"token-{i}"),
				HashTokenCreatedAt = DateTime.UtcNow,
				HashTokenExpiration = DateTime.UtcNow.AddHours(24),
				SelectPackage = "Standard",
				RushNormal = "Normal",
				EmailSentStatus = emailSentStatus,
				EmailClaimedAt = emailClaimedAt,
				ApplicationFormStatus = "Pending",
				OrderStatus = "Pending Candidate Info",
				OrderCreatedAt = DateTime.UtcNow.AddMinutes(i)
			};

			invitations.Add(invitation);
			await _dbContext.EmailInvitationRequests.AddAsync(invitation);
		}

		await _dbContext.SaveChangesAsync();
		return invitations;
	}

	private async Task<List<EmailInvitationRequest>> ReloadAsync(List<EmailInvitationRequest> seeded)
	{
		var ids = seeded.Select(x => x.EmailInvitationID).ToList();

		return await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.Where(e => ids.Contains(e.EmailInvitationID))
			.ToListAsync();
	}

	#region Positive Path
	[Fact]
	public async Task ProcessForPendingStatusAsync_WithPendingInvitations_ShouldProcessThem()
	{
		// Arrange
		var emailInvitations = await SeedEmailInvitationRequestsAsync(3);

		// Act
		await _emailNotificationProcessorService.ProcessForPendingStatusAsync(CancellationToken.None);

		// Assert - the claim moved every row out of Pending and the send pass settled it
		var processed = await ReloadAsync(emailInvitations);

		processed.Should().HaveCount(3);
		processed.Should().AllSatisfy(e =>
		{
			e.EmailSentStatus.Should().NotBe("Pending");
			e.EmailSentStatus.Should().NotBe("Processing");
		});
	}

	[Fact]
	public async Task ProcessForPendingStatusAsync_WithNoPendingInvitations_ShouldDoNothing()
	{
		// Arrange - nothing seeded; the table is truncated per test
		var initialCount = await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.CountAsync();

		// Act
		Func<Task> act = async () =>
			await _emailNotificationProcessorService.ProcessForPendingStatusAsync(CancellationToken.None);

		// Assert
		await act.Should().NotThrowAsync();

		var finalCount = await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.CountAsync();

		finalCount.Should().Be(initialCount);
	}

	[Fact]
	public async Task ProcessForPendingStatusAsync_WithStaleClaim_ShouldReleaseAndProcessIt()
	{
		// Arrange - a worker crashed mid-send 25 hours ago and left the row claimed
		var stale = await SeedEmailInvitationRequestsAsync(
			1,
			emailSentStatus: "Processing",
			emailClaimedAt: DateTime.UtcNow.AddHours(-25));

		// Act
		await _emailNotificationProcessorService.ProcessForPendingStatusAsync(CancellationToken.None);

		// Assert - released back to Pending, then claimed and settled in the same tick
		var recovered = await ReloadAsync(stale);

		recovered.Should().ContainSingle();
		recovered[0].EmailSentStatus.Should().NotBe("Pending");
		recovered[0].EmailSentStatus.Should().NotBe("Processing");
	}
	#endregion

	#region Negative Path
	[Fact]
	public async Task ProcessForPendingStatusAsync_WithFreshClaim_ShouldLeaveItAlone()
	{
		// Arrange - another worker claimed this row moments ago and is still sending it
		var claimed = await SeedEmailInvitationRequestsAsync(
			1,
			emailSentStatus: "Processing",
			emailClaimedAt: DateTime.UtcNow);

		// Act
		await _emailNotificationProcessorService.ProcessForPendingStatusAsync(CancellationToken.None);

		// Assert - the live worker is not robbed of rows it is still processing
		var untouched = await ReloadAsync(claimed);

		untouched.Should().ContainSingle();
		untouched[0].EmailSentStatus.Should().Be("Processing");
		untouched[0].EmailClaimedAt.Should().NotBeNull();
	}
	#endregion

}
