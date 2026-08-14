namespace FrontendWebassembly.Pages.ATS;

using FrontendWebassembly.ShareData.ATS;

public partial class ATS
{
	protected override async Task OnInitializedAsync()
	{
		await base.OnInitializedAsync();
		if (!IsPageAuthorized)
			return;

		var firstAccessibleModule = ModuleList.List
			.Where(module => AccessibleATSModuleIds.Contains(module.Key))
			.OrderBy(module => module.Key)
			.Select(module => module.Value.path)
			.FirstOrDefault();
		NavigationManager.NavigateTo(firstAccessibleModule is null
			? "/access-denied"
			: $"/s&i/ats/{firstAccessibleModule}");
	}
}
