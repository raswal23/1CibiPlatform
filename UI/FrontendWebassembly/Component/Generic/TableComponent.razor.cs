namespace FrontendWebassembly.Component.Generic;

public partial class TableComponent<TItem>
{
	[Parameter] public string? Title { get; set; }
	[Parameter] public string? TitleId { get; set; }
	[Parameter] public string? TableAriaLabel { get; set; }
	[Parameter] public string? ContainerClass { get; set; }
	[Parameter] public string? TableClass { get; set; }
	[Parameter] public bool EnableSearch { get; set; }
	[Parameter] public bool EnableReload { get; set; }
	[Parameter] public string ReloadAriaLabel { get; set; } = "Reload table";
	[Parameter] public EventCallback OnReloadClicked { get; set; }
	[Parameter] public string? SearchLabel { get; set; } = "Search";
	[Parameter] public string? SearchPlaceholder { get; set; }
	// Apply the search when the user presses Enter or leaves the field,
	// rather than reloading the server table on every keystroke.
	[Parameter] public bool SearchImmediate { get; set; } = false;
	[Parameter] public int SearchDebounceInterval { get; set; } = 300;
	[Parameter] public string? AddButtonText { get; set; }
	[Parameter] public RenderFragment? NoRecordsTemplate { get; set; }
	[Parameter] public EventCallback OnAddClicked { get; set; }
	[Parameter] public int? RowsPerPage { get; set; }
	[Parameter] public int[] PageSizeOptions { get; set; } = [10, 25, 50];
	[Parameter] public string RowsPerPageString { get; set; } = "Rows per page:";
	[Parameter] public string PagerInfoFormat { get; set; } = "{first_item}-{last_item} of {all_items}";
	[Parameter] public Breakpoint ResponsiveBreakpoint { get; set; } = Breakpoint.Sm;
	[Parameter] public Func<TableState, CancellationToken, Task<TableData<TItem>>>? LoadServerData { get; set; }
	[Parameter] public RenderFragment? HeaderTemplate { get; set; }
	[Parameter] public RenderFragment<TItem>? RowTemplate { get; set; }
	[Parameter] public int ColumnCount { get; set; }
	[Parameter] public string? SearchString { get; set; }
	[Parameter] public IEnumerable<TItem>? Items { get; set; }
	[Parameter] public EventCallback<string> SearchStringChanged { get; set; }
	[Parameter] public RenderFragment? ToolBarLeft { get; set; }
	[Parameter] public Func<TItem, int, string>? RowClassFunc { get; set; }

	public MudTable<TItem>? TableRef { get; private set; }
	public Task ReloadServerData() => TableRef?.ReloadServerData() ?? Task.CompletedTask;

	private readonly string _generatedTitleId = $"table-title-{Guid.NewGuid():N}";
	private readonly bool UseLoadingContentBody = true;
	private int _rowsPerPage;
	private bool _isReloading;

	private string ContainerCssClass => string.IsNullOrWhiteSpace(ContainerClass)
		? "responsive-table-container"
		: $"responsive-table-container {ContainerClass}";

	private string TableCssClass => string.IsNullOrWhiteSpace(TableClass)
		? "generic-responsive-table transparent-paper"
		: $"generic-responsive-table transparent-paper {TableClass}";

	private string ToolbarCssClass =>
		$"table-component-toolbar{(ToolBarLeft is not null ? " has-left-content" : string.Empty)}{(!string.IsNullOrWhiteSpace(Title) ? " has-title" : string.Empty)}";

	private bool HasToolbarActions =>
		ToolBarLeft is not null || EnableSearch || EnableReload || !string.IsNullOrWhiteSpace(AddButtonText);

	private string ResolvedTitleId => string.IsNullOrWhiteSpace(TitleId) ? _generatedTitleId : TitleId;
	private bool HasAccessibleName =>
		!string.IsNullOrWhiteSpace(Title) || !string.IsNullOrWhiteSpace(TableAriaLabel);
	private string? AccessibleLabel => string.IsNullOrWhiteSpace(Title) ? TableAriaLabel : null;
	private string? AccessibleTitleId => string.IsNullOrWhiteSpace(Title) ? null : ResolvedTitleId;

	protected override void OnInitialized()
	{
		_rowsPerPage = RowsPerPage ?? 10;
	}

	private void OnRowsPerPageChanged(int value)
	{
		_rowsPerPage = value;
	}

	private async Task SearchChanged(string value)
	{
		SearchString = value;

		if (SearchStringChanged.HasDelegate)
			await SearchStringChanged.InvokeAsync(value);

		if (TableRef is not null && LoadServerData is not null)
			await TableRef.ReloadServerData();
	}

	private async Task ReloadDataAsync()
	{
		if (_isReloading)
			return;

		_isReloading = true;

		try
		{
			if (OnReloadClicked.HasDelegate)
				await OnReloadClicked.InvokeAsync();
			else
				await ReloadServerData();
		}
		finally
		{
			_isReloading = false;
		}
	}
}
