using FrontendWebassembly.ShareData.ATS;

namespace FrontendWebassembly.Component.ATS;

public partial class EditModuleComponent
{
	private MudForm? EditModuleForm;

	[CascadingParameter]
	IMudDialogInstance? EditModuleDialog { get; set; }

	[Parameter]
	public ModuleDetailsDTO Module { get; set; } = new();

	private EditATSModuleDTO EditModule = new();

	private Task<IEnumerable<string>> SearchModules(string value, CancellationToken cancellationToken)
	{
		var result = ModuleList.List.Values
			.Select(x => x.Name);

		if (!string.IsNullOrWhiteSpace(value))
		{
			result = result.Where(x =>
				x.Contains(value, StringComparison.OrdinalIgnoreCase));
		}

		return Task.FromResult(result);
	}

	protected override void OnParametersSet()
	{
		EditModule = new EditATSModuleDTO
		{
			ModuleId = Module.ModuleId,
			ModuleName = Module.ModuleName,
			ModuleDescription = Module.ModuleDescription,
			IsActive = Module.IsActive
		};
	}

	void Cancel() => EditModuleDialog!.Cancel();

	async Task Submit()
	{
		await EditModuleForm!.ValidateAsync();
		if (EditModuleForm.IsValid)
		{
			EditModuleDialog!.Close(DialogResult.Ok(EditModule));
		}
	}
}
