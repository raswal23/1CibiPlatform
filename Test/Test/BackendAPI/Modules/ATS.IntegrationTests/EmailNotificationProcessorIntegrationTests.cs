using ATS.Data.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Test.BackendAPI.Infrastructure.ATS.Infrastracture;

namespace Test.BackendAPI.Modules.ATS.IntegrationTests;

public class EmailNotificationProcessorIntegrationTests : BaseIntegrationTest
{

	public EmailNotificationProcessorIntegrationTests(IntegrationTestWebAppFactory factory)
		: base(factory)
	{
	}

	private async Task<List<EmailInvitationRequest>> SeedEmailInvitationRequestsAsync(int count)
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
				EmailSentStatus = "Pending",
				ApplicationFormStatus = "Pending",
				OrderStatus = "Pending Candidate Info"
			};

			invitations.Add(invitation);
			await _dbContext.EmailInvitationRequests.AddAsync(invitation);
		}

		await _dbContext.SaveChangesAsync();
		return invitations;
	}

	#region Positive Path
	[Fact]
	public async Task ProcessForPendingStatusAsync_WithValidBatch_ShouldProcessEmailInvitations()
	{
		// Arrange
		var emailInvitations = await SeedEmailInvitationRequestsAsync(3);
		var batchId = $"testbatch:{Guid.CreateVersion7():N}:{DateTime.UtcNow:yyyyMMdd}";

		await _hybridCache.SetAsync(
			batchId,
			new List<List<EmailInvitationRequest>> { emailInvitations },
			new HybridCacheEntryOptions { Expiration = TimeSpan.FromMinutes(30) });

		var dbRedis = _redis.GetDatabase();
		await dbRedis.KeyDeleteAsync("devtest-ats-batches:pending");
		await dbRedis.KeyDeleteAsync("devtest-ats-batches:processing");
		await dbRedis.ListRightPushAsync("devtest-ats-batches:pending", batchId);

		// Act
		await _emailNotificationProcessorService.ProcessForPendingStatusAsync(CancellationToken.None);

		// Assert
		var processedInvitations = await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.Where(e => emailInvitations.Select(ei => ei.EmailInvitationID).Contains(e.EmailInvitationID))
			.ToListAsync();

		processedInvitations.Should().NotBeEmpty();
		processedInvitations.Should().AllSatisfy(e =>
		{
			e.EmailSentStatus.Should().NotBe("Pending");
		});
	}

	[Fact]
	public async Task ProcessForPendingStatusAsync_WithNoPendingBatch_ShouldDoNothing()
	{
		// Arrange
		var initialCount = await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.CountAsync();

		// Act
		await _emailNotificationProcessorService.ProcessForPendingStatusAsync(CancellationToken.None);

		// Assert 
		var finalCount = await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.CountAsync();

		finalCount.Should().Be(initialCount);
	}

	[Fact]
	public async Task ProcessForPendingStatusAsync_WithMultipleBatches_ShouldProcessSequentially()
	{
		// Arrange
		var batch1Invitations = await SeedEmailInvitationRequestsAsync(2);
		var batch2Invitations = await SeedEmailInvitationRequestsAsync(3);

		var batch1Id = $"testbatch:{Guid.CreateVersion7():N}:{DateTime.UtcNow:yyyyMMdd}";
		var batch2Id = $"testbatch:{Guid.CreateVersion7():N}:{DateTime.UtcNow.AddSeconds(1):yyyyMMdd}";

		await _hybridCache.SetAsync(
			batch1Id,
			new List<List<EmailInvitationRequest>> { batch1Invitations },
			new HybridCacheEntryOptions { Expiration = TimeSpan.FromMinutes(30) });

		await _hybridCache.SetAsync(
			batch2Id,
			new List<List<EmailInvitationRequest>> { batch2Invitations },
			new HybridCacheEntryOptions { Expiration = TimeSpan.FromMinutes(30) });

		var dbRedis = _redis.GetDatabase();
		await dbRedis.KeyDeleteAsync("devtest-ats-batches:pending");
		await dbRedis.KeyDeleteAsync("devtest-ats-batches:processing");
		await dbRedis.ListRightPushAsync("devtest-ats-batches:pending", batch1Id);

		// Act 
		await _emailNotificationProcessorService.ProcessForPendingStatusAsync(CancellationToken.None);

		// Assert 
		var batch1Processed = await _dbContext.EmailInvitationRequests
			.AsNoTracking()
			.Where(e => batch1Invitations.Select(ei => ei.EmailInvitationID).Contains(e.EmailInvitationID))
			.ToListAsync();

		batch1Processed.Should().NotBeEmpty();
		batch1Processed.Should().AllSatisfy(e => e.EmailSentStatus.Should().NotBe("Pending"));

	}
	#endregion

}
