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

	/// <summary>
	/// Pre-fills the search box from the URL, so the "bulk upload processed" notification
	/// lands on the file it is about. The sender passes the file name, which is what this
	/// board's search matches on.
	/// </summary>
	[SupplyParameterFromQuery(Name = "search")]
	private string? SearchFromQuery { get; set; }

	// The ?search= value this board has already acted on. Only a change to it counts as a
	// new deep link - see OnParametersSetAsync.
	private string? _appliedSearchFromQuery;

	protected override async Task OnInitializedAsync()
	{
		// Before the first await: base.OnInitializedAsync yields, Blazor renders, and
		// MudTable loads the board at that point. Seeding afterwards left a filled search
		// box over unfiltered results. See TicketingStatusComponent for the full note.
		if (!string.IsNullOrWhiteSpace(SearchFromQuery))
		{
			_searchString = SearchFromQuery;
		}

		// Claimed here so the OnParametersSetAsync pass that follows this first render does
		// not treat the value it just seeded as an arriving deep link.
		_appliedSearchFromQuery = SearchFromQuery;

		await base.OnInitializedAsync();

		// Without this guard the RequirePermission/RequireATSModule attributes are inert.
		if (!IsPageAuthorized)
		{
			return;
		}

		await RefreshCountsAsync();
	}

	/// <summary>
	/// Applies a ?search= that arrives while this board is already on screen.
	/// </summary>
	/// <remarks>
	/// OnInitializedAsync only runs when the notification is clicked from another page,
	/// because that builds the component. Clicking one while already here just rewrites the
	/// URL and re-supplies the query parameter, so this is the only place the new file name
	/// is seen. See TicketingStatusComponent for the full note.
	/// </remarks>
	protected override async Task OnParametersSetAsync()
	{
		await base.OnParametersSetAsync();

		if (!IsPageAuthorized || SearchFromQuery == _appliedSearchFromQuery)
		{
			return;
		}

		_appliedSearchFromQuery = SearchFromQuery;

		// A link with no ?search= leaves the current filter alone rather than silently
		// clearing what the user typed.
		if (string.IsNullOrWhiteSpace(SearchFromQuery))
		{
			return;
		}

		_searchString = SearchFromQuery;

		// The notification points at one file; a status filter left on from earlier would
		// hide it.
		_activeStatus = null;

		if (_uploadsTable?.TableRef is not null)
		{
			_uploadsTable.TableRef.CurrentPage = 0;
		}

		await ReloadTableAsync();
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
			? "ats-segment-btn ats-status-board-btn active"
			: "ats-segment-btn ats-status-board-btn";

	private static bool HasSubjects(BulkUploadListDTO upload) => upload.SubjectCount > 0;

	// The email progress bar only means something for a file that sends invitations.
	// A data file never does, so its permanent 0/N would read as a stalled queue.
	private static bool SendsInvitations(BulkUploadListDTO upload) =>
		HasSubjects(upload) && upload.AutoChasing != false;

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

	private static string GetOrderTypeClass(string? orderType) => OrderTypeDisplay.GetClass(orderType);

	// Same vocabulary as package management, so a file and the package it was placed
	// under read identically. Null is "Not set" rather than defaulting to either type:
	// an unclassified file is a genuine gap, not a quiet Manual.
	private static string GetScreeningTypeLabel(bool? autoChasing) => autoChasing switch
	{
		true => "Manual",
		false => "Data",
		_ => "Not set"
	};

	private static string GetScreeningTypeClass(bool? autoChasing) => autoChasing switch
	{
		true => "is-manual",
		false => "is-data",
		_ => "is-unset"
	};

	// The column answers "why is the Emails cell empty?", so the tooltip says it outright.
	private static string GetScreeningTypeHint(bool? autoChasing) => autoChasing switch
	{
		true => "Manual screening: an application form is emailed to every candidate in this file",
		false => "Data screening: no application form is emailed to these candidates",
		_ => "This file predates screening types, or was uploaded without one"
	};

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
