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

	private string GetSelectedPackagesText(IReadOnlyList<string> selectedValues)
	{
		var selectedIds = SelectedPackageIds.ToHashSet();
		return string.Join(", ", Packages
			.Where(package => selectedIds.Contains(package.PackageId))
			.Select(package => package.PackageName));
	}
}
