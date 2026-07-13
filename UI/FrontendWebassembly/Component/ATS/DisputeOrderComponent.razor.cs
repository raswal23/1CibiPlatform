namespace FrontendWebassembly.Component.ATS;

public partial class DisputeOrderComponent
{
  private TableComponent<DisputeOrderListDTO>? ordersTable;
	private string? _searchString;

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
		
}
