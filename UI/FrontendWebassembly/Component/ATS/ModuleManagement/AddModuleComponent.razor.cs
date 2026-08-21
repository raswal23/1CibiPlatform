using FrontendWebassembly.ShareData.ATS;

namespace FrontendWebassembly.Component.ATS;

public partial class AddModuleComponent
{
	private const string ATSRoleIdStorageKey = "ATSRoleId";
	private MudForm? AddModuleForm;
	private bool _canViewAllModules;

	[Inject]
	private IAccessService AccessService { get; set; } = default!;

	[Inject]
	private LocalStorageService LocalStorageService { get; set; } = default!;

	[CascadingParameter] IMudDialogInstance? AddModuleDialog { get; set; }

	[Parameter]
	public AddATSModuleDTO Module { get; set; } = new() { IsActive = true };

	private int DescriptionLength => Module.ModuleDescription?.Length ?? 0;

	protected override async Task OnInitializedAsync()
	{
		var isPlatformSuperAdmin = await AccessService.HasRoleAsync(RoleList.SuperAdminId);
		var atsRoleId = await GetStoredATSRoleIdAsync();
		_canViewAllModules = isPlatformSuperAdmin || atsRoleId == 1;
	}

	private async Task<int> GetStoredATSRoleIdAsync()
	{
		try
		{
			return await LocalStorageService.GetItemAsync<int>(ATSRoleIdStorageKey);
		}
		catch (JsonException)
		{
			return 0;
		}
	}

	private Task<IEnumerable<string>> SearchModules(string value, CancellationToken cancellationToken)
	{
		var result = ModuleList.List
			.Where(module => ModuleList.IsVisibleForAdministration(module.Key, _canViewAllModules))
			.Select(module => module.Value.Name);

		if (!string.IsNullOrWhiteSpace(value))
		{
			result = result.Where(x =>
				x.Contains(value, StringComparison.OrdinalIgnoreCase));
		}

		return Task.FromResult(result);
	}

	void Cancel() => AddModuleDialog!.Cancel();

	private void ToggleStatus() => Module.IsActive = !Module.IsActive;

	async Task Submit()
	{
		await AddModuleForm!.ValidateAsync();
		if (AddModuleForm.IsValid)
		{
			AddModuleDialog!.Close(DialogResult.Ok(Module));
		}
	}
}
