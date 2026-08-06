namespace FrontendWebassembly.Component.ATS;

public partial class EditClientComponent
{
	private MudForm? EditClientForm;

	[CascadingParameter]
	IMudDialogInstance? EditClientDialog { get; set; }

	[Parameter]
	public ClientDetailsDTO Client { get; set; } = new();

	private EditClientDTO EditClient = new();

	protected override void OnParametersSet()
	{
		EditClient = new EditClientDTO
		{
			ClientId = Client.ClientId,
			ClientName = Client.ClientName,
			IsActive = Client.IsActive
		};
	}

	void Cancel() => EditClientDialog!.Cancel();

	async Task Submit()
	{
		await EditClientForm!.ValidateAsync();
		if (EditClientForm!.IsValid)
		{
			EditClientDialog!.Close(DialogResult.Ok(EditClient));
		}
	}
}
