using System.Security.Claims;
using ATS.Constants;
using ATS.Data.Entities;
using Auth.Constants;
using FluentAssertions;
using Test.BackendAPI.Infrastructure.ATS.Infrastracture;

namespace Test.BackendAPI.Modules.ATS.IntegrationTests;

public class GetWithdrawnEmailInvitationRequestsIntegrationTests : BaseIntegrationTest
{
	public GetWithdrawnEmailInvitationRequestsIntegrationTests(IntegrationTestWebAppFactory factory)
		: base(factory)
	{
	}

	#region Positive Path
	[Theory]
	[InlineData(AtsRoleIds.PlatformManager)]
	[InlineData(AtsRoleIds.Admin)]
	public async Task GetWithdrawnEmailInvitationRequests_ShouldIncludeAllRequestersForAssignedClients(
		int roleId)
	{
		var userId = Guid.CreateVersion7();
		var assigned = CreateWithdrawnInvitation("Assigned", 3, Guid.CreateVersion7());
		var sameClient = CreateWithdrawnInvitation("SameClient", 3, Guid.CreateVersion7());
		var unassigned = CreateWithdrawnInvitation("Unassigned", 4, userId);
		await _dbContext.EmailInvitationRequests.AddRangeAsync(assigned, sameClient, unassigned);
		await AddAssignmentAsync(userId, clientId: 3);
		await _dbContext.SaveChangesAsync();
		SetAuthenticatedUser(userId, roleId, clientId: 99);

		var result = await _endorsementSubmissionService.GetWithdrawnEmailInvitationRequestsAsync(
			new BuildingBlocks.Pagination.KeysetPaginationRequest(null, 10),
			CancellationToken.None);

		result.TotalCount.Should().Be(2);
		result.Items.Select(invitation => invitation.EmailInvitationID)
			.Should().BeEquivalentTo(new[]
			{
				assigned.EmailInvitationID,
				sameClient.EmailInvitationID
			});
	}

	[Theory]
	[InlineData(AtsRoleIds.User)]
	[InlineData(AtsRoleIds.Uploader)]
	public async Task GetWithdrawnEmailInvitationRequests_ShouldRequireOwnRequestorAndClientForRestrictedRoles(
		int roleId)
	{
		var userId = Guid.CreateVersion7();
		var matching = CreateWithdrawnInvitation("Matching", 5, userId);
		var wrongRequester = CreateWithdrawnInvitation("WrongRequester", 5, Guid.CreateVersion7());
		var wrongClient = CreateWithdrawnInvitation("WrongClient", 6, userId);
		await _dbContext.EmailInvitationRequests.AddRangeAsync(matching, wrongRequester, wrongClient);
		await _dbContext.SaveChangesAsync();
		SetAuthenticatedUser(userId, roleId, clientId: 5);

		var result = await _endorsementSubmissionService.GetWithdrawnEmailInvitationRequestsAsync(
			new BuildingBlocks.Pagination.KeysetPaginationRequest(null, 10),
			CancellationToken.None);

		result.TotalCount.Should().Be(1);
		result.Items.Should().ContainSingle()
			.Which.EmailInvitationID.Should().Be(matching.EmailInvitationID);
	}

	[Fact]
	public async Task GetWithdrawnEmailInvitationRequests_ShouldIncludeAllClientsAndRequesters_ForPlatformSuperAdmin()
	{
		var first = CreateWithdrawnInvitation("FirstClient", 1, Guid.CreateVersion7());
		var second = CreateWithdrawnInvitation("SecondClient", 2, Guid.CreateVersion7());
		await _dbContext.EmailInvitationRequests.AddRangeAsync(first, second);
		await _dbContext.SaveChangesAsync();
		SetAuthenticatedUser(
			Guid.CreateVersion7(),
			AtsRoleIds.User,
			clientId: 99,
			isPlatformSuperAdmin: true);

		var result = await _endorsementSubmissionService.GetWithdrawnEmailInvitationRequestsAsync(
			new BuildingBlocks.Pagination.KeysetPaginationRequest(null, 10),
			CancellationToken.None);

		result.TotalCount.Should().Be(2);
		result.Items.Select(invitation => invitation.EmailInvitationID)
			.Should().BeEquivalentTo(new[] { first.EmailInvitationID, second.EmailInvitationID });
	}

