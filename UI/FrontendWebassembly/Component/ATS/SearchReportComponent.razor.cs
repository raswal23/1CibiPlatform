namespace FrontendWebassembly.Component.ATS;

public partial class SearchReportComponent
{
    private TableComponent<ReportListDTO>? reportsTable;
	private DateRange? _dateRange { get; set; }
    private string? _searchString;
	private List<ReportListDTO> currentPageData = new();


	private string searchString
	{
      get => _searchString!;
		set => UpdateSearch(ref _searchString!, value, reportsTable!);
	}

    private void UpdateSearch<T>(ref string field, string value, TableComponent<T> table) where T : class
	{
      if (field != value)
		{
			field = value;
			table?.TableRef!.ReloadServerData();
		}
	}

	private async Task<TableData<ReportListDTO>> LoadReportData(TableState state, CancellationToken cancellationToken)
	{
		try
		{
         var result = await ReportService.GetReportsAsync(
				state.Page + 1,
				state.PageSize,
				searchString,
				state.SortLabel,
				state.SortDirection == SortDirection.Descending);
			currentPageData = result.Data?.ToList() ?? new List<ReportListDTO>();

         if (_dateRange?.Start is not null || _dateRange?.End is not null)
			{
				var start = _dateRange?.Start?.Date;
				var end = _dateRange?.End?.Date;

				currentPageData = currentPageData
					.Where(r => r.OrderCompletedAt.HasValue &&
						(!start.HasValue || r.OrderCompletedAt.Value.Date >= start.Value) &&
						(!end.HasValue || r.OrderCompletedAt.Value.Date <= end.Value))
					.ToList();
			}

			return new TableData<ReportListDTO>
			{
				Items = currentPageData,
               TotalItems = (int)result.Count
			};
		}
		catch (Exception)
		{
			Snackbar.Add("Failed to load reports.", Severity.Error);
			return new TableData<ReportListDTO>
			{
				Items = Array.Empty<ReportListDTO>(),
				TotalItems = 0
			};
		}
	}

	private async Task DownloadSelected()
	{
       var selected = currentPageData.Where(r => r.Selected).ToList();
		if (!selected.Any())
		{
          await JS.InvokeVoidAsync("console.warn", "No reports selected for download.");
			return;
		}

      await JS.InvokeVoidAsync("console.log", $"Downloading {selected.Count} reports.", selected.Select(r => r.EmailInvitationRequestId));
	}

	private async Task OpenResultDialog<TComponent>(
	string title,
	DialogParameters? parameters = null)
	where TComponent : IComponent
	{
		var options = new DialogOptions
		{
			CloseButton = true,
			MaxWidth = MaxWidth.Large,
			FullWidth = true
		};

		var dialog = await DialogService.ShowAsync<TComponent>(
			title,
			parameters!,
			options);

		var result = await dialog.Result;
	}

	private async Task OpenResultTriggerDialog(Guid emailInvitationId)
	{
       try
		{
			var reportResult = await ReportService.GetReportResultByEmailInvitationRequestIdAsync(emailInvitationId);

			var parameters = new DialogParameters
			{
				{ nameof(ATSResultComponent.EmailInvitationId), emailInvitationId },
				{ nameof(ATSResultComponent.ReportResult), reportResult }
			};

			await OpenResultDialog<ATSResultComponent>(
				"Subject Result",
				parameters);
		}
		catch (Exception)
		{
			Snackbar.Add("Failed to load ATS result details.", Severity.Error);
		}
	}

}