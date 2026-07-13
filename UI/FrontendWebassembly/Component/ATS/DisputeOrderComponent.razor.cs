namespace FrontendWebassembly.Component.ATS;

public partial class DisputeOrderComponent
{
  private TableComponent<DisputeOrderListDTO>? ordersTable;
	private string? _searchString;
	private bool isMarkingAsDisputed = false;

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

   private async Task MarkAsDisputed(DisputeOrderListDTO order)
	{
     if (order.IsDisputed)
		{
			return;
		}

		var confirmParam = new DialogParameters
		{
			{ nameof(ConfirmationDialogComponent.Message), "Do you want to mark this order as disputed?" }
		};

		var dialog = await DialogService.ShowAsync<ConfirmationDialogComponent>("Confirmation", confirmParam);
		var result = await dialog.Result;

		if (result!.Canceled)
		{
			if (ordersTable?.TableRef != null)
				await ordersTable.TableRef.ReloadServerData();
			return;
		}

		try
		{
			isMarkingAsDisputed = true;
			await InvokeAsync(StateHasChanged);

			var success = await DisputeOrderService.MarkAsDisputedAsync(order.EmailInvitationID);

			if (!success)
			{
				Snackbar.Add("Failed to mark order as disputed.", Severity.Error);
				return;
			}

			Snackbar.Add("Order marked as disputed successfully.", Severity.Success);

			if (ordersTable?.TableRef != null)
				await ordersTable.TableRef.ReloadServerData();
		}
		catch (Exception)
		{
			Snackbar.Add("Failed to mark order as disputed.", Severity.Error);
		}
		finally
		{
			isMarkingAsDisputed = false;
			await InvokeAsync(StateHasChanged);
		}
	}
		
}
