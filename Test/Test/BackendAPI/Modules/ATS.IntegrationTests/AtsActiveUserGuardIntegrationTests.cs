using ATS.Data.Entities;
using BuildingBlocks.Exceptions;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using Test.BackendAPI.Infrastructure.ATS.Infrastracture;

namespace Test.BackendAPI.Modules.ATS.IntegrationTests;

public class AtsActiveUserGuardIntegrationTests : BaseIntegrationTest
{
	public AtsActiveUserGuardIntegrationTests(IntegrationTestWebAppFactory factory)
		: base(factory)
	{
	}

	// The fake IHttpContextAccessor is scoped, so the principal (and its random
	// UserId) is stable within one test's scope. The test principal carries an
	// AtsRoleId claim but no platformRoleId, so the super-admin bypass does not
	// apply and the guard has to consult the database.
	private Guid CurrentPrincipalId()
	{
		var userIdValue = _httpContextAccessor.HttpContext!.User
			.FindFirstValue(ClaimTypes.NameIdentifier);
		return Guid.Parse(userIdValue!);
	}

	[Fact]
	public async Task EnsureActiveAtsUserAsync_ShouldThrowForbidden_WhenCallerHasNoAtsAccount()
	{
		Func<Task> act = () => _atsActiveUserGuard.EnsureActiveAtsUserAsync(CancellationToken.None);

		await act.Should().ThrowAsync<ForbiddenException>()
			.WithMessage("The current user does not have valid ATS access.");
	}

	[Fact]
	public async Task EnsureActiveAtsUserAsync_ShouldPass_WhenCallerHasAnActiveAtsAccount()
	{
		await SeedAtsUserAsync(CurrentPrincipalId(), isActive: true);

		Func<Task> act = () => _atsActiveUserGuard.EnsureActiveAtsUserAsync(CancellationToken.None);

		await act.Should().NotThrowAsync();
	}

	[Fact]
	public async Task EnsureActiveAtsUserAsync_ShouldThrowForbidden_WhenCallerAccountIsDisabled()
	{
		await SeedAtsUserAsync(CurrentPrincipalId(), isActive: false);

		Func<Task> act = () => _atsActiveUserGuard.EnsureActiveAtsUserAsync(CancellationToken.None);

		await act.Should().ThrowAsync<ForbiddenException>()
			.WithMessage("The current user does not have valid ATS access.");
	}

	// UserDetails carries FKs to role and module, so both are seeded first.
	private async Task SeedAtsUserAsync(Guid userId, bool isActive)
	{
		var now = DateTime.UtcNow;
		var role = new RoleDetails
		{
			RoleName = "Guard Gate Role",
			RoleDescription = "Seeded for the active-user gate",
			IsActive = true,
			CreatedAt = now,
			UpdatedAt = now
		};
		var module = new ModuleDetails
		{
			ModuleName = "Guard Gate Module",
			ModuleDescription = "Seeded for the active-user gate",
			IsActive = true,
			CreatedAt = now,
			UpdatedAt = now
		};
		_dbContext.RoleDetails.Add(role);
		_dbContext.ModuleDetails.Add(module);
		await _dbContext.SaveChangesAsync();

		_dbContext.UserDetails.Add(new UserDetails
		{
			UserId = userId,
			UserName = "Guard Gate User",
			UserEmail = "guard.gate@cibi.com.ph",
			IsActive = isActive,
			Site = "Integration Test Site",
			RoleId = role.RoleId,
			ModuleId = module.ModuleId,
			CreatedAt = now,
			UpdatedAt = now
		});
		await _dbContext.SaveChangesAsync();
	}
}
