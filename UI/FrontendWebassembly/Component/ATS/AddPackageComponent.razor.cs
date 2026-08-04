namespace FrontendWebassembly.Component.ATS;

public partial class AddPackageComponent
{
	private MudForm? AddPackageForm;

	[CascadingParameter] IMudDialogInstance? AddPackageDialog { get; set; }

	[Parameter]
	public AddPackageDTO Package { get; set; } = new AddPackageDTO { IsActive = true };

	void Cancel() => AddPackageDialog!.Cancel();

	async Task Submit()
	{
		await AddPackageForm!.ValidateAsync();
		if (AddPackageForm!.IsValid)
		{
			AddPackageDialog!.Close(DialogResult.Ok(Package));
		}
	}
}
