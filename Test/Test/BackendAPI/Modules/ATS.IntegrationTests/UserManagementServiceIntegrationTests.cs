using ATS.DTO;
using Auth.Data.Entities;
using Auth.Constants;
using BuildingBlocks.Exceptions;
using BuildingBlocks.Pagination;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Test.BackendAPI.Infrastructure.ATS.Infrastracture;

namespace Test.BackendAPI.Modules.ATS.IntegrationTests;

public class UserManagementServiceIntegrationTests : BaseIntegrationTest
{
	public UserManagementServiceIntegrationTests(IntegrationTestWebAppFactory factory)
		: base(factory)
	{
	}

	#region Happy Path

	[Fact]
	public async Task GetAuthUsersAsync_ShouldReturnOnlyActiveApprovedUsersAssignedToATS()
	{
		// Arrange
		var expectedUserId = await AddAuthUserAsync(
			"alice.reviewer@example.com",
			"Alice",
			"Reviewer",
			middleName: "Middle");
		await AddAuthUserAsync(
			"inactive@example.com",
			"Inactive",
			"User",
			isActive: false);
		await AddAuthUserAsync(
			"pending@example.com",
			"Pending",
			"User",
			isApproved: false);
		await AddAuthUserAsync(
			"unassigned@example.com",
			"Unassigned",
			"User",
			assignedToAts: false);

		// Act
		var result = await _userManagementService.GetAuthUsersAsync(CancellationToken.None);

		// Assert
		result.Should().ContainSingle();
		result[0].UserId.Should().Be(expectedUserId);
		result[0].UserName.Should().Be("Alice Middle Reviewer");
		result[0].UserEmail.Should().Be("alice.reviewer@example.com");
	}

	[Fact]
	public async Task AtsAccessClaimsProvider_ShouldReadActiveRoleAndClientAssignment()
	{
		var userId = await AddAuthUserAsync("claims.user@example.com", "Claims", "User");
		var clientId = await AddClientAsync("Claims Client");
		var roleId = await AddRoleAsync("Claims Role");
		var moduleId = await AddModuleAsync("Claims Module");
		await _userManagementService.AssignUserClientAsync(
			new AssignUserClientDTO { UserId = userId, ClientId = clientId },
			CancellationToken.None);
		await _userManagementService.AddUserAsync(
			CreateAddRequest(userId, roleId, null, "Claims Site", true, moduleId),
			CancellationToken.None);

		var claims = await _atsAccessClaimsProvider.GetClaimsAsync(userId);

		claims.Should().NotBeNull();
		claims!.AtsRoleId.Should().Be(roleId);
		claims.AtsClientId.Should().Be(clientId);
	}

	[Fact]
	public async Task RoleTwo_ShouldOnlyReadUsersForClaimedClient()
	{
		var firstUserId = await AddAuthUserAsync("first.client@example.com", "First", "Client");
		var secondUserId = await AddAuthUserAsync("second.client@example.com", "Second", "Client");
		var firstClientId = await AddClientAsync("First Scoped Client");
		var secondClientId = await AddClientAsync("Second Scoped Client");
		var roleId = await AddRoleAsync("Scoped User Role");
		var moduleId = await AddModuleAsync("Scoped User Module");

		await _userManagementService.AssignUserClientAsync(
			new AssignUserClientDTO { UserId = firstUserId, ClientId = firstClientId },
			CancellationToken.None);
		await _userManagementService.AssignUserClientAsync(
			new AssignUserClientDTO { UserId = secondUserId, ClientId = secondClientId },
			CancellationToken.None);
		await _userManagementService.AddUserAsync(
			CreateAddRequest(firstUserId, roleId, null, "First Site", true, moduleId),
			CancellationToken.None);
		await _userManagementService.AddUserAsync(
			CreateAddRequest(secondUserId, roleId, null, "Second Site", true, moduleId),
			CancellationToken.None);

		await _userManagementService.GetUsersAsync(
			new KeysetPaginationRequest(Cursor: null, PageSize: 10),
			CancellationToken.None);
		SetAtsScope(2, firstClientId);

		var candidates = await _userManagementService.GetAuthUsersAsync(CancellationToken.None);
		var assignments = await _userManagementService.GetUserClientAssignmentsAsync(CancellationToken.None);
		var users = await _userManagementService.GetUsersAsync(
			new KeysetPaginationRequest(Cursor: null, PageSize: 10),
			CancellationToken.None);

		candidates.Select(user => user.UserId).Should().Equal(firstUserId);
		assignments.Select(item => item.UserId).Should().Equal(firstUserId);
		users.TotalCount.Should().Be(1);
		users.Items.Should().OnlyContain(user => user.UserId == firstUserId && user.ClientId == firstClientId);
	}

