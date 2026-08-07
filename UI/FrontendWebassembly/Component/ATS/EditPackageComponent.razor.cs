namespace FrontendWebassembly.Component.ATS;

public partial class EditPackageComponent
{
	private MudForm? EditPackageForm;

	[CascadingParameter]
	IMudDialogInstance? EditPackageDialog { get; set; }

	[Parameter]
	public PackageDetailsDTO Package { get; set; } = new();

	private EditPackageDTO EditPackage = new();

	protected override void OnParametersSet()
	{
		EditPackage = new EditPackageDTO
		{
			PackageId = Package.PackageId,
			PackageName = Package.PackageName,
			PackageDescription = Package.PackageDescription,
			IsActive = Package.IsActive,
			FollowUpEmail = Package.FollowUpEmail
		};
	}

	void Cancel() => EditPackageDialog!.Cancel();

	async Task Submit()
	{
		await EditPackageForm!.ValidateAsync();
		if (EditPackageForm!.IsValid)
		{
			EditPackageDialog!.Close(DialogResult.Ok(EditPackage));
		}
	}
}
