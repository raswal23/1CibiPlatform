namespace FrontendWebassembly.Component.ATS;

public partial class AddClientComponent
{
	private MudForm? AddClientForm;

	[CascadingParameter] IMudDialogInstance? AddClientDialog { get; set; }

	[Parameter]
	public AddClientDTO Client { get; set; } = new AddClientDTO { IsActive = true };

	void Cancel() => AddClientDialog!.Cancel();

	async Task Submit()
	{
		await AddClientForm!.ValidateAsync();
		if (AddClientForm!.IsValid)
		{
			AddClientDialog!.Close(DialogResult.Ok(Client));
		}
	}
}