	[Fact]
	public async Task PlatformSuperAdmin_ShouldOverrideClientScopedAtsRole()
	{
		var firstUserId = await AddAuthUserAsync("platform.first@example.com", "Platform", "First");
		var secondUserId = await AddAuthUserAsync("platform.second@example.com", "Platform", "Second");
		var firstClientId = await AddClientAsync("Platform First Client");
		var secondClientId = await AddClientAsync("Platform Second Client");
		var roleId = await AddRoleAsync("Platform SuperAdmin User Role");
		var moduleId = await AddModuleAsync("Platform SuperAdmin User Module");

		await _userManagementService.AssignUserClientAsync(
			new AssignUserClientDTO { UserId = firstUserId, ClientId = firstClientId },
			CancellationToken.None);
		await _userManagementService.AssignUserClientAsync(
			new AssignUserClientDTO { UserId = secondUserId, ClientId = secondClientId },
			CancellationToken.None);
		await _userManagementService.AddUserAsync(
			CreateAddRequest(firstUserId, roleId, null, "First Site", true, moduleId),
			CancellationToken.None);

		SetAtsScope(2, firstClientId, isPlatformSuperAdmin: true);

		var candidates = await _userManagementService.GetAuthUsersAsync(CancellationToken.None);
		var addResult = await _userManagementService.AddUserAsync(
			CreateAddRequest(secondUserId, roleId, null, "Second Site", true, moduleId),
			CancellationToken.None);
		var users = await _userManagementService.GetUsersAsync(
			new KeysetPaginationRequest(Cursor: null, PageSize: 10),
			CancellationToken.None);

		candidates.Select(user => user.UserId).Should().BeEquivalentTo([firstUserId, secondUserId]);
		addResult.Should().BeTrue();
		users.Items.Select(user => user.UserId).Distinct().Should().BeEquivalentTo([firstUserId, secondUserId]);
	}

	[Fact]
	public async Task UnsupportedAtsRole_ShouldReturnEmptyUserManagementReads()
	{
		await AddAuthUserAsync("unsupported.role@example.com", "Unsupported", "Role");
		SetAtsScope(99, null);

		var candidates = await _userManagementService.GetAuthUsersAsync(CancellationToken.None);
		var assignments = await _userManagementService.GetUserClientAssignmentsAsync(CancellationToken.None);
		var users = await _userManagementService.GetUsersAsync(
			new KeysetPaginationRequest(Cursor: null, PageSize: 10),
			CancellationToken.None);

		candidates.Should().BeEmpty();
		assignments.Should().BeEmpty();
		users.TotalCount.Should().Be(0);
		users.Items.Should().BeEmpty();
	}

	[Fact]
	public async Task RoleTwo_ShouldRejectAddingUserFromAnotherClient()
	{
		var userId = await AddAuthUserAsync("cross.client@example.com", "Cross", "Client");
		var currentClientId = await AddClientAsync("Current Client");
		var otherClientId = await AddClientAsync("Other Client");
		var roleId = await AddRoleAsync("Cross Client Role");
		var moduleId = await AddModuleAsync("Cross Client Module");
		await _userManagementService.AssignUserClientAsync(
			new AssignUserClientDTO { UserId = userId, ClientId = otherClientId },
			CancellationToken.None);
		SetAtsScope(2, currentClientId);

		Func<Task> act = () => _userManagementService.AddUserAsync(
			CreateAddRequest(userId, roleId, null, "Other Site", true, moduleId),
			CancellationToken.None);

		await act.Should().ThrowAsync<ForbiddenException>();
		(await _dbContext.UserDetails.AnyAsync(user => user.UserId == userId)).Should().BeFalse();
	}

