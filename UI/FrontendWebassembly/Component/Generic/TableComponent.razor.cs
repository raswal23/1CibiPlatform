namespace FrontendWebassembly.Component.Generic;

public partial class TableComponent<TItem>
{
	[Parameter] public string? Title { get; set; }
	[Parameter] public bool EnableSearch { get; set; } = true;
	[Parameter] public string? AddButtonText { get; set; }
	[Parameter] public RenderFragment? NoRecordsTemplate { get; set; }
	[Parameter] public EventCallback OnAddClicked { get; set; }
	private bool UseLoadingContentBody { get; set; } = true;

    [Parameter] public int? RowsPerPage { get; set; }
	[Parameter] public Func<TableState, CancellationToken, Task<TableData<TItem>>>? LoadServerData { get; set; }

	[Parameter] public RenderFragment? HeaderTemplate { get; set; }
	[Parameter] public RenderFragment<TItem>? RowTemplate { get; set; }
	[Parameter] public int ColumnCount { get; set; }
	public MudTable<TItem>? TableRef { get; private set; }

	[Parameter]
	public string? SearchString { get; set; }

	[Parameter]
	public IEnumerable<TItem>? Items { get; set; }

	[Parameter]
	public EventCallback<string> SearchStringChanged { get; set; }

	private async Task SearchChanged(string value)
	{
		SearchString = value;
		if (SearchStringChanged.HasDelegate)
			await SearchStringChanged.InvokeAsync(value);
		if (TableRef != null)
		{
			await TableRef.ReloadServerData();
		}
	}


}
