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

	void Cancel() => AddClientDialog!.Cancel();

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

	private string GetSelectedPackagesText(IReadOnlyList<string> selectedValues)
	{
		var selectedIds = SelectedPackageIds.ToHashSet();
		return string.Join(", ", Packages
			.Where(package => selectedIds.Contains(package.PackageId))
			.Select(package => package.PackageName));
	}
}