	[Fact]
	public async Task GetAssignmentsAsync_ShouldPaginateActiveATSUsersAndIncludeUnassignedUsers()
	{
		// Arrange
		var assignedUserId = await AddAuthUserAsync(
			"assigned.alpha@example.com",
			"Assigned",
			"Alpha");
		await AddAuthUserAsync(
			"unassigned.middle@example.com",
			"Unassigned",
			"Middle");
		await AddAuthUserAsync(
			"unassigned.zulu@example.com",
			"Unassigned",
			"Zulu");
		var clientId = await AddClientAsync("Assignment Grid Client");
		await _clientAssignmentService.AssignClientAsync(
			new AssignUserClientDTO { UserId = assignedUserId, ClientId = clientId },
			CancellationToken.None);

		// Act
		var page = await _clientAssignmentService.GetAssignmentsAsync(
			new KeysetPaginationRequest(Cursor: null, PageSize: 2),
			CancellationToken.None);
		var search = await _clientAssignmentService.GetAssignmentsAsync(
			new KeysetPaginationRequest(Cursor: null, PageSize: 10, SearchTerm: "zulu@"),
			CancellationToken.None);

		// Assert
		page.TotalCount.Should().Be(3);
		page.Items.Should().HaveCount(2);
		page.Items.First().UserId.Should().Be(assignedUserId);
		page.Items.First().ClientId.Should().Be(clientId);
		page.Items.First().ClientName.Should().Be("Assignment Grid Client");
		page.Items.Last().ClientId.Should().BeNull();
		page.NextCursor.Should().NotBeNull();

		var secondPage = await _clientAssignmentService.GetAssignmentsAsync(
			new KeysetPaginationRequest(Cursor: page.NextCursor, PageSize: 2),
			CancellationToken.None);
		secondPage.TotalCount.Should().BeNull();
		secondPage.Items.Should().ContainSingle();
		secondPage.Items.Select(item => item.UserId)
			.Should().NotIntersectWith(page.Items.Select(item => item.UserId));
		secondPage.NextCursor.Should().BeNull();

		search.Items.Should().ContainSingle();
		search.Items.Single().UserEmail.Should().Be("unassigned.zulu@example.com");
	}

	[Fact]
	public async Task GetAssignableClientsAsync_ShouldSearchAndExcludeInactiveClients()
	{
		// Arrange
		var expectedClientId = await AddClientAsync("Searchable Active Client");
		await AddClientAsync("Searchable Inactive Client", isActive: false);
		await AddClientAsync("Different Client");

		// Act
		var result = await _clientAssignmentService.GetAssignableClientsAsync(
			new KeysetPaginationRequest(Cursor: null, PageSize: 10, SearchTerm: "searchable"),
			CancellationToken.None);

		// Assert
		result.TotalCount.Should().Be(1);
		result.Items.Should().ContainSingle();
		result.Items.Single().ClientId.Should().Be(expectedClientId);
	}

	[Fact]
	public async Task AssignClientAsync_ShouldNotChangeTimestamps_WhenClientIsUnchanged()
	{
		// Arrange
		var userId = await AddAuthUserAsync("no.op@example.com", "No", "Op");
		var clientId = await AddClientAsync("No-op Assignment Client");
		var first = await _clientAssignmentService.AssignClientAsync(
			new AssignUserClientDTO { UserId = userId, ClientId = clientId },
			CancellationToken.None);

		// Act
		var second = await _clientAssignmentService.AssignClientAsync(
			new AssignUserClientDTO { UserId = userId, ClientId = clientId },
			CancellationToken.None);

		// Assert
		second.AssignedAt.Should().Be(first.AssignedAt);
		second.UpdatedAt.Should().Be(first.UpdatedAt);
	}