	[Fact]
	public async Task GetWithdrawnEmailInvitationRequests_ShouldReturnWithdrawnRecords()
	{
		// Arrange
		var userId = Guid.CreateVersion7();
		SetAuthenticatedUser(userId, AtsRoleIds.User, clientId: 7);
		var withdrawn1 = new EmailInvitationRequest
		{
			EmailInvitationID = Guid.CreateVersion7(),
			FirstName = "Integration",
			LastName = "Tester1",
			MiddleInitial = "A",
			EmailAddress = "withdrawn1@example.com",
			MobileNumber = "09171234567",
			HashToken = "valid-hash-token-1",
			HashTokenCreatedAt = DateTime.UtcNow,
			HashTokenExpiration = DateTime.UtcNow.AddDays(1),
			SelectPackage = "Standard",
			RushNormal = "Normal",
			EmailSentStatus = "Done",
			ApplicationFormStatus = "Pending",
			OrderStatus = "Application Withdrawn",
			ClientId = 7,
			RequestorId = userId
		};

		var withdrawn2 = new EmailInvitationRequest
		{
			EmailInvitationID = Guid.CreateVersion7(),
			FirstName = "Integration",
			LastName = "Tester2",
			MiddleInitial = "B",
			EmailAddress = "withdrawn2@example.com",
			MobileNumber = "09171234568",
			HashToken = "valid-hash-token-2",
			HashTokenCreatedAt = DateTime.UtcNow,
			HashTokenExpiration = DateTime.UtcNow.AddDays(1),
			SelectPackage = "Premium",
			RushNormal = "Rush",
			EmailSentStatus = "Done",
			ApplicationFormStatus = "Pending",
			OrderStatus = "Application Withdrawn",
			ClientId = 7,
			RequestorId = userId
		};

		var active = new EmailInvitationRequest
		{
			EmailInvitationID = Guid.CreateVersion7(),
			FirstName = "Active",
			LastName = "Candidate",
			MiddleInitial = "C",
			EmailAddress = "active@example.com",
			MobileNumber = "09171234569",
			HashToken = "valid-hash-token-3",
			HashTokenCreatedAt = DateTime.UtcNow,
			HashTokenExpiration = DateTime.UtcNow.AddDays(1),
			SelectPackage = "Standard",
			RushNormal = "Normal",
			EmailSentStatus = "Done",
			ApplicationFormStatus = "Pending",
			OrderStatus = "Pending Candidate Info"
		};

		await _dbContext.EmailInvitationRequests.AddRangeAsync(withdrawn1, withdrawn2, active);
		await _dbContext.SaveChangesAsync();

      // Act
		var result = await _endorsementSubmissionService.GetWithdrawnEmailInvitationRequestsAsync(new BuildingBlocks.Pagination.KeysetPaginationRequest(null, 10), CancellationToken.None);

		// Assert
		result.Should().NotBeNull();
		result!.Items.Should().HaveCount(2);
		result.Items.Should().AllSatisfy(x => x.OrderStatus.Should().Be("Application Withdrawn"));
		result.Items.Select(x => x.EmailAddress).Should().Contain(new[] { "withdrawn1@example.com", "withdrawn2@example.com" });
	}

	[Fact]
	public async Task GetWithdrawnEmailInvitationRequests_ShouldReturnPaginatedResults()
	{
		// Arrange
		var userId = Guid.CreateVersion7();
		SetAuthenticatedUser(userId, AtsRoleIds.User, clientId: 7);
		var withdrawnRecords = new List<EmailInvitationRequest>();
		for (int i = 0; i < 15; i++)
		{
			withdrawnRecords.Add(new EmailInvitationRequest
			{
				EmailInvitationID = Guid.CreateVersion7(),
				FirstName = $"Tester{i}",
				LastName = $"Withdrawn{i}",
				MiddleInitial = "T",
				EmailAddress = $"withdrawn{i}@example.com",
				MobileNumber = $"0917123456{i % 10}",
				HashToken = $"hash-token-{i}",
				HashTokenCreatedAt = DateTime.UtcNow,
				HashTokenExpiration = DateTime.UtcNow.AddDays(1),
				SelectPackage = "Standard",
				RushNormal = "Normal",
				EmailSentStatus = "Done",
				ApplicationFormStatus = "Pending",
				OrderStatus = "Application Withdrawn",
				ClientId = 7,
				RequestorId = userId
			});
		}

		await _dbContext.EmailInvitationRequests.AddRangeAsync(withdrawnRecords);
		await _dbContext.SaveChangesAsync();

		// Act - Get first page
		var page1 = await _endorsementSubmissionService.GetWithdrawnEmailInvitationRequestsAsync(new BuildingBlocks.Pagination.KeysetPaginationRequest(null, 10), CancellationToken.None);

		// Assert
		page1.Should().NotBeNull();
		page1!.Items.Should().HaveCount(10);
		page1.TotalCount.Should().Be(15);
		page1.NextCursor.Should().NotBeNull();

		// Act - Get second page via the returned cursor
		var page2 = await _endorsementSubmissionService.GetWithdrawnEmailInvitationRequestsAsync(new BuildingBlocks.Pagination.KeysetPaginationRequest(page1.NextCursor, 10), CancellationToken.None);

		// Assert
		page2.Should().NotBeNull();
		page2!.Items.Should().HaveCount(5);
		page2.TotalCount.Should().BeNull();
		page2.Items.Select(x => x.EmailInvitationID)
			.Should().NotIntersectWith(page1.Items.Select(x => x.EmailInvitationID));
		page2.NextCursor.Should().BeNull();
	}

