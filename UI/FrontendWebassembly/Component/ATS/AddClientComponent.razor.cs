namespace FrontendWebassembly.Component.ATS;

public partial class AddClientComponent
{
	private MudForm? AddClientForm;

	[CascadingParameter] IMudDialogInstance? AddClientDialog { get; set; }

	[Parameter]
	public AddClientDTO Client { get; set; } = new AddClientDTO { IsActive = true };

	[Parameter]
	public IReadOnlyList<PackageDetailsDTO> Packages { get; set; } = Array.Empty<PackageDetailsDTO>();

	private IReadOnlyCollection<int> SelectedPackageIds { get; set; } = new HashSet<int>();
	private string? PackageError { get; set; }
	private int DescriptionLength => Client.ClientDescription?.Length ?? 0;
	private IEnumerable<PackageDetailsDTO> SelectedPackages => Packages
		.Where(package => SelectedPackageIds.Contains(package.PackageId))
		.OrderBy(package => package.PackageName);

	void Cancel() => AddClientDialog!.Cancel();

	private void ToggleStatus() => Client.IsActive = !Client.IsActive;

	async Task Submit()
	{
		await AddClientForm!.ValidateAsync();
		var packageIds = SelectedPackageIds.Distinct().ToHashSet();
		PackageError = packageIds.Count == 0 ? "At least one package is required" : null;

		if (AddClientForm!.IsValid && PackageError is null)
		{
			Client.PackageIds = packageIds;
			AddClientDialog!.Close(DialogResult.Ok(Client));
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