	[Fact]
	public async Task AssignUserClientAsync_ShouldUpsertAssignmentAndPropagateClientToAccessRows()
	{
		// Arrange
		var userId = await AddAuthUserAsync("assigned.user@example.com", "Assigned", "User");
		var originalClientId = await AddClientAsync("Original Assignment Client");
		var updatedClientId = await AddClientAsync("Updated Assignment Client");
		var roleId = await AddRoleAsync("Assignment Role");
		var moduleId = await AddModuleAsync("Assignment Module");

		var originalAssignment = await _userManagementService.AssignUserClientAsync(
			new AssignUserClientDTO { UserId = userId, ClientId = originalClientId },
			CancellationToken.None);

		await _userManagementService.AddUserAsync(
			CreateAddRequest(userId, roleId, null, "Manila", true, moduleId),
			CancellationToken.None);

		// Act
		var result = await _userManagementService.AssignUserClientAsync(
			new AssignUserClientDTO { UserId = userId, ClientId = updatedClientId },
			CancellationToken.None);

		// Assert
		result.UserId.Should().Be(userId);
		result.ClientId.Should().Be(updatedClientId);
		result.CreatedAt.Should().Be(originalAssignment.CreatedAt);
		result.UpdatedAt.Should().BeOnOrAfter(originalAssignment.UpdatedAt);

		var assignments = await _userManagementService.GetUserClientAssignmentsAsync(CancellationToken.None);
		assignments.Should().ContainSingle();
		assignments[0].ClientId.Should().Be(updatedClientId);

		var accessRows = await _dbContext.UserDetails
			.AsNoTracking()
			.Where(user => user.UserId == userId)
			.ToListAsync();
		accessRows.Should().OnlyContain(user => user.ClientId == updatedClientId);
	}

	[Fact]
	public async Task AddUserAsync_ShouldPersistAuthIdentityAndOneAccessRowPerSelectedModule()
	{
		// Arrange
		var userId = await AddAuthUserAsync(
			"canonical.user@example.com",
			"Canonical",
			"User",
			middleName: "Auth");
		var clientId = await AddClientAsync("Canonical User Client");
		var roleId = await AddRoleAsync("Canonical User Role");
		var dashboardModuleId = await AddModuleAsync("Canonical Dashboard");
		var reportsModuleId = await AddModuleAsync("Canonical Reports");

		await _userManagementService.AssignUserClientAsync(
			new AssignUserClientDTO { UserId = userId, ClientId = clientId },
			CancellationToken.None);

		var request = CreateAddRequest(
			userId,
			roleId,
			int.MaxValue,
			"  Manila Site  ",
			true,
			dashboardModuleId,
			reportsModuleId);

		// Act
		var result = await _userManagementService.AddUserAsync(request, CancellationToken.None);

		// Assert
		result.Should().BeTrue();

		var persisted = await _dbContext.UserDetails
			.AsNoTracking()
			.Where(user => user.UserId == userId)
			.OrderBy(user => user.ModuleId)
			.ToListAsync();

		persisted.Should().HaveCount(2);
		persisted.Select(user => user.ModuleId).Should().Equal(dashboardModuleId, reportsModuleId);
		persisted.Should().OnlyContain(user =>
			user.UserName == "Canonical Auth User" &&
			user.UserEmail == "canonical.user@example.com" &&
			user.ClientId == clientId &&
			user.Site == "Manila Site" &&
			user.RoleId == roleId &&
			user.IsActive);
		persisted.Select(user => user.CreatedAt).Distinct().Should().ContainSingle();
		persisted.Should().OnlyContain(user => user.UpdatedAt == user.CreatedAt);
	}

	[Fact]
	public async Task AddUserAsync_ShouldAllowPlatformSuperAdminToAddUserWithoutClientAssignment()
	{
		// Arrange
		var userId = await AddAuthUserAsync("unassigned.platform.target@example.com", "Platform", "Target");
		var roleId = await AddRoleAsync("Super Admin Role");
		var moduleId = await AddModuleAsync("Super Admin Module");
		var request = CreateAddRequest(userId, roleId, null, "All", true, moduleId);
		SetAtsScope(99, null, isPlatformSuperAdmin: true);

		// Act
		var result = await _userManagementService.AddUserAsync(request, CancellationToken.None);

		// Assert
		result.Should().BeTrue();

		var persisted = await _dbContext.UserDetails
			.AsNoTracking()
			.SingleAsync(user => user.UserId == userId);
		persisted.ClientId.Should().BeNull();

		var hasClientAssignment = await _dbContext.UserClientDetails
			.AsNoTracking()
			.AnyAsync(assignment => assignment.UserId == userId);
		hasClientAssignment.Should().BeFalse();
	}

