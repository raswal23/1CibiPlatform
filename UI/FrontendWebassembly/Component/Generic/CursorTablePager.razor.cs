namespace FrontendWebassembly.Component.Generic;

using FrontendWebassembly.Services.Shared.Interfaces;

public partial class CursorTablePager
{
	// Cascaded by MudTable to its PagerContent, same as the stock MudTablePager.
	[CascadingParameter] public TableContext? Context { get; set; }

	[Parameter] public ICursorPagerState? PagerState { get; set; }
	[Parameter] public int[] PageSizeOptions { get; set; } = [10, 25, 50];
	[Parameter] public string RowsPerPageString { get; set; } = "Rows per page:";
	[Parameter] public string InfoFormat { get; set; } = "{first_item}-{last_item} of {all_items}";
	[Parameter] public bool HideRowsPerPage { get; set; }

	private MudTableBase? Table => Context?.Table;
	private int RowsPerPage => Table?.RowsPerPage ?? PageSizeOptions.FirstOrDefault(10);
	private int CurrentPage => Table?.CurrentPage ?? 0;
	private bool IsFirstPage => CurrentPage == 0;

	private string InfoText
	{
		get
		{
			var itemsOnPage = PagerState?.ItemsOnPage ?? 0;
			var total = PagerState?.TotalCount ?? 0;
			var first = itemsOnPage == 0 ? 0 : CurrentPage * RowsPerPage + 1;
			var last = itemsOnPage == 0 ? 0 : first + itemsOnPage - 1;

			return InfoFormat
				.Replace("{first_item}", first.ToString())
				.Replace("{last_item}", last.ToString())
				.Replace("{all_items}", total.ToString());
		}
	}

	protected override void OnInitialized()
	{
		if (Context is not null)
			Context.PagerStateHasChanged = StateHasChanged;
	}

	private void Navigate(Page page) => Table?.NavigateTo(page);

	private void SetRowsPerPageAsync(int size) => Table?.SetRowsPerPage(size);
}
