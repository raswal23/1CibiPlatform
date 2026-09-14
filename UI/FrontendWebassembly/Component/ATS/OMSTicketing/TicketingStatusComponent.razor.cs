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

	/// <summary>
	/// Pre-fills the search box from the URL, so a "ticketing failed" notification can deep
	/// link straight to the order it is about.
	/// </summary>
	/// <remarks>
	/// The sender passes the subject's LAST NAME, not the full name: this board's search
	/// ILIKEs FirstName and LastName as separate columns, so "Russel Gutierrez" would match
	/// neither. See AtsNotificationService.BuildOrderLink.
	/// </remarks>
	[SupplyParameterFromQuery(Name = "search")]
	private string? SearchFromQuery { get; set; }

	// Disables the row's button while its retry is in flight, so a double-click cannot
	// queue the same order twice.
	private Guid? _retryingOrderId;

	// Ids rather than rows, so a selection survives the list reloading underneath it. It is
	// pruned to what is on screen whenever the table reloads, because a selection the user
	// can no longer see is one they cannot reason about.
	private readonly HashSet<Guid> _selectedInvitationIds = [];

	private bool _isBulkRetrying;

	// The rows on the page right now. Held here because CursorTableLoader tracks cursors
	// and counts, not the items themselves, and the select-all checkbox has to know what
	// "all" currently means.
	private IReadOnlyList<TicketedOrderListDTO> _pageOrders = [];

	// The rows currently rendered that a retry actually applies to. Selecting a row the
	// server would refuse only produces a confusing "0 of N" outcome.
	private IEnumerable<TicketedOrderListDTO> SelectableOrders =>
		_pageOrders.Where(CanRetry);

	private bool HasSelectableOrders => SelectableOrders.Any();

	private bool AreAllSelectablesSelected =>
		HasSelectableOrders
		&& SelectableOrders.All(order => _selectedInvitationIds.Contains(order.EmailInvitationID));

	protected override async Task OnInitializedAsync()
	{
		// Seeded BEFORE the first await, not after.
		//
		// base.OnInitializedAsync awaits an access check, and an await here lets Blazor
		// render - at which point MudTable fires its ServerData callback and loads the
		// board. Setting _searchString after that returned a filled search box over
		// unfiltered results: the value was there, but the query that had already run
		// never saw it, so the user had to retype a character to trigger a reload.
		if (!string.IsNullOrWhiteSpace(SearchFromQuery))
		{
			_searchString = SearchFromQuery;
		}

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

		// Recorded so the select-all checkbox knows what is on screen, and so a selection
		// can be pruned to it below.
		_pageOrders = tableData.Items?.ToList() ?? [];

		// A row that left the page - filtered out, paged past, or no longer retryable after
		// a reload - drops out of the selection. Keeping it would let an operator submit
		// ids they can no longer see.
		if (_selectedInvitationIds.Count > 0)
		{
			var stillSelectable = SelectableOrders
				.Select(order => order.EmailInvitationID)
				.ToHashSet();

			_selectedInvitationIds.RemoveWhere(id => !stillSelectable.Contains(id));
		}

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

	// A manual retry is only offered once the job has exhausted its automatic attempts.
	// Below the cap the order is still queued, so a button would be redundant.
	private static bool CanRetry(TicketedOrderListDTO order) =>
		IsError(order) && order.TicketAttempts >= OrderTicketStatus.MaxAttempts;

	private async Task ConfirmRetryTicketAsync(TicketedOrderListDTO order)
	{
		var confirmParam = new DialogParameters
		{
			{
				nameof(YesNoDialogComponent.Title),
				"Retry Ticketing"
			},
			{
				nameof(YesNoDialogComponent.Message),
				$"This will queue {FullName(order)}'s order to be sent to OMS again."
			},
			{
				nameof(YesNoDialogComponent.ConfirmText),
				"Retry"
			},
			{
				nameof(YesNoDialogComponent.InformationMessage),
				"Automatic retries have already been used up for this order. Make sure the "
					+ "cause has been fixed, otherwise it will fail again."
			},
			{
				nameof(YesNoDialogComponent.ConfirmIcon), Icons.Material.Outlined.Refresh
			},
			{
				nameof(YesNoDialogComponent.ConfirmActionAsync),
				(Func<Task<bool>>)(() => RetryTicketAsync(order.EmailInvitationID))
			},
			{
				nameof(YesNoDialogComponent.AvatarIcon), Icons.Material.Filled.WarningAmber
			},
			{
				nameof(YesNoDialogComponent.AvatarColor), Color.Warning
			},
			{
				nameof(YesNoDialogComponent.InfoColor), Color.Warning
			},
			{
				nameof(YesNoDialogComponent.InfoBGColor), "var(--c-warn-bg)"
			},
			{
				nameof(YesNoDialogComponent.ThemeButtonColor), "theme-button-warning"
			}
		};

		var options = new DialogOptions
		{
			NoHeader = true,
			MaxWidth = MaxWidth.ExtraSmall,
			FullWidth = true
		};

		var dialog = await DialogService.ShowAsync<YesNoDialogComponent>(null, confirmParam, options);

		await dialog.Result;
	}

	private void ToggleSelection(Guid emailInvitationId, bool isSelected)
	{
		if (isSelected)
		{
			_selectedInvitationIds.Add(emailInvitationId);
		}
		else
		{
			_selectedInvitationIds.Remove(emailInvitationId);
		}
	}

	// Scoped to the current page on purpose. Selecting rows the operator has not seen -
	// across pages or the whole filter - is how a click ends up retrying far more than
	// intended.
	private void ToggleSelectAll(bool isSelected)
	{
		foreach (var order in SelectableOrders)
		{
			ToggleSelection(order.EmailInvitationID, isSelected);
		}
	}

	// The selection bar's own escape hatch. Unchecking rows one at a time is the only
	// other way out, which is tedious once a whole page is selected.
	private void ClearSelection() => _selectedInvitationIds.Clear();

	private async Task ConfirmBulkRetryAsync()
	{
		var selectedCount = _selectedInvitationIds.Count;

		if (selectedCount == 0)
		{
			return;
		}

		var confirmParam = new DialogParameters
		{
			{
				nameof(YesNoDialogComponent.Title),
				"Retry Ticketing"
			},
			{
				nameof(YesNoDialogComponent.Message),
				$"This will queue {selectedCount} order(s) to be sent to OMS again."
			},
			{
				nameof(YesNoDialogComponent.ConfirmText),
				"Retry"
			},
			{
				nameof(YesNoDialogComponent.InformationMessage),
				"Automatic retries have already been used up for these orders. Make sure "
					+ "the cause has been fixed, otherwise they will fail again."
			},
			{
				nameof(YesNoDialogComponent.ConfirmIcon), Icons.Material.Outlined.Refresh
			},
			{
				nameof(YesNoDialogComponent.ConfirmActionAsync),
				(Func<Task<bool>>)BulkRetryAsync
			},
			{
				nameof(YesNoDialogComponent.AvatarIcon), Icons.Material.Filled.WarningAmber
			},
			{
				nameof(YesNoDialogComponent.AvatarColor), Color.Warning
			},
			{
				nameof(YesNoDialogComponent.InfoColor), Color.Warning
			},
			{
				nameof(YesNoDialogComponent.InfoBGColor), "var(--c-warn-bg)"
			},
			{
				nameof(YesNoDialogComponent.ThemeButtonColor), "theme-button-warning"
			}
		};

		var options = new DialogOptions
		{
			NoHeader = true,
			MaxWidth = MaxWidth.ExtraSmall,
			FullWidth = true
		};

		var dialog = await DialogService.ShowAsync<YesNoDialogComponent>(null, confirmParam, options);

		await dialog.Result;
	}

	private async Task<bool> BulkRetryAsync()
	{
		_isBulkRetrying = true;
		await InvokeAsync(StateHasChanged);

		try
		{
			// Copied before the call: the list is pruned when the table reloads, and the
			// response has to be compared against what was actually submitted.
			var requestedIds = _selectedInvitationIds.ToList();

			var response = await OMSTicketingService.RetryTicketsAsync(requestedIds);

			if (!response.IsSuccess || response.Data is null)
			{
				Snackbar.Add(response.ErrorDetail, Severity.Error);

				// The list is refreshed either way: a rejection usually means the rows moved
				// on since the page was loaded.
				await ReloadTableAsync();

				return false;
			}

			var result = response.Data;

			_selectedInvitationIds.Clear();

			await ReloadTableAsync();

			// A shortfall is normal rather than an error: the ticketing job may have picked
			// up some of the selection between rendering and clicking. Saying so is more use
			// than a flat "done".
			if (result.IsComplete)
			{
				Snackbar.Add(
					$"{result.RequeuedCount} order(s) queued for ticketing.",
					Severity.Success);
			}
			else
			{
				Snackbar.Add(
					$"{result.RequeuedCount} of {result.RequestedCount} order(s) queued. "
						+ "The rest were already back in the queue.",
					Severity.Info);
			}

			return true;
		}
		finally
		{
			_isBulkRetrying = false;
			await InvokeAsync(StateHasChanged);
		}
	}

	private async Task<bool> RetryTicketAsync(Guid emailInvitationId)
	{
		_retryingOrderId = emailInvitationId;
		await InvokeAsync(StateHasChanged);

		try
		{
			var response = await OMSTicketingService.RetryTicketAsync(emailInvitationId);

			if (!response.IsSuccess || !response.Data)
			{
				// A 409 means the row moved on since the page was loaded, so the list is
				// refreshed either way to show its real state.
				Snackbar.Add(
					response.IsSuccess ? "Failed to queue the order for ticketing." : response.ErrorDetail,
					Severity.Error);

				await ReloadTableAsync();

				return false;
			}

			Snackbar.Add("The order has been queued for ticketing.", Severity.Success);

			// The row moves out of Error and the chip counts change, so both are reloaded.
			await ReloadTableAsync();

			return true;
		}
		finally
		{
			_retryingOrderId = null;
			await InvokeAsync(StateHasChanged);
		}
	}

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
