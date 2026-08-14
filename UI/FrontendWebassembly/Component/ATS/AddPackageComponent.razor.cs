namespace FrontendWebassembly.Component.ATS;

public partial class AddPackageComponent
{
	private MudForm? AddPackageForm;

	[CascadingParameter] IMudDialogInstance? AddPackageDialog { get; set; }

	[Parameter]
	public AddPackageDTO Package { get; set; } = new AddPackageDTO { IsActive = true };

	private int DescriptionLength => Package.PackageDescription?.Length ?? 0;

	void Cancel() => AddPackageDialog!.Cancel();

	private void ToggleStatus() => Package.IsActive = !Package.IsActive;

	async Task Submit()
	{
		await AddPackageForm!.ValidateAsync();
		if (AddPackageForm!.IsValid)
		{
			AddPackageDialog!.Close(DialogResult.Ok(Package));
		}
	}
}
