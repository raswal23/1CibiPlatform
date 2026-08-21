namespace FrontendWebassembly.Component.ATS;

public partial class BulkUploadsComponent
{
	// null is the "All" segment; the other three are the BulkFileStatus vocabulary.
	private static readonly StatusSegment[] StatusSegments =
	[
		new StatusSegment(null, "All", "is-all"),
		new StatusSegment(BulkUploadStatus.Pending, "Pending", "is-pending"),
		new StatusSegment(BulkUploadStatus.Processing, "Processing", "is-processing"),
		new StatusSegment(BulkUploadStatus.Done, "Done", "is-done")
	];

	private readonly CursorTableLoader<BulkUploadListDTO> _uploadsLoader = new();

	private TableComponent<BulkUploadListDTO>? _uploadsTable;
	private BulkUploadStatusCountsDTO _counts = new();
	private DateRange? _dateRange;
	private string? _activeStatus;
	private string? _searchString;
	private bool _isLoadingCounts;

	protected override async Task OnInitializedAsync()
	{
		await base.OnInitializedAsync();

		// Without this guard the RequirePermission/RequireATSModule attributes are inert.
		if (!IsPageAuthorized)
		{
			return;
		}

		await RefreshCountsAsync();
	}

	private async Task<TableData<BulkUploadListDTO>> LoadUploadsAsync(
		TableState state,
		CancellationToken cancellationToken)
	{
		// Every input that invalidates the keyset walk must be in the signature.
		var signature = string.Join(
			'|',
			_activeStatus,
			_searchString,
			_dateRange?.Start?.ToString("yyyy-MM-dd"),
			_dateRange?.End?.ToString("yyyy-MM-dd"));

		var tableData = await LoadCursorPagedDataAsync(
			_uploadsLoader,
			state,
			signature,
			(cursor, pageSize) => BulkUploadService.GetBulkUploadsAsync(
				cursor,
				pageSize,
				_activeStatus,
				_searchString,
				_dateRange?.Start,
				_dateRange?.End));

		// The chips track the same search/date filters as the table, so they refresh
		// with it rather than drifting out of step.
		await RefreshCountsAsync();

		return tableData;
	}

	private async Task OnUploadRowClickedAsync(TableRowClickEventArgs<BulkUploadListDTO> args)
	{
		if (args.Item is not { } upload)
		{
			return;
		}

		// A file the parsing job has not reached yet has no subjects at all. Opening an
		// empty dialog would read as "the upload lost my rows", so say what is happening.
		if (!HasSubjects(upload))
		{
			Snackbar.Add(
				"This file has not been parsed yet. Its subjects appear here within seconds.",
				Severity.Info);

			return;
		}

		var parameters = new DialogParameters
		{
			{ nameof(BulkUploadSubjectsDialog.FileID), upload.FileID },
			{ nameof(BulkUploadSubjectsDialog.Upload), upload }
		};

		var options = new DialogOptions
		{
			NoHeader = true,
			MaxWidth = MaxWidth.Large,
			FullWidth = true
		};

		var dialog = await DialogService.ShowAsync<BulkUploadSubjectsDialog>(
			null,
			parameters,
			options);

		await dialog.Result;

		// Resending from the dialog changes a row's email rollup, so pick the new
		// figures up rather than leaving the dashboard showing pre-resend counts.
		await ReloadTableAsync();
	}

	private async Task SetStatusAsync(string? status)
	{
		if (_activeStatus == status)
		{
			return;
		}

		_activeStatus = status;

		// A changed filter starts a new keyset walk; keep MudTable's page in sync with
		// the loader's reset-to-first-page or the pager shows a stale page.
		if (_uploadsTable?.TableRef is not null)
		{
			_uploadsTable.TableRef.CurrentPage = 0;
		}

		await ReloadTableAsync();
	}

	private async Task OnDateRangeChanged(DateRange range)
	{
		_dateRange = range;

		if (_uploadsTable?.TableRef is not null)
		{
			_uploadsTable.TableRef.CurrentPage = 0;
		}

		await ReloadTableAsync();
	}

