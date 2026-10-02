using FluentAssertions;
using FrontendWebassembly.ShareData.ATS;

namespace Test.UI;

/// <summary>
/// The ATS module-access matrix: which modules each role may be granted. Every role sees the
/// same eight operational modules; Platform Manager adds the whole administration set and
/// Client Admin adds only User Management. Mirrors the ATS module access sheet, and covers
/// <see cref="ModuleList.IsInRoleModuleSet"/> plus the role half of
/// <see cref="ModuleList.IsSelectableForUserRole"/> used by the Add/Edit User dialogs.
/// </summary>
public class AtsModuleListTests
{
	// Dashboard, New Order, Orders & Reports, Disputes, Withdrawn, AI Assistant,
	// Bulk Uploads Status, Ticketing Status.
	private static readonly int[] SharedModuleIds = [1, 2, 3, 4, 5, 12, 13, 14];

	// Everything the shared eight leave out.
	private static readonly int[] AdministrationModuleIds = [6, 7, 8, 9, 10, 11, 15, 16];

	public static TheoryData<int> EveryAtsRole => new()
	{
		AtsRoleList.PlatformManagerId,
		AtsRoleList.ClientAdminId,
		AtsRoleList.ServiceDeliveryId,
		AtsRoleList.UserId,
		AtsRoleList.ClientExperienceId
	};

	public static TheoryData<int> NonAdminRoles => new()
	{
		AtsRoleList.ServiceDeliveryId,
		AtsRoleList.UserId,
		AtsRoleList.ClientExperienceId
	};

	[Theory]
	[MemberData(nameof(EveryAtsRole))]
	public void IsInRoleModuleSet_ShouldIncludeEverySharedModule_ForEveryRole(int roleId)
	{
		SharedModuleIds.Should().OnlyContain(moduleId =>
			ModuleList.IsInRoleModuleSet(moduleId, roleId));
	}

	[Fact]
	public void IsInRoleModuleSet_ShouldIncludeEveryModule_ForPlatformManager()
	{
		ModuleList.List.Keys.Should().OnlyContain(moduleId =>
			ModuleList.IsInRoleModuleSet(moduleId, AtsRoleList.PlatformManagerId));
	}

	[Fact]
	public void IsInRoleModuleSet_ShouldAddOnlyUserManagement_ForClientAdmin()
	{
		var beyondShared = ModuleList.List.Keys
			.Where(moduleId => ModuleList.IsInRoleModuleSet(moduleId, AtsRoleList.ClientAdminId))
			.Except(SharedModuleIds);

		beyondShared.Should().BeEquivalentTo([10]);
	}

	[Theory]
	[MemberData(nameof(NonAdminRoles))]
	public void IsInRoleModuleSet_ShouldExcludeAdministrationModules_ForNonAdminRoles(int roleId)
	{
		AdministrationModuleIds.Should().NotContain(moduleId =>
			ModuleList.IsInRoleModuleSet(moduleId, roleId));
	}

	[Theory]
	[InlineData(0)]   // No role picked yet - the dialog opens on "Select a role".
	[InlineData(99)]  // A role this UI does not know about.
	public void IsInRoleModuleSet_ShouldFallBackToTheSharedModules_WhenRoleIsNotRecognised(int roleId)
	{
		ModuleList.List.Keys
			.Where(moduleId => ModuleList.IsInRoleModuleSet(moduleId, roleId))
			.Should().BeEquivalentTo(SharedModuleIds);
	}

	[Fact]
	public void IsSelectableForUserRole_ShouldHideAdministrationModules_WhenActorCannotViewAllModules()
	{
		// A Client Admin actor stays capped by the actor gate even when assigning a
		// Platform Manager, so the role can never widen what the admin is allowed to grant.
		AdministrationModuleIds
			.Where(moduleId => moduleId != 10)
			.Should().NotContain(moduleId => ModuleList.IsSelectableForUserRole(
				moduleId,
				canViewAllModules: false,
				AtsRoleList.PlatformManagerId));
	}

	[Fact]
	public void IsSelectableForUserRole_ShouldOfferEveryModule_WhenSuperAdminAssignsPlatformManager()
	{
		ModuleList.List.Keys.Should().OnlyContain(moduleId => ModuleList.IsSelectableForUserRole(
			moduleId,
			canViewAllModules: true,
			AtsRoleList.PlatformManagerId));
	}

	[Fact]
	public void IsSelectableForUserRole_ShouldNarrowToTheRole_WhenActorCouldGrantMore()
	{
		// The regression this exists for: a Super Admin used to see all sixteen modules
		// regardless of the role chosen, and could grant Package Management to a
		// Client Experience user.
		AdministrationModuleIds.Should().NotContain(moduleId => ModuleList.IsSelectableForUserRole(
			moduleId,
			canViewAllModules: true,
			AtsRoleList.ClientExperienceId));

		SharedModuleIds.Should().OnlyContain(moduleId => ModuleList.IsSelectableForUserRole(
			moduleId,
			canViewAllModules: true,
			AtsRoleList.ClientExperienceId));
	}

	[Fact]
	public void IsSelectableForUserRole_ShouldKeepUserManagement_ForClientAdminTarget()
	{
		ModuleList.IsSelectableForUserRole(
			10,
			canViewAllModules: false,
			AtsRoleList.ClientAdminId).Should().BeTrue();
	}

	[Fact]
	public void IsVisibleForAdministration_ShouldMatchTheClientAdminRoleSet_WhenActorCannotViewAllModules()
	{
		// RestrictedAdministrationModuleIds is maintained by hand as the complement of the
		// Client Admin role set. This fails if a module is added to one and not the other.
		foreach (var moduleId in ModuleList.List.Keys)
		{
			ModuleList.IsVisibleForAdministration(moduleId, canViewAllModules: false)
				.Should().Be(
					ModuleList.IsInRoleModuleSet(moduleId, AtsRoleList.ClientAdminId),
					$"module {moduleId} ({ModuleList.List[moduleId].Name}) disagrees between the actor gate and the Client Admin role set");
		}
	}
}
