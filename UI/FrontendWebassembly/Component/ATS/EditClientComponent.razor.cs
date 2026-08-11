namespace FrontendWebassembly.Component.ATS;

public partial class EditClientComponent
{
	private MudForm? EditClientForm;

	[CascadingParameter]
	IMudDialogInstance? EditClientDialog { get; set; }

	[Parameter]
	public ClientManagementViewModel Client { get; set; } = new();

	[Parameter]
	public IReadOnlyList<PackageDetailsDTO> Packages { get; set; } = Array.Empty<PackageDetailsDTO>();

	private EditClientDTO EditClient = new();
	private IReadOnlyCollection<int> SelectedPackageIds { get; set; } = new HashSet<int>();
	private string? PackageError { get; set; }
	private int DescriptionLength => EditClient.ClientDescription?.Length ?? 0;
	private IEnumerable<PackageDetailsDTO> SelectedPackages => Packages
		.Where(package => SelectedPackageIds.Contains(package.PackageId))
		.OrderBy(package => package.PackageName);

	protected override void OnParametersSet()
	{
		EditClient = new EditClientDTO
		{
			ClientId = Client.ClientId,
			ClientName = Client.ClientName,
			ClientDescription = Client.ClientDescription,
			IsActive = Client.IsActive
		};
		SelectedPackageIds = Client.Packages.Select(package => package.PackageId).ToHashSet();
	}

	void Cancel() => EditClientDialog!.Cancel();

	private void ToggleStatus() => EditClient.IsActive = !EditClient.IsActive;

	async Task Submit()
	{
		await EditClientForm!.ValidateAsync();
		var packageIds = SelectedPackageIds.Distinct().ToHashSet();
		PackageError = packageIds.Count == 0 ? "At least one package is required" : null;

		if (EditClientForm!.IsValid && PackageError is null)
		{
			EditClient.PackageIds = packageIds;
			EditClientDialog!.Close(DialogResult.Ok(EditClient));
		}
	}

	private void OnSelectedPackageIdsChanged(IEnumerable<int> packageIds)
	{
		SelectedPackageIds = packageIds.Distinct().ToArray();
		PackageError = SelectedPackageIds.Count == 0 ? "At least one package is required" : null;
	}

	private void TogglePackage(int packageId)
	{
		var selectedIds = SelectedPackageIds.ToHashSet();
		if (!selectedIds.Add(packageId))
			selectedIds.Remove(packageId);

		OnSelectedPackageIdsChanged(selectedIds);
	}

	private void RemovePackage(int packageId)
	{
		var selectedIds = SelectedPackageIds.ToHashSet();
		selectedIds.Remove(packageId);
		OnSelectedPackageIdsChanged(selectedIds);
	}
}
