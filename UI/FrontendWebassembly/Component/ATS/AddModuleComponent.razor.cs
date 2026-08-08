using FrontendWebassembly.ShareData.ATS;

namespace FrontendWebassembly.Component.ATS;

public partial class AddModuleComponent
{
	private MudForm? AddModuleForm;

	[CascadingParameter] IMudDialogInstance? AddModuleDialog { get; set; }

	[Parameter]
	public AddATSModuleDTO Module { get; set; } = new() { IsActive = true };

	private int DescriptionLength => Module.ModuleDescription?.Length ?? 0;

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
