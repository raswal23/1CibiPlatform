namespace FrontendWebassembly.Component.ATS;

public partial class EditClientAssignmentComponent
{
	private MudForm? assignmentForm;
	private ClientLookupDTO? selectedClient;
	private bool isSubmitting;

	[CascadingParameter]
	private IMudDialogInstance? Dialog { get; set; }

	[Inject]
	private IClientAssignmentService ClientAssignmentService { get; set; } = default!;

	[Inject]
	private ISnackbar Snackbar { get; set; } = default!;

	[Parameter]
	public ClientAssignmentDetailsDTO CurrentAssignment { get; set; } = new();

	private bool CanSave =>
		!isSubmitting &&
		selectedClient is not null &&
		selectedClient.ClientId != CurrentAssignment.ClientId;

	private string UserInitial => string.IsNullOrWhiteSpace(CurrentAssignment.UserName)
		? "?"
		: char.ToUpperInvariant(CurrentAssignment.UserName[0]).ToString();

	protected override void OnParametersSet()
	{
		selectedClient = CurrentAssignment.ClientId.HasValue
			? new ClientLookupDTO
			{
				ClientId = CurrentAssignment.ClientId.Value,
				ClientName = CurrentAssignment.ClientName ?? $"Client #{CurrentAssignment.ClientId}"
			}
			: null;
	}

	private void Cancel()
	{
		if (!isSubmitting)
			Dialog?.Cancel();
	}

	private void OnClientChanged(ClientLookupDTO? client) => selectedClient = client;

	private async Task<IEnumerable<ClientLookupDTO>> SearchClients(
		string value,
		CancellationToken cancellationToken)
	{
		// First page only: the autocomplete narrows by search term instead of paging.
		var response = await ClientAssignmentService.GetAssignableClientsAsync(
			null,
			25,
			value,
			cancellationToken);

		if (!response.IsSuccess || response.Data is null)
			return Array.Empty<ClientLookupDTO>();

		return response.Data.Items;
	}

	private static string GetClientText(ClientLookupDTO? client) =>
		client?.ClientName ?? string.Empty;

	private async Task Submit()
	{
		if (isSubmitting)
			return;

		await assignmentForm!.ValidateAsync();
		if (!assignmentForm.IsValid || !CanSave)
			return;

		isSubmitting = true;
		try
		{
			var response = await ClientAssignmentService.AssignClientAsync(
				new AssignATSUserClientDTO
				{
					UserId = CurrentAssignment.UserId,
					ClientId = selectedClient!.ClientId
				});

			if (!response.IsSuccess)
			{
				Snackbar.Add(response.ErrorDetail, Severity.Error);
				return;
			}

			Dialog?.Close(DialogResult.Ok(response.Data));
		}
		finally
		{
			isSubmitting = false;
		}
	}
}
