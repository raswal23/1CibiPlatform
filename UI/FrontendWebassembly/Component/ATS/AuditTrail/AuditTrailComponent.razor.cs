namespace FrontendWebassembly.Component.ATS;

public partial class AuditTrailComponent
{
	// null is the "All" segment; the other two are the AuditOutcome vocabulary. Reusing
	// the shared dot modifiers: a successful action is "done" and a failed one is "error",
	// so this screen's chips match every other status board.
	private static readonly OutcomeSegment[] OutcomeSegments =
	[
		new OutcomeSegment(null, "All", "is-all"),
		new OutcomeSegment(AuditActionOutcome.Success, "Success", "is-done"),
		new OutcomeSegment(AuditActionOutcome.Failure, "Failure", "is-error")
	];

	// Mirrors AtsAuditOptions.RetentionDays, which lives in the backend assembly. Shown so
	// the screen explains why older actions are not here.
	private const int RetentionDays = 30;

	private const string ExcelContentType =
		"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

	private readonly CursorTableLoader<AuditTrailListDTO> _auditLoader = new();

	private TableComponent<AuditTrailListDTO>? _auditTable;
	private AuditOutcomeCountsDTO _counts = new();
	private DateRange? _dateRange;
	private string? _activeOutcome;
	private string? _searchString;
	private bool _isLoadingCounts;
	private bool _isExporting;

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

	private async Task<TableData<AuditTrailListDTO>> LoadAuditEntriesAsync(
		TableState state,
		CancellationToken cancellationToken)
	{
		// Every input that invalidates the keyset walk must be in the signature.
		var signature = string.Join(
			'|',
			_activeOutcome,
			_searchString,
			_dateRange?.Start?.ToString("yyyy-MM-dd"),
			_dateRange?.End?.ToString("yyyy-MM-dd"));

		var tableData = await LoadCursorPagedDataAsync(
			_auditLoader,
			state,
			signature,
			(cursor, pageSize) => AuditTrailService.GetAuditTrailAsync(
				cursor,
				pageSize,
				_activeOutcome,
				action: null,
				area: null,
				_searchString,
				_dateRange?.Start,
				_dateRange?.End));

		// The chips track the same search/date filters as the table, so they refresh with
		// it rather than drifting out of step.
		await RefreshCountsAsync();

		return tableData;
	}

	/// <summary>
	/// Downloads the trail as a styled Excel workbook, honouring every active filter.
	/// </summary>
	/// <remarks>
	/// Filtered on purpose, unlike the bulk subject export: that file is named after one
	/// upload and always means the whole of it, whereas this screen IS its filters - a
	/// workbook that silently ignored the active outcome chip and date range would not be
	/// the thing the user is looking at.
	/// </remarks>
	private async Task ExportAuditTrailAsync()
	{
		if (_isExporting)
		{
			return;
		}

		_isExporting = true;

		try
		{
			var response = await AuditTrailService.ExportAuditTrailAsync(
				_activeOutcome,
				action: null,
				area: null,
				_searchString,
				_dateRange?.Start,
				_dateRange?.End);

			if (!response.IsSuccess || response.Data is null)
			{
				Snackbar.Add(response.ErrorDetail, Severity.Error);
				return;
			}

			var fileBytes = await response.Data.Content.ReadAsByteArrayAsync();

			var fileName =
				response.Data.Content.Headers.ContentDisposition?.FileName?.Trim('"')
				?? "ats-audit-trail.xlsx";

			await JS.InvokeVoidAsync("downloadFile", fileName, ExcelContentType, fileBytes);

			Snackbar.Add("Audit trail exported.", Severity.Success);
		}
		finally
		{
			_isExporting = false;
			await InvokeAsync(StateHasChanged);
		}
	}

	private async Task SetOutcomeAsync(string? outcome)
	{
		if (_activeOutcome == outcome)
		{
			return;
		}

		_activeOutcome = outcome;

		// A changed filter starts a new keyset walk; keep MudTable's page in sync with the
		// loader's reset-to-first-page or the pager shows a stale page.
		if (_auditTable?.TableRef is not null)
		{
			_auditTable.TableRef.CurrentPage = 0;
		}

		await ReloadTableAsync();
	}

