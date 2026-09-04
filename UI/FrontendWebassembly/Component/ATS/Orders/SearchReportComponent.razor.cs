namespace FrontendWebassembly.Component.ATS;

public partial class SearchReportComponent
{
	private const string RoleIdStorageKey = "RoleId";
	private const string ATSRoleIdStorageKey = "ATSRoleId";

	[Inject]
	private LocalStorageService LocalStorageService { get; set; } = default!;

	private readonly CursorTableLoader<ReportListDTO> _reportsLoader = new();
	private TableComponent<ReportListDTO>? reportsTable;
	private DateRange? _dateRange { get; set; }
	private string? _searchString;
	private List<ReportListDTO> currentPageData = new();
	private bool _isStatusLegendExpanded = false;
	private bool _canUploadReport;
	private bool _canEditSubjectName;
	private int ReportColumnCount => 10
		+ (_canUploadReport ? 1 : 0)
		+ (_canEditSubjectName ? 1 : 0);

	protected override async Task OnInitializedAsync()
	{
		var roleIds = await GetStoredRoleIdsAsync();
		var atsRoleId = await GetStoredATSRoleIdAsync();

		_canUploadReport = roleIds.Contains(1) || atsRoleId is 1 or 3;

		// Correcting a subject name is an administrative fix, so it follows the
		// platform super admin / platform manager / admin ladder rather than the
		// uploader-oriented one above. The API re-checks scope on every call.
		_canEditSubjectName = roleIds.Contains(1) || atsRoleId is 1 or 2;
	}

	private async Task<List<int>> GetStoredRoleIdsAsync()
	{
		try
		{
			var roleIdsJson = await LocalStorageService.GetItemAsync<string>(RoleIdStorageKey);
			return string.IsNullOrWhiteSpace(roleIdsJson)
				? []
				: JsonSerializer.Deserialize<List<int>>(roleIdsJson) ?? [];
		}
		catch (JsonException)
		{
			return [];
		}
	}

	private async Task<int> GetStoredATSRoleIdAsync()
	{
		try
		{
			return await LocalStorageService.GetItemAsync<int>(ATSRoleIdStorageKey);
		}
		catch (JsonException)
		{
			return 0;
		}
	}

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
		var signature = $"{searchString}|{_dateRange?.Start:yyyy-MM-dd}|{_dateRange?.End:yyyy-MM-dd}";

		var tableData = await _reportsLoader.LoadAsync(
			state,
			signature,
			(cursor, pageSize) => ReportService.GetReportsAsync(
				cursor,
				pageSize,
				searchString,
				_dateRange?.Start,
				_dateRange?.End),
			message => Snackbar.Add(message, Severity.Error));

		currentPageData = tableData.Items?.ToList() ?? new List<ReportListDTO>();

		return tableData;
	}

	private async Task OnDateRangeChanged(DateRange range)
	{
		_dateRange = range;

		// A changed filter starts a new keyset walk; keep MudTable's page in
		// sync with the loader's reset-to-first-page.
		if (reportsTable?.TableRef is not null)
			reportsTable.TableRef.CurrentPage = 0;

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

		var downloadResponse = await ReportService.DownloadMultipleOrderRecordsAsync(downloadMultipleOrderRecordsRequest);

		if (!downloadResponse.IsSuccess || downloadResponse.Data is null)
		{
			Snackbar.Add(downloadResponse.ErrorDetail, Severity.Error);
			return;
		}

		var response = downloadResponse.Data;
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
		var resultResponse = await ReportService.GetReportResultByEmailInvitationRequestIdAsync(emailInvitationId);

		if (!resultResponse.IsSuccess)
		{
			Snackbar.Add(resultResponse.ErrorDetail, Severity.Error);
			return;
		}

		var parameters = new DialogParameters
		{
			{ nameof(ATSResultComponent.EmailInvitationId), emailInvitationId },
			{ nameof(ATSResultComponent.ReportResult), resultResponse.Data }
		};

		await OpenResultDialog<ATSResultComponent>(
			"",
			parameters,
			MaxWidth.Medium,
			fullWidth: false);
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
	private async Task OpenEditSubjectNameDialog(ReportListDTO report)
	{
		var parameters = new DialogParameters<EditSubjectNameComponent>
		{
			{ component => component.Report, report }
		};

		var options = new DialogOptions
		{
			CloseButton = false,
			NoHeader = true,
			MaxWidth = MaxWidth.Small,
			FullWidth = true,
			BackdropClick = false
		};

		var dialog = await DialogService.ShowAsync<EditSubjectNameComponent>(
			"Edit subject name",
			parameters,
			options);

		var result = await dialog.Result;

		if (result is null || result.Canceled || result.Data is not SubjectNameDTO updated)
			return;

		// The row is patched in place instead of reloading the page: a reload would
		// reset the keyset walk and drop any checkboxes the user already ticked.
		report.SubjectName = updated.SubjectName;
		report.FirstName = updated.FirstName;
		report.MiddleInitial = updated.MiddleInitial;
		report.LastName = updated.LastName;

		await InvokeAsync(StateHasChanged);
	}

	private async Task OpenStatusHistoryDialog(ReportListDTO report)
	{
		var parameters = new DialogParameters { { nameof(OrderStatusHistoryDialog.EmailInvitationRequestId), report.EmailInvitationRequestId }, { nameof(OrderStatusHistoryDialog.SubjectName), report.SubjectName } };
		await OpenResultDialog<OrderStatusHistoryDialog>(string.Empty, parameters, MaxWidth.Small, noHeader: true);
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