	private async Task ReloadTableAsync()
	{
		if (_uploadsTable?.TableRef is not null)
		{
			await _uploadsTable.TableRef.ReloadServerData();
			await InvokeAsync(StateHasChanged);
		}
	}

	private async Task RefreshCountsAsync()
	{
		if (_isLoadingCounts)
		{
			return;
		}

		_isLoadingCounts = true;

		try
		{
			var response = await BulkUploadService.GetStatusCountsAsync(
				_searchString,
				_dateRange?.Start,
				_dateRange?.End);

			// A failed count must not blank the table that just loaded successfully;
			// the previous chip values stay on screen and the snackbar explains why.
			if (!response.IsSuccess || response.Data is null)
			{
				Snackbar.Add(response.ErrorDetail, Severity.Error);
				return;
			}

			_counts = response.Data;
		}
		finally
		{
			_isLoadingCounts = false;
		}
	}

	private long CountFor(string? status) => status switch
	{
		BulkUploadStatus.Pending => _counts.Pending,
		BulkUploadStatus.Processing => _counts.Processing,
		BulkUploadStatus.Done => _counts.Done,
		_ => _counts.Total
	};

	private string GetSegmentClass(string? status) =>
		_activeStatus == status
			? "ats-segment-btn bulk-segment-btn active"
			: "ats-segment-btn bulk-segment-btn";

	private static bool HasSubjects(BulkUploadListDTO upload) => upload.SubjectCount > 0;

	private static int SentPercent(BulkUploadListDTO upload) =>
		upload.SubjectCount == 0
			? 0
			: (int)Math.Round(upload.EmailsSent * 100d / upload.SubjectCount);

	private static string GetStatusClass(string? status) => status switch
	{
		BulkUploadStatus.Pending => "pending",
		BulkUploadStatus.Processing => "processing",
		BulkUploadStatus.Done => "done",
		_ => "unknown"
	};

	private static string GetOrderTypeClass(string? orderType) =>
		string.Equals(orderType, "Rush", StringComparison.OrdinalIgnoreCase)
			? "is-rush"
			: "is-normal";

	private static string FormatAbsolute(DateTime dateCreated) =>
		dateCreated.ToLocalTime().ToString("MMMM dd, yyyy h:mm tt");

	private static string FormatRelative(DateTime dateCreated)
	{
		var elapsed = DateTime.UtcNow - DateTime.SpecifyKind(dateCreated, DateTimeKind.Utc);

		// A clock skew between the browser and the server can make a fresh upload look
		// like it arrived in the future; treat anything negative as "just now".
		if (elapsed < TimeSpan.FromMinutes(1))
		{
			return "just now";
		}

		if (elapsed < TimeSpan.FromHours(1))
		{
			var minutes = (int)elapsed.TotalMinutes;
			return $"{minutes} minute{(minutes == 1 ? string.Empty : "s")} ago";
		}

		if (elapsed < TimeSpan.FromDays(1))
		{
			var hours = (int)elapsed.TotalHours;
			return $"{hours} hour{(hours == 1 ? string.Empty : "s")} ago";
		}

		if (elapsed < TimeSpan.FromDays(7))
		{
			var days = (int)elapsed.TotalDays;
			return $"{days} day{(days == 1 ? string.Empty : "s")} ago";
		}

		return FormatAbsolute(dateCreated);
	}

	private string EmptyTitle => _activeStatus switch
	{
		BulkUploadStatus.Pending => "No pending uploads",
		BulkUploadStatus.Processing => "Nothing is processing",
		BulkUploadStatus.Done => "No completed uploads",
		_ => "No bulk uploads yet"
	};

	private string EmptySubtitle => _activeStatus switch
	{
		BulkUploadStatus.Pending => "Every uploaded file has already been picked up for processing.",
		BulkUploadStatus.Processing => "No file is being parsed right now. Pending files are picked up within seconds.",
		BulkUploadStatus.Done => "Nothing has finished processing yet. Check the Pending and Processing views.",
		_ => "Upload a CSV from New Order and it will appear here within seconds."
	};

	private sealed record StatusSegment(string? Value, string Label, string Modifier);
}