	[Fact]
	public async Task GetWithdrawnEmailInvitationRequests_ShouldReturnEmptyWhenNoWithdrawnRecords()
	{
		// Arrange
		SetAuthenticatedUser(Guid.CreateVersion7(), AtsRoleIds.User, clientId: 7);
		var active = new EmailInvitationRequest
		{
			EmailInvitationID = Guid.CreateVersion7(),
			FirstName = "Active",
			LastName = "Candidate",
			MiddleInitial = "C",
			EmailAddress = "active@example.com",
			MobileNumber = "09171234569",
			HashToken = "valid-hash-token-3",
			HashTokenCreatedAt = DateTime.UtcNow,
			HashTokenExpiration = DateTime.UtcNow.AddDays(1),
			SelectPackage = "Standard",
			RushNormal = "Normal",
			EmailSentStatus = "Done",
			ApplicationFormStatus = "Pending",
			OrderStatus = "Pending Candidate Info"
		};

		await _dbContext.EmailInvitationRequests.AddAsync(active);
		await _dbContext.SaveChangesAsync();

      // Act
		var result = await _endorsementSubmissionService.GetWithdrawnEmailInvitationRequestsAsync(new BuildingBlocks.Pagination.KeysetPaginationRequest(null, 10), CancellationToken.None);

		// Assert
		result.Should().NotBeNull();
		result!.Items.Should().BeEmpty();
		result.TotalCount.Should().Be(0);
		result.NextCursor.Should().BeNull();
	}


	[Fact]
	public async Task SearchWithdrawnEmailInvitationRequests_ShouldReturnMatchingRecords()
	{
		// Arrange
		var userId = Guid.CreateVersion7();
		SetAuthenticatedUser(userId, AtsRoleIds.User, clientId: 7);
		var withdrawn1 = new EmailInvitationRequest
		{
			EmailInvitationID = Guid.CreateVersion7(),
			FirstName = "John",
			LastName = "Doe",
			MiddleInitial = "A",
			EmailAddress = "john.doe@example.com",
			MobileNumber = "09171234567",
			HashToken = "hash-1",
			HashTokenCreatedAt = DateTime.UtcNow,
			HashTokenExpiration = DateTime.UtcNow.AddDays(1),
			SelectPackage = "Standard",
			RushNormal = "Normal",
			EmailSentStatus = "Done",
			ApplicationFormStatus = "Pending",
			OrderStatus = "Application Withdrawn",
			ClientId = 7,
			RequestorId = userId
		};

		var withdrawn2 = new EmailInvitationRequest
		{
			EmailInvitationID = Guid.CreateVersion7(),
			FirstName = "Jane",
			LastName = "Smith",
			MiddleInitial = "B",
			EmailAddress = "jane.smith@example.com",
			MobileNumber = "09171234568",
			HashToken = "hash-2",
			HashTokenCreatedAt = DateTime.UtcNow,
			HashTokenExpiration = DateTime.UtcNow.AddDays(1),
			SelectPackage = "Premium",
			RushNormal = "Rush",
			EmailSentStatus = "Done",
			ApplicationFormStatus = "Pending",
			OrderStatus = "Application Withdrawn",
			ClientId = 7,
			RequestorId = userId
		};

		await _dbContext.EmailInvitationRequests.AddRangeAsync(withdrawn1, withdrawn2);
		await _dbContext.SaveChangesAsync();

		// Act
       var result = await _endorsementSubmissionService.GetWithdrawnEmailInvitationRequestsAsync(new BuildingBlocks.Pagination.KeysetPaginationRequest(null, 10, "john"), CancellationToken.None);

		// Assert
		result.Should().NotBeNull();
		result!.Items.Should().HaveCount(1);
		result.Items.First().FirstName.Should().Be("John");
		result.Items.First().EmailAddress.Should().Be("john.doe@example.com");
	}

