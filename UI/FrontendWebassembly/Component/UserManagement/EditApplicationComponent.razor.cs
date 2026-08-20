using FrontendWebassembly.ShareData.Auth;

namespace FrontendWebassembly.Component.UserManagement;

public partial class EditApplicationComponent
{
	private MudForm? EditApplicationForm;

	[CascadingParameter]
	IMudDialogInstance? EditApplicationDialog { get; set; }

	[Parameter]
	public ApplicationsDTO Application { get; set; } = new();

	private ApplicationsDTO EditApplication = new();

	protected override void OnParametersSet()
	{
		EditApplication = new ApplicationsDTO
		{
			applicationId = Application.applicationId,
			applicationName = Application.applicationName,
			Description = Application.Description,
			IsActive = Application.IsActive
		};
	}

	private Task<IEnumerable<string>> SearchApplications(string value, CancellationToken cancellationToken)
	{
		var result = ApplicationListDescriptionIcon.List.Values
			.Select(x => x.Name);

		if (!string.IsNullOrWhiteSpace(value))
		{
			result = result.Where(x =>
				x.Contains(value, StringComparison.OrdinalIgnoreCase));
		}

		return Task.FromResult(result);
	}

	void Cancel() => EditApplicationDialog!.Cancel();

	private void ToggleStatus() => EditApplication.IsActive = !EditApplication.IsActive;

	async Task Submit()
	{
		await EditApplicationForm!.ValidateAsync();
		if (EditApplicationForm!.IsValid)
		{
			EditApplicationDialog!.Close(DialogResult.Ok(EditApplication));
		}
	}
}