	[Fact]
	public async Task GetUsersAsync_ShouldPaginateLogicalUsersAndSearchAcrossUserFields()
	{
		// Arrange
		var alphaUserId = await AddAuthUserAsync("alpha@example.com", "Alice", "Alpha");
		var middleUserId = await AddAuthUserAsync("middle@example.com", "Mike", "Middle");
		var zuluUserId = await AddAuthUserAsync("zulu@example.com", "Zed", "Zulu");
		var clientId = await AddClientAsync("User Listing Client");
		var roleId = await AddRoleAsync("User Listing Role");
		var firstModuleId = await AddModuleAsync("User Listing First Module");
		var secondModuleId = await AddModuleAsync("User Listing Second Module");

		foreach (var userId in new[] { alphaUserId, middleUserId, zuluUserId })
		{
			await _userManagementService.AssignUserClientAsync(
				new AssignUserClientDTO { UserId = userId, ClientId = clientId },
				CancellationToken.None);
		}

		await _userManagementService.AddUserAsync(
			CreateAddRequest(alphaUserId, roleId, null, "Manila", true, firstModuleId, secondModuleId),
			CancellationToken.None);
		await _userManagementService.AddUserAsync(
			CreateAddRequest(middleUserId, roleId, null, "Premium Operations", true, firstModuleId),
			CancellationToken.None);
		await _userManagementService.AddUserAsync(
			CreateAddRequest(zuluUserId, roleId, null, "Cebu", true, secondModuleId),
			CancellationToken.None);

		// Act
		var page = await _userManagementService.GetUsersAsync(
			new KeysetPaginationRequest(Cursor: null, PageSize: 2),
			CancellationToken.None);
		var search = await _userManagementService.GetUsersAsync(
			new KeysetPaginationRequest(Cursor: null, PageSize: 10, SearchTerm: "PREMIUM"),
			CancellationToken.None);

		// Assert
		page.TotalCount.Should().Be(3);
		page.Items.Should().HaveCount(3);
		page.Items.Select(user => user.UserName).Distinct()
			.Should().Equal("Alice Alpha", "Mike Middle");
		page.Items.Where(user => user.UserId == alphaUserId)
			.Select(user => user.ModuleId)
			.Should().Equal(firstModuleId, secondModuleId);
		page.NextCursor.Should().NotBeNull();

		var secondPage = await _userManagementService.GetUsersAsync(
			new KeysetPaginationRequest(Cursor: page.NextCursor, PageSize: 2),
			CancellationToken.None);
		secondPage.TotalCount.Should().BeNull();
		secondPage.Items.Select(user => user.UserName).Distinct()
			.Should().Equal("Zed Zulu");
		secondPage.NextCursor.Should().BeNull();

		search.TotalCount.Should().Be(1);
		search.Items.Should().ContainSingle();
		search.Items.Single().UserId.Should().Be(middleUserId);
	}