	[Fact]
	public async Task SearchWithdrawnEmailInvitationRequests_ShouldSearchByLastName()
	{
		// Arrange
		var userId = Guid.CreateVersion7();
		SetAuthenticatedUser(userId, AtsRoleIds.User, clientId: 7);
		var withdrawn1 = new EmailInvitationRequest
		{
			EmailInvitationID = Guid.CreateVersion7(),
			FirstName = "John",
			LastName = "Doe",
			MiddleInitial = "A",
			EmailAddress = "john.doe@example.com",
			MobileNumber = "09171234567",
			HashToken = "hash-1",
			HashTokenCreatedAt = DateTime.UtcNow,
			HashTokenExpiration = DateTime.UtcNow.AddDays(1),
			SelectPackage = "Standard",
			RushNormal = "Normal",
			EmailSentStatus = "Done",
			ApplicationFormStatus = "Pending",
			OrderStatus = "Application Withdrawn",
			ClientId = 7,
			RequestorId = userId
		};

		var withdrawn2 = new EmailInvitationRequest
		{
			EmailInvitationID = Guid.CreateVersion7(),
			FirstName = "Jane",
			LastName = "Doe",
			MiddleInitial = "B",
			EmailAddress = "jane.doe@example.com",
			MobileNumber = "09171234568",
			HashToken = "hash-2",
			HashTokenCreatedAt = DateTime.UtcNow,
			HashTokenExpiration = DateTime.UtcNow.AddDays(1),
			SelectPackage = "Premium",
			RushNormal = "Rush",
			EmailSentStatus = "Done",
			ApplicationFormStatus = "Pending",
			OrderStatus = "Application Withdrawn",
			ClientId = 7,
			RequestorId = userId
		};

		await _dbContext.EmailInvitationRequests.AddRangeAsync(withdrawn1, withdrawn2);
		await _dbContext.SaveChangesAsync();

		// Act
       var result = await _endorsementSubmissionService.GetWithdrawnEmailInvitationRequestsAsync(new BuildingBlocks.Pagination.KeysetPaginationRequest(null, 10, "doe"), CancellationToken.None);

		// Assert
		result.Should().NotBeNull();
		result!.Items.Should().HaveCount(2);
		result.Items.Should().AllSatisfy(x => x.LastName.Should().Be("Doe"));
	}

	[Fact]
	public async Task SearchWithdrawnEmailInvitationRequests_ShouldReturnEmptyWhenNoMatch()
	{
		// Arrange
		var userId = Guid.CreateVersion7();
		SetAuthenticatedUser(userId, AtsRoleIds.User, clientId: 7);
		var withdrawn = new EmailInvitationRequest
		{
			EmailInvitationID = Guid.CreateVersion7(),
			FirstName = "John",
			LastName = "Doe",
			MiddleInitial = "A",
			EmailAddress = "john.doe@example.com",
			MobileNumber = "09171234567",
			HashToken = "hash-1",
			HashTokenCreatedAt = DateTime.UtcNow,
			HashTokenExpiration = DateTime.UtcNow.AddDays(1),
			SelectPackage = "Standard",
			RushNormal = "Normal",
			EmailSentStatus = "Done",
			ApplicationFormStatus = "Pending",
			OrderStatus = "Application Withdrawn",
			ClientId = 7,
			RequestorId = userId
		};

		await _dbContext.EmailInvitationRequests.AddAsync(withdrawn);
		await _dbContext.SaveChangesAsync();

		// Act
       var result = await _endorsementSubmissionService.GetWithdrawnEmailInvitationRequestsAsync(new BuildingBlocks.Pagination.KeysetPaginationRequest(null, 10, "nonexistent"), CancellationToken.None);

		// Assert
		result.Should().NotBeNull();
		result!.Items.Should().BeEmpty();
		result.TotalCount.Should().Be(0);
	}

	#endregion

	private async Task AddAssignmentAsync(Guid userId, int clientId)
	{
		var now = DateTime.UtcNow;
		await _dbContext.UserClientDetails.AddAsync(new UserClientDetails
		{
			UserId = userId,
			ClientId = clientId,
			CreatedAt = now,
			UpdatedAt = now
		});
	}

	private void SetAuthenticatedUser(
		Guid userId,
		int roleId,
		int clientId,
		bool isPlatformSuperAdmin = false)
	{
		var claims = new List<Claim>
		{
			new(ClaimTypes.NameIdentifier, userId.ToString()),
			new(AuthClaimTypes.AtsRoleId, roleId.ToString()),
			new(AuthClaimTypes.AtsClientId, clientId.ToString())
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

	private static EmailInvitationRequest CreateWithdrawnInvitation(
		string prefix,
		int clientId,
		Guid requestorId)
	{
		var id = Guid.CreateVersion7();
		return new EmailInvitationRequest
		{
			EmailInvitationID = id,
			FirstName = prefix,
			LastName = "Candidate",
			EmailAddress = $"{id:N}@example.com",
			MobileNumber = "09171234567",
			HashToken = $"hash-{id:N}",
			HashTokenCreatedAt = DateTime.UtcNow,
			HashTokenExpiration = DateTime.UtcNow.AddDays(1),
			SelectPackage = "Standard",
			RushNormal = "Normal",
			EmailSentStatus = "Done",
			ApplicationFormStatus = "Withdrawn",
			OrderStatus = "Application Withdrawn",
			ClientId = clientId,
			RequestorId = requestorId
		};
	}
}
