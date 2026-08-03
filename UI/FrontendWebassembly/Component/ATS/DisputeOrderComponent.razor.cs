namespace FrontendWebassembly.Component.ATS;

public partial class DisputeOrderComponent
{
  private TableComponent<DisputeOrderListDTO>? ordersTable;
	private string? _searchString;

	private Guid? _loadingDisputeId;
	private string searchString
	{
		get => _searchString!;
		set => UpdateSearch(ref _searchString!, value, ordersTable!);
	}

    private void UpdateSearch<T>(ref string field, string value, TableComponent<T> table) where T : class
	{
		if (field != value)
		{
			field = value;
			table?.TableRef!.ReloadServerData();
		}
	}

	private async Task<TableData<DisputeOrderListDTO>> LoadOrderData(TableState state, CancellationToken cancellationToken)
	=> await LoadPagedDataAsync(state, (page, pageSize) =>
				DisputeOrderService.GetDisputeOrdersAsync(page, pageSize, searchString));

	private async Task OpenResultDialog<TComponent>(
		string title,
		DialogParameters? parameters = null,
		MaxWidth maxWidth = MaxWidth.Small,
		bool fullWidth = true,
		bool noHeader = true)
		where TComponent : IComponent
	{
		var options = new DialogOptions
		{
			CloseButton = !noHeader,
			NoHeader = noHeader,
			MaxWidth = maxWidth,
			FullWidth = fullWidth
		};

		var dialog = await DialogService.ShowAsync<TComponent>(
			title,
			parameters ?? new DialogParameters(),
			options);

		await dialog.Result;
	}


	private async Task MarkAsDisputed(Guid emailInvitationId, DateTime? orderCreatedAt, string subjectName)
	{
		var confirmParam = new DialogParameters
		{
			{ nameof(DisputeDialogOrderComponent.EmailInvitationId), emailInvitationId },
			{ nameof(DisputeDialogOrderComponent.OrderCreatedAt), orderCreatedAt },
			{ nameof(DisputeDialogOrderComponent.SubjectName), subjectName }
		};

		await OpenResultDialog<DisputeDialogOrderComponent>("", confirmParam);

		try
		{
			_loadingDisputeId = emailInvitationId;
			StateHasChanged();

			if(ordersTable?.TableRef != null)
				await ordersTable.TableRef.ReloadServerData();

			Snackbar.Add("Dispute reason submitted successfully.", Severity.Success);
		}
		finally
		{
			_loadingDisputeId = null;
			StateHasChanged();
		}
	}
}