	[Fact]
	public async Task EditUserAsync_ShouldSynchronizeModulesAndUseCurrentAuthAndClientAssignments()
	{
		// Arrange
		var userId = await AddAuthUserAsync("edited.user@example.com", "Edited", "User");
		var originalClientId = await AddClientAsync("Original User Client");
		var updatedClientId = await AddClientAsync("Updated User Client");
		var originalRoleId = await AddRoleAsync("Original User Role");
		var updatedRoleId = await AddRoleAsync("Updated User Role");
		var removedModuleId = await AddModuleAsync("Removed User Module");
		var retainedModuleId = await AddModuleAsync("Retained User Module");
		var addedModuleId = await AddModuleAsync("Added User Module");

		await _userManagementService.AssignUserClientAsync(
			new AssignUserClientDTO { UserId = userId, ClientId = originalClientId },
			CancellationToken.None);
		await _userManagementService.AddUserAsync(
			CreateAddRequest(userId, originalRoleId, null, "Original Site", true, removedModuleId, retainedModuleId),
			CancellationToken.None);

		var createdAt = await _dbContext.UserDetails
			.AsNoTracking()
			.Where(user => user.UserId == userId)
			.MinAsync(user => user.CreatedAt);

		await _userManagementService.AssignUserClientAsync(
			new AssignUserClientDTO { UserId = userId, ClientId = updatedClientId },
			CancellationToken.None);
		_dbContext.ChangeTracker.Clear();

		var request = CreateEditRequest(
			userId,
			updatedRoleId,
			originalClientId,
			"  Updated Site  ",
			true,
			retainedModuleId,
			addedModuleId);

		// Act
		var result = await _userManagementService.EditUserAsync(request, CancellationToken.None);

		// Assert
		result.Should().HaveCount(2);
		result.Select(user => user.ModuleId).Should().Equal(retainedModuleId, addedModuleId);
		result.Should().OnlyContain(user =>
			user.UserName == "Edited User" &&
			user.UserEmail == "edited.user@example.com" &&
			user.ClientId == updatedClientId &&
			user.Site == "Updated Site" &&
			user.RoleId == updatedRoleId &&
			user.IsActive);

		var persisted = await _dbContext.UserDetails
			.AsNoTracking()
			.Where(user => user.UserId == userId)
			.OrderBy(user => user.ModuleId)
			.ToListAsync();
		persisted.Select(user => user.ModuleId).Should().Equal(retainedModuleId, addedModuleId);
		persisted.Should().OnlyContain(user => user.CreatedAt == createdAt);
		persisted.Should().NotContain(user => user.ModuleId == removedModuleId);

		var activeModuleIds = await _userManagementService.GetActiveUserModuleIdsAsync(
			userId,
			CancellationToken.None);
		activeModuleIds.Should().Equal(retainedModuleId, addedModuleId);
	}

	#endregion

	#region Bad Path

	[Fact]
	public async Task AddUserAsync_ShouldThrowBadRequestException_WhenClientIsNotAssigned()
	{
		// Arrange
		var userId = await AddAuthUserAsync("no.client@example.com", "No", "Client");
		var request = CreateAddRequest(userId, int.MaxValue, null, "Manila", true, int.MaxValue);

		// Act
		Func<Task> act = () => _userManagementService.AddUserAsync(request, CancellationToken.None);

		// Assert
		await act.Should()
			.ThrowAsync<BadRequestException>()
			.WithMessage("Assign a client to this Auth user before configuring ATS access.");
	}

	[Fact]
	public async Task AddUserAsync_ShouldThrowBadRequestException_WhenSelectedModuleIsInactive()
	{
		// Arrange
		var userId = await AddAuthUserAsync("inactive.module@example.com", "Inactive", "Module");
		var clientId = await AddClientAsync("Inactive Module Client");
		var roleId = await AddRoleAsync("Inactive Module Role");
		var moduleId = await AddModuleAsync("Inactive User Module", isActive: false);
		await _userManagementService.AssignUserClientAsync(
			new AssignUserClientDTO { UserId = userId, ClientId = clientId },
			CancellationToken.None);

		var request = CreateAddRequest(userId, roleId, null, "Manila", true, moduleId);

		// Act
		Func<Task> act = () => _userManagementService.AddUserAsync(request, CancellationToken.None);

		// Assert
		await act.Should()
			.ThrowAsync<BadRequestException>()
			.WithMessage("One or more selected modules do not exist or are inactive.");

		var userWasPersisted = await _dbContext.UserDetails
			.AsNoTracking()
			.AnyAsync(user => user.UserId == userId);
		userWasPersisted.Should().BeFalse();
	}

