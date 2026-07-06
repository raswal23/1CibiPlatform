using FrontendWebassembly.ShareData.Auth;

namespace FrontendWebassembly.Component.UserManagement;

public partial class AddApplicationComponent
{
	private MudForm? AddApplicationForm;

	[CascadingParameter] IMudDialogInstance? AddApplicationDialog { get; set; }

	[Parameter]
	public AddApplicationDTO Application { get; set; } = new AddApplicationDTO { IsActive = true };

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

	void Cancel() => AddApplicationDialog!.Cancel();

	async Task Submit()
	{
		await AddApplicationForm!.ValidateAsync();
		if (AddApplicationForm!.IsValid)
		{
			AddApplicationDialog!.Close(DialogResult.Ok(Application));
		}
	}
}
