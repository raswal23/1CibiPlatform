namespace FrontendWebassembly.Component.ATS;

public partial class TicketingStatusComponent
{
	// null is the "All" segment; the other four are the TicketStatus vocabulary.
	private static readonly StatusSegment[] StatusSegments =
	[
		new StatusSegment(null, "All", "is-all"),
		new StatusSegment(OrderTicketStatus.Pending, "Pending", "is-pending"),
		new StatusSegment(OrderTicketStatus.Processing, "Processing", "is-processing"),
		new StatusSegment(OrderTicketStatus.Done, "Done", "is-done"),
		new StatusSegment(OrderTicketStatus.Error, "Error", "is-error")
	];

	private readonly CursorTableLoader<TicketedOrderListDTO> _ordersLoader = new();

	private TableComponent<TicketedOrderListDTO>? _ordersTable;
	private TicketStatusCountsDTO _counts = new();
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

	private async Task<TableData<TicketedOrderListDTO>> LoadOrdersAsync(
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
			_ordersLoader,
			state,
			signature,
			(cursor, pageSize) => OMSTicketingService.GetTicketedOrdersAsync(
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

	private async Task SetStatusAsync(string? status)
	{
		if (_activeStatus == status)
		{
			return;
		}

		_activeStatus = status;

		// A changed filter starts a new keyset walk; keep MudTable's page in sync with
		// the loader's reset-to-first-page or the pager shows a stale page.
		if (_ordersTable?.TableRef is not null)
		{
			_ordersTable.TableRef.CurrentPage = 0;
		}

		await ReloadTableAsync();
	}

	private async Task OnDateRangeChanged(DateRange range)
	{
		_dateRange = range;

		if (_ordersTable?.TableRef is not null)
		{
			_ordersTable.TableRef.CurrentPage = 0;
		}

		await ReloadTableAsync();
	}

	private async Task ReloadTableAsync()
	{
		if (_ordersTable?.TableRef is not null)
		{
			await _ordersTable.TableRef.ReloadServerData();
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
			var response = await OMSTicketingService.GetStatusCountsAsync(
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
		OrderTicketStatus.Pending => _counts.Pending,
		OrderTicketStatus.Processing => _counts.Processing,
		OrderTicketStatus.Done => _counts.Done,
		OrderTicketStatus.Error => _counts.Error,
		_ => _counts.Total
	};

	private string GetSegmentClass(string? status) =>
		_activeStatus == status
			? "ats-segment-btn ats-status-board-btn active"
			: "ats-segment-btn ats-status-board-btn";

	private static bool IsError(TicketedOrderListDTO order) =>
		string.Equals(order.TicketStatus, OrderTicketStatus.Error, StringComparison.OrdinalIgnoreCase);

	private static string FullName(TicketedOrderListDTO order)
	{
		var parts = new[] { order.FirstName, order.MiddleInitial, order.LastName }
			.Where(part => !string.IsNullOrWhiteSpace(part));

		var name = string.Join(' ', parts);

		return string.IsNullOrWhiteSpace(name) ? "—" : name;
	}

	private static string GetStatusClass(string? status) => status switch
	{
		OrderTicketStatus.Pending => "pending",
		OrderTicketStatus.Processing => "processing",
		OrderTicketStatus.Done => "done",
		OrderTicketStatus.Error => "error",
		_ => "unknown"
	};

	private static string FormatDate(DateTime? value) =>
		value.HasValue
			? value.Value.ToLocalTime().ToString("MMMM dd, yyyy")
			: "—";

	private static string FormatAbsolute(DateTime? value) =>
		value.HasValue
			? value.Value.ToLocalTime().ToString("MMMM dd, yyyy h:mm tt")
			: "—";

	private static string FormatRelative(DateTime? value)
	{
		if (!value.HasValue)
		{
			return "—";
		}

		var elapsed = DateTime.UtcNow - DateTime.SpecifyKind(value.Value, DateTimeKind.Utc);

		// A clock skew between the browser and the server can make a fresh order look
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

		return FormatAbsolute(value);
	}

	private string EmptyTitle => _activeStatus switch
	{
		OrderTicketStatus.Pending => "No orders waiting to be ticketed",
		OrderTicketStatus.Processing => "Nothing is being ticketed",
		OrderTicketStatus.Done => "No tickets raised yet",
		OrderTicketStatus.Error => "No failed tickets",
		_ => "No orders queued for ticketing yet"
	};

	private string EmptySubtitle => _activeStatus switch
	{
		OrderTicketStatus.Pending => "Every order has already been picked up for ticketing.",
		OrderTicketStatus.Processing => "No order is being sent to OMS right now. Pending orders are picked up within seconds.",
		OrderTicketStatus.Done => "No order has been ticketed yet. Check the Pending and Processing views.",
		OrderTicketStatus.Error => "Every order OMS has seen was accepted. Nothing needs attention.",
		_ => "Create an order from New Order and it will appear here within seconds."
	};

	private sealed record StatusSegment(string? Value, string Label, string Modifier);
}