	[Fact]
	public async Task AddUserAsync_ShouldThrowBadRequestException_WhenAuthUserAlreadyExistsInATS()
	{
		// Arrange
		var userId = await AddAuthUserAsync("duplicate.ats@example.com", "Duplicate", "ATS");
		var clientId = await AddClientAsync("Duplicate ATS User Client");
		var roleId = await AddRoleAsync("Duplicate ATS User Role");
		var moduleId = await AddModuleAsync("Duplicate ATS User Module");
		await _userManagementService.AssignUserClientAsync(
			new AssignUserClientDTO { UserId = userId, ClientId = clientId },
			CancellationToken.None);

		var request = CreateAddRequest(userId, roleId, null, "Manila", true, moduleId);
		await _userManagementService.AddUserAsync(request, CancellationToken.None);

		// Act
		Func<Task> act = () => _userManagementService.AddUserAsync(request, CancellationToken.None);

		// Assert
		await act.Should()
			.ThrowAsync<BadRequestException>()
			.WithMessage("The selected Auth user already exists in ATS User Management.");

		var persistedCount = await _dbContext.UserDetails
			.AsNoTracking()
			.CountAsync(user => user.UserId == userId);
		persistedCount.Should().Be(1);
	}

	[Fact]
	public async Task EditUserAsync_ShouldThrowNotFoundException_WhenATSUserDoesNotExist()
	{
		// Arrange
		var userId = await AddAuthUserAsync("missing.ats@example.com", "Missing", "ATS");
		var clientId = await AddClientAsync("Missing ATS User Client");
		var roleId = await AddRoleAsync("Missing ATS User Role");
		var moduleId = await AddModuleAsync("Missing ATS User Module");
		await _userManagementService.AssignUserClientAsync(
			new AssignUserClientDTO { UserId = userId, ClientId = clientId },
			CancellationToken.None);

		var request = CreateEditRequest(userId, roleId, null, "Manila", true, moduleId);

		// Act
		Func<Task> act = () => _userManagementService.EditUserAsync(request, CancellationToken.None);

		// Assert
		await act.Should()
			.ThrowAsync<NotFoundException>()
			.WithMessage($"User with ID {userId} was not found.");
	}

	[Fact]
	public async Task AssignUserClientAsync_ShouldThrowBadRequestException_WhenClientIsInactive()
	{
		// Arrange
		var userId = await AddAuthUserAsync("inactive.client@example.com", "Inactive", "Client");
		var clientId = await AddClientAsync("Inactive Assigned Client", isActive: false);
		var assignment = new AssignUserClientDTO { UserId = userId, ClientId = clientId };

		// Act
		Func<Task> act = () => _userManagementService.AssignUserClientAsync(
			assignment,
			CancellationToken.None);

		// Assert
		await act.Should()
			.ThrowAsync<BadRequestException>()
			.WithMessage("The selected client does not exist or is inactive.");
	}

	#endregion

	private async Task<Guid> AddAuthUserAsync(
		string email,
		string firstName,
		string lastName,
		string? middleName = null,
		bool isActive = true,
		bool isApproved = true,
		bool assignedToAts = true)
	{
		var userId = Guid.CreateVersion7();
		await _authDbContext.AuthUsers.AddAsync(new Authusers
		{
			Id = userId,
			Email = email,
			PasswordHash = "integration-test-password-hash",
			FirstName = firstName,
			MiddleName = middleName,
			LastName = lastName,
			IsActive = isActive,
			IsApproved = isApproved,
			CreatedAt = DateTime.UtcNow
		});
		await _authDbContext.SaveChangesAsync();

		if (assignedToAts)
		{
			var (applicationId, roleId) = await GetAuthAccessMetadataAsync();
			await _authDbContext.AuthUserAppRoles.AddAsync(new AuthUserAppRole
			{
				UserId = userId,
				AppId = applicationId,
				Submenu = 7,
				RoleId = roleId,
				AssignedBy = userId,
				AssignedAt = DateTime.UtcNow
			});
			await _authDbContext.SaveChangesAsync();
		}

		await _hybridCache.RemoveByTagAsync("users");
		await _hybridCache.RemoveByTagAsync("appsubroles");
		return userId;
	}

	private void SetAtsScope(int roleId, int? clientId, bool isPlatformSuperAdmin = false)
	{
		var claims = new List<Claim>
		{
			new(ClaimTypes.NameIdentifier, Guid.CreateVersion7().ToString()),
			new(AuthClaimTypes.AtsRoleId, roleId.ToString())
		};
		if (clientId.HasValue)
			claims.Add(new Claim(AuthClaimTypes.AtsClientId, clientId.Value.ToString()));
		if (isPlatformSuperAdmin)
			claims.Add(new Claim(AuthClaimTypes.PlatformRoleId, PlatformRoleIds.SuperAdmin.ToString()));

		_httpContextAccessor.HttpContext!.User =
			new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
	}

