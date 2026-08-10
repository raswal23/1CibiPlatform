namespace FrontendWebassembly.Component.ATS;

public partial class SearchReportComponent
{
    private TableComponent<ReportListDTO>? reportsTable;
	private DateRange? _dateRange { get; set; }
    private string? _searchString;
	private List<ReportListDTO> currentPageData = new();
	private bool _isStatusLegendExpanded = false;

	private void ToggleStatusLegend() => _isStatusLegendExpanded = !_isStatusLegendExpanded;

	private static string GetInitials(string? name)
		=> string.Join(string.Empty, (name ?? string.Empty)
			.Split(' ', StringSplitOptions.RemoveEmptyEntries)
			.Take(2)
			.Select(part => char.ToUpperInvariant(part[0])));

	private static string GetOrderStatusClass(string? status) => OrderStatusDisplay.GetClass(status);
	private static string GetOrderStatusText(string? status) => OrderStatusDisplay.GetText(status);

	private static string GetHitStatusClass(string? status) => HitStatusDisplay.GetClass(status);
	private static string GetHitStatusText(string? status) => HitStatusDisplay.GetText(status);

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
				state.SortDirection == SortDirection.Descending,
				_dateRange?.Start,
				_dateRange?.End);
			currentPageData = result.Data?.ToList() ?? new List<ReportListDTO>();

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

    private async Task OnDateRangeChanged(DateRange range)
    {
        _dateRange = range;

        await ReloadTable();
    }

    private async Task DownloadSelected()
	{

		if (!currentPageData.Any(r => r.Selected))
		{
			Snackbar.Add("Please select at least one record to download.", Severity.Error);
			return;
		}

		var selected = currentPageData.Where(r => r.Selected).ToList();

		DownloadMultipleOrderRecordsRequestDTO downloadMultipleOrderRecordsRequest = new DownloadMultipleOrderRecordsRequestDTO();

		foreach (var report in currentPageData.Where(x => x.Selected))
		{
			downloadMultipleOrderRecordsRequest.EmailInvitaionRequestList.Add(report.EmailInvitationRequestId);
		}

		var response = await ReportService.DownloadMultipleOrderRecordsAsync(downloadMultipleOrderRecordsRequest);

		if (!response.IsSuccessStatusCode)
		{
			Snackbar.Add("Failed to download records.", Severity.Error);
			return;
		}

		var fileBytes = await response.Content.ReadAsByteArrayAsync();

		using (var ms = new MemoryStream(fileBytes))
		using (var zip = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Read, leaveOpen: false))
		{
			if (!zip.Entries.Any())
			{
				Snackbar.Add("The downloaded ZIP contains no files.", Severity.Warning);
				return;
			}
		}

		var fileName =
			response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
			?? $"FileRecords-{DateTime.Now:yyyyMMdd_HHmmss}.zip";

		await JS.InvokeVoidAsync("downloadFile", fileName, "application/zip", fileBytes);
	}

	private async Task OpenResultDialog<TComponent>(
		string title,
		DialogParameters? parameters = null,
		MaxWidth maxWidth = MaxWidth.Medium,
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
				"",
				parameters,
				MaxWidth.Medium,
				fullWidth: false);
		}
		catch (Exception)
		{
			Snackbar.Add("Failed to load ATS result details.", Severity.Error);
		}
	}

	private async Task OpenUploadReportDialog(Guid emailInvitationId)
	{
		IDialogReference? dialog = null;
		var parameters = new DialogParameters
		{
			{ nameof(UploadReportComponent.EmailInvitationRequestId), emailInvitationId },
			{ nameof(UploadReportComponent.OnUploadSucceededReload),
				EventCallback.Factory.Create(this, ReloadTable) }
		};

		var options = new DialogOptions
		{
			CloseButton = false,
			NoHeader = true,
			MaxWidth = MaxWidth.Small,
			FullWidth = false
		};

		dialog = await DialogService.ShowAsync<UploadReportComponent>(
			string.Empty,
			parameters,
			options);

		await dialog.Result;
	}

	private async Task ReloadTable()
	{
		if (reportsTable?.TableRef is not null)
		{
			await reportsTable.TableRef.ReloadServerData();
			await InvokeAsync(StateHasChanged);
		}
	}

	private string GetRowClass(ReportListDTO r, int index)
	{
		return r.Selected ? "ats-selected-row" : "";
	}

	private void OnCheckboxChanged(ReportListDTO row, bool value)
	{
		row.Selected = value;
		StateHasChanged();
	}
}
