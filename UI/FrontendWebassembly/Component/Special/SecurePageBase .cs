namespace FrontendWebassembly.Component.Special;

using FrontendWebassembly.Services.Auth.Shared;
using Microsoft.AspNetCore.Components;
using System.Reflection;

public abstract class SecurePageBase : ComponentBase
{
	[Inject] protected NavigationManager Nav { get; set; } = default!;
	[Inject] protected IAccessService AccessService { get; set; } = default!;
	[Inject] protected IATSUserManagementService ATSUserManagementService { get; set; } = default!;
	[CascadingParameter(Name = "ATSAccessibleModuleIds")]
	protected IReadOnlySet<int>? CascadingATSModuleIds { get; set; }

	protected IReadOnlySet<int> AccessibleATSModuleIds { get; private set; } = new HashSet<int>();
	protected bool IsPageAuthorized { get; private set; }

	protected override async Task OnInitializedAsync()
	{
		IsPageAuthorized = false;
		AccessibleATSModuleIds = CascadingATSModuleIds ?? new HashSet<int>();

		var permissionAttr = GetType().GetCustomAttribute<RequirePermissionAttribute>();
		if (permissionAttr is not null &&
			!await AccessService.HasAccessAsync(permissionAttr.AppId, permissionAttr.SubMenuId))
		{
			Nav.NavigateTo("/access-denied");
			return;
		}

		var moduleAttr = GetType().GetCustomAttribute<RequireATSModuleAttribute>();
		if (moduleAttr is not null)
		{
			if (CascadingATSModuleIds is null)
			{
				var moduleIdsResponse = await ATSUserManagementService.GetMyModuleIdsAsync();

				// Fail closed: an unavailable module list means no module access, which
				// falls through to the access-denied redirect below.
				AccessibleATSModuleIds = moduleIdsResponse.IsSuccess && moduleIdsResponse.Data is not null
					? moduleIdsResponse.Data.ToHashSet()
					: new HashSet<int>();
			}

			if (moduleAttr.ModuleIds.Count == 0 ||
				!moduleAttr.ModuleIds.Any(AccessibleATSModuleIds.Contains))
			{
				Nav.NavigateTo("/access-denied");
				return;
			}
		}

		IsPageAuthorized = true;
	}
}