	private async Task<(int ApplicationId, int RoleId)> GetAuthAccessMetadataAsync()
	{
		var application = await _authDbContext.AuthApplications
			.FirstOrDefaultAsync(item => item.AppName == "ATS Integration Tests");
		if (application is null)
		{
			application = new AuthApplication
			{
				AppName = "ATS Integration Tests",
				Description = "ATS integration-test application",
				IsActive = true,
				CreatedAt = DateTime.UtcNow
			};
			await _authDbContext.AuthApplications.AddAsync(application);
		}

		var role = await _authDbContext.AuthRoles
			.FirstOrDefaultAsync(item => item.RoleName == "ATS Integration User");
		if (role is null)
		{
			role = new AuthRole
			{
				RoleName = "ATS Integration User",
				Description = "ATS integration-test role",
				CreatedAt = DateTime.UtcNow
			};
			await _authDbContext.AuthRoles.AddAsync(role);
		}

		await _authDbContext.SaveChangesAsync();
		return (application.AppId, role.RoleId);
	}

	private async Task<int> AddRoleAsync(string name, bool isActive = true)
	{
		await _roleManagementService.AddRoleAsync(new AddRoleDTO
		{
			RoleName = name,
			RoleDescription = $"Description for {name}",
			IsActive = isActive
		});

		return await _dbContext.RoleDetails
			.AsNoTracking()
			.Where(role => role.RoleName == name)
			.Select(role => role.RoleId)
			.SingleAsync();
	}

	private async Task<int> AddModuleAsync(string name, bool isActive = true)
	{
		await _moduleManagementService.AddModuleAsync(new AddModuleDTO
		{
			ModuleName = name,
			ModuleDescription = $"Description for {name}",
			IsActive = isActive
		});

		return await _dbContext.ModuleDetails
			.AsNoTracking()
			.Where(module => module.ModuleName == name)
			.Select(module => module.ModuleId)
			.SingleAsync();
	}

	private async Task<int> AddClientAsync(string name, bool isActive = true)
	{
		var packageId = await AddPackageAsync($"{name} Package");
		await _clientManagementService.AddClientAsync(
			[new AddClientDTO
			{
				ClientName = name,
				ClientDescription = $"Description for {name}",
				IsActive = isActive,
				PackageId = packageId
			}],
			CancellationToken.None);

		return await _dbContext.ClientDetails
			.AsNoTracking()
			.Where(client => client.ClientName == name)
			.Select(client => client.ClientId)
			.SingleAsync();
	}

	private async Task<int> AddPackageAsync(string name)
	{
		await _packageManagementService.AddPackageAsync(new AddPackageDTO
		{
			PackageName = name,
			PackageDescription = $"Description for {name}",
			IsActive = true,
			FollowUpEmail = 1
		}, CancellationToken.None);

		return await _dbContext.PackageDetails
			.AsNoTracking()
			.Where(package => package.PackageName == name)
			.Select(package => package.PackageId)
			.SingleAsync();
	}

	private static AddUserDTO[] CreateAddRequest(
		Guid userId,
		int roleId,
		int? clientId,
		string site,
		bool isActive,
		params int[] moduleIds) =>
		moduleIds.Select(moduleId => new AddUserDTO
		{
			UserId = userId,
			UserName = "Untrusted Request Name",
			UserEmail = "untrusted.request@example.com",
			IsActive = isActive,
			ClientId = clientId,
			Site = site,
			RoleId = roleId,
			ModuleId = moduleId
		}).ToArray();

	private static EditUserDTO[] CreateEditRequest(
		Guid userId,
		int roleId,
		int? clientId,
		string site,
		bool isActive,
		params int[] moduleIds) =>
		moduleIds.Select(moduleId => new EditUserDTO
		{
			UserId = userId,
			UserName = "Untrusted Edited Name",
			UserEmail = "untrusted.edited@example.com",
			IsActive = isActive,
			ClientId = clientId,
			Site = site,
			RoleId = roleId,
			ModuleId = moduleId
		}).ToArray();
}