	private async Task OnDateRangeChanged(DateRange range)
	{
		_dateRange = range;

		if (_auditTable?.TableRef is not null)
		{
			_auditTable.TableRef.CurrentPage = 0;
		}

		await ReloadTableAsync();
	}

	private async Task ReloadTableAsync()
	{
		if (_auditTable?.TableRef is not null)
		{
			await _auditTable.TableRef.ReloadServerData();
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
			var response = await AuditTrailService.GetOutcomeCountsAsync(
				action: null,
				area: null,
				_searchString,
				_dateRange?.Start,
				_dateRange?.End);

			// A failed count must not blank the table that just loaded successfully; the
			// previous chip values stay on screen and the snackbar explains why.
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

	private async Task OpenDetailsAsync(AuditTrailListDTO entry)
	{
		var parameters = new DialogParameters
		{
			{ nameof(AuditTrailDetailDialog.Entry), entry }
		};

		var options = new DialogOptions
		{
			NoHeader = true,
			MaxWidth = MaxWidth.Small,
			FullWidth = true
		};

		var dialog = await DialogService.ShowAsync<AuditTrailDetailDialog>(null, parameters, options);

		await dialog.Result;
	}

	private long CountFor(string? outcome) => outcome switch
	{
		AuditActionOutcome.Success => _counts.Success,
		AuditActionOutcome.Failure => _counts.Failure,
		_ => _counts.Total
	};

	private string GetSegmentClass(string? outcome) =>
		_activeOutcome == outcome
			? "ats-segment-btn ats-status-board-btn active"
			: "ats-segment-btn ats-status-board-btn";

	private static bool IsFailure(AuditTrailListDTO entry) =>
		string.Equals(entry.Outcome, AuditActionOutcome.Failure, StringComparison.OrdinalIgnoreCase);

	// Reuses the shared pill colours rather than introducing a new pair: a completed
	// action reads like a completed ticket, and a failed one like a failed ticket.
	private static string GetOutcomeClass(string? outcome) => outcome switch
	{
		AuditActionOutcome.Success => "done",
		AuditActionOutcome.Failure => "error",
		_ => "unknown"
	};

	// The account may since have been renamed or removed, so the trail falls back to
	// whatever identifier it captured at the time.
	private static string DisplayName(AuditTrailListDTO entry)
	{
		if (!string.IsNullOrWhiteSpace(entry.UserFullName))
		{
			return entry.UserFullName;
		}

		return string.IsNullOrWhiteSpace(entry.UserEmail)
			? "Unknown user"
			: entry.UserEmail;
	}

	private static string FormatDuration(int durationMs) =>
		durationMs < 1000
			? $"{durationMs} ms"
			: $"{durationMs / 1000.0:0.0} s";

	private static string FormatAbsolute(DateTime value) =>
		DateTime.SpecifyKind(value, DateTimeKind.Utc)
			.ToLocalTime()
			.ToString("MMMM dd, yyyy h:mm tt");

	private static string FormatRelative(DateTime value)
	{
		var elapsed = DateTime.UtcNow - DateTime.SpecifyKind(value, DateTimeKind.Utc);

		// A clock skew between the browser and the server can make a fresh entry look like
		// it arrived in the future; treat anything negative as "just now".
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

		return FormatAbsolute(value);
	}

	private string EmptyTitle => _activeOutcome switch
	{
		AuditActionOutcome.Success => "No successful actions recorded",
		AuditActionOutcome.Failure => "No failed actions",
		_ => "No actions recorded yet"
	};

	private string EmptySubtitle => _activeOutcome switch
	{
		AuditActionOutcome.Success => "Nothing completed in this period. Check the Failure view.",
		AuditActionOutcome.Failure => "Every action in this period completed. Nothing needs attention.",
		_ => $"Changes made in ATS appear here within seconds and are kept for {RetentionDays} days."
	};

	private sealed record OutcomeSegment(string? Value, string Label, string Modifier);
}
