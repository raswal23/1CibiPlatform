namespace FrontendWebassembly.Component.ATS;

public partial class EditPackageComponent
{
	private MudForm? EditPackageForm;

	[CascadingParameter]
	IMudDialogInstance? EditPackageDialog { get; set; }

	[Parameter]
	public PackageDetailsDTO Package { get; set; } = new();

	private EditPackageDTO EditPackage = new();
	private int DescriptionLength => EditPackage.PackageDescription?.Length ?? 0;

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

	private void ToggleStatus() => EditPackage.IsActive = !EditPackage.IsActive;

	async Task Submit()
	{
		await EditPackageForm!.ValidateAsync();
		if (EditPackageForm!.IsValid)
		{
			EditPackageDialog!.Close(DialogResult.Ok(EditPackage));
		}
	}
}
