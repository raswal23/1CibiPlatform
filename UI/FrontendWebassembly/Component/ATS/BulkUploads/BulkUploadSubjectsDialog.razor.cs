namespace FrontendWebassembly.Component.ATS;

public partial class BulkUploadSubjectsDialog
{
	// null is the "All" segment; the other three are the BulkSubjectEmailStatus
	// vocabulary the backend accepts as a filter.
	private static readonly StatusSegment[] StatusSegments =
	[
		new StatusSegment(null, "All", "is-all"),
		new StatusSegment(BulkSubjectEmailStatus.Pending, "Pending", "is-pending"),
		new StatusSegment(BulkSubjectEmailStatus.Sent, "Sent", "is-done"),
		new StatusSegment(BulkSubjectEmailStatus.Failed, "Failed", "is-failed")
	];

	[CascadingParameter]
	private IMudDialogInstance MudDialog { get; set; } = default!;

	[Parameter]
	public Guid FileID { get; set; }

	// The row the user clicked, so the header renders immediately instead of waiting
	// for the first page. Replaced by the server's copy once that page lands.
	[Parameter]
	public BulkUploadListDTO? Upload { get; set; }

	private readonly CursorTableLoader<BulkUploadSubjectListDTO> _subjectsLoader = new();

	private TableComponent<BulkUploadSubjectListDTO>? _subjectsTable;
	private BulkUploadHeaderDTO? _file;
	private BulkUploadSubjectCountsDTO _counts = new();
	private Guid? _resendingInvitationId;
	private string? _activeStatus;
	private string? _searchString;
	private bool _isLoadingCounts;
	private bool _isExporting;

	// This dialog is not a page, so it cannot inherit CrudPageBase and its
	// LoadCursorPagedDataAsync helper; it calls the loader directly with its own error
	// callback, exactly as SearchReportComponent does.
	private async Task<TableData<BulkUploadSubjectListDTO>> LoadSubjectsAsync(
		TableState state,
		CancellationToken cancellationToken)
	{
		// Every input that invalidates the keyset walk must be in the signature.
		var signature = string.Join('|', FileID, _activeStatus, _searchString);

		var tableData = await _subjectsLoader.LoadAsync(
			state,
			signature,
			async (cursor, pageSize) =>
			{
				var response = await BulkUploadService.GetSubjectsAsync(
					FileID,
					cursor,
					pageSize,
					_activeStatus,
					_searchString);

				if (response.IsSuccess && response.Data?.Subjects is not null)
				{
					// The server's header wins over the row the caller passed in: it is
					// the copy that was actually scope-checked.
					_file = response.Data.File;

					return ServiceResponse<KeysetPaginatedResult<BulkUploadSubjectListDTO>>
						.Success(response.Data.Subjects);
				}

				return ServiceResponse<KeysetPaginatedResult<BulkUploadSubjectListDTO>>
					.Failure(response.ErrorDetail);
			},
			message => Snackbar.Add(message, Severity.Error));

		// The chips track the same search filter as the table, so they refresh with it
		// rather than drifting out of step.
		await RefreshCountsAsync();

		return tableData;
	}

	private async Task SetStatusAsync(string? emailStatus)
	{
		if (_activeStatus == emailStatus)
		{
			return;
		}

		_activeStatus = emailStatus;

		// A changed filter starts a new keyset walk; keep MudTable's page in sync with
		// the loader's reset-to-first-page or the pager shows a stale page.
		if (_subjectsTable?.TableRef is not null)
		{
			_subjectsTable.TableRef.CurrentPage = 0;
		}

		await ReloadTableAsync();
	}

	private async Task ReloadTableAsync()
	{
		if (_subjectsTable?.TableRef is not null)
		{
			await _subjectsTable.TableRef.ReloadServerData();
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
			var response = await BulkUploadService.GetSubjectCountsAsync(
				FileID,
				_searchString);

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

	private async Task ExportSubjectsAsync()
	{
		if (_isExporting)
		{
			return;
		}

		_isExporting = true;

		try
		{
			// The export is unfiltered on purpose: the file it names is the whole bulk
			// upload, so a CSV that silently honoured the active chip would mislead.
			var response = await BulkUploadService.ExportSubjectsAsync(FileID);

			if (!response.IsSuccess || response.Data is null)
			{
				Snackbar.Add(response.ErrorDetail, Severity.Error);
				return;
			}

			var fileBytes = await response.Data.Content.ReadAsByteArrayAsync();

			var fileName =
				response.Data.Content.Headers.ContentDisposition?.FileName?.Trim('"')
				?? "bulk-upload-subjects.csv";

			await JS.InvokeVoidAsync("downloadFile", fileName, "text/csv", fileBytes);

			Snackbar.Add($"Exported {_counts.Total} subject(s).", Severity.Success);
		}
		finally
		{
			_isExporting = false;
			await InvokeAsync(StateHasChanged);
		}
	}

	private async Task ConfirmResendAsync(BulkUploadSubjectListDTO subject)
	{
		var confirmParameters = new DialogParameters
		{
			{
				nameof(YesNoDialogComponent.Title),
				"Resend Application"
			},
			{
				nameof(YesNoDialogComponent.Message),
				$"This will send a new application form to {FormatName(subject)}."
			},
			{
				nameof(YesNoDialogComponent.ConfirmText),
				"Resend"
			},
			{
				nameof(YesNoDialogComponent.InformationMessage),
				$"Clicking \"Resend\" emails a fresh application link to {subject.EmailAddress} "
					+ "and invalidates the previous one."
			},
			{
				nameof(YesNoDialogComponent.ConfirmIcon),
				Icons.Material.Outlined.Refresh
			},
			{
				nameof(YesNoDialogComponent.ConfirmActionAsync),
				(Func<Task<bool>>)(() => ResendAsync(subject.EmailInvitationID))
			},
			{
				nameof(YesNoDialogComponent.AvatarIcon),
				Icons.Material.Filled.WarningAmber
			},
			{
				nameof(YesNoDialogComponent.AvatarColor),
				Color.Warning
			},
			{
				nameof(YesNoDialogComponent.InfoColor),
				Color.Warning
			},
			{
				nameof(YesNoDialogComponent.InfoBGColor),
				"#FCF1DD"
			},
			{
				nameof(YesNoDialogComponent.ThemeButtonColor),
				"theme-button-warning"
			}
		};

		var options = new DialogOptions
		{
			NoHeader = true,
			MaxWidth = MaxWidth.ExtraSmall,
			FullWidth = true
		};

		var dialog = await DialogService.ShowAsync<YesNoDialogComponent>(
			null,
			confirmParameters,
			options);

		await dialog.Result;
	}

	private async Task<bool> ResendAsync(Guid emailInvitationId)
	{
		_resendingInvitationId = emailInvitationId;
		await InvokeAsync(StateHasChanged);

		try
		{
			var response = await EndorsementSubmissionService.ResendApplicationFormAsync(
				emailInvitationId);

			if (!response.IsSuccess)
			{
				Snackbar.Add(response.ErrorDetail, Severity.Error);
				return false;
			}

			if (!response.Data)
			{
				Snackbar.Add("Failed to resend application form.", Severity.Error);
				return false;
			}

			// The row's statuses have just changed, so reload rather than patch in place.
			await ReloadTableAsync();

			Snackbar.Add("Application form resent successfully.", Severity.Success);
			return true;
		}
		finally
		{
			_resendingInvitationId = null;
			await InvokeAsync(StateHasChanged);
		}
	}

	private void Cancel() => MudDialog.Cancel();

	// Resend is offered only where it helps. A Pending or Processing invitation is
	// already in the email job's queue, so resending would double-send; a completed
	// form has nothing left to fill in.
	private static bool CanResend(BulkUploadSubjectListDTO subject)
	{
		if (string.Equals(
			subject.ApplicationFormStatus,
			SubjectApplicationFormStatus.Done,
			StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		if (string.Equals(
			subject.EmailSentStatus,
			SubjectEmailSentStatus.Error,
			StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		// Delivered but not acted on, or withdrawn: a nudge is legitimate.
		return string.Equals(
			subject.EmailSentStatus,
			SubjectEmailSentStatus.Done,
			StringComparison.OrdinalIgnoreCase);
	}

	private static string GetResendTooltip(BulkUploadSubjectListDTO subject) =>
		string.Equals(
			subject.EmailSentStatus,
			SubjectEmailSentStatus.Error,
			StringComparison.OrdinalIgnoreCase)
			? "The invitation email failed. Resend it."
			: "Resend the application form to this subject.";

	private static string GetResendBlockedReason(BulkUploadSubjectListDTO subject) =>
		string.Equals(
			subject.ApplicationFormStatus,
			SubjectApplicationFormStatus.Done,
			StringComparison.OrdinalIgnoreCase)
			? "This subject already completed their application form."
			: "The invitation email has not been sent yet. Resending would send it twice.";

	private long CountFor(string? emailStatus) => emailStatus switch
	{
		BulkSubjectEmailStatus.Pending => _counts.Pending,
		BulkSubjectEmailStatus.Sent => _counts.Sent,
		BulkSubjectEmailStatus.Failed => _counts.Failed,
		_ => _counts.Total
	};

	private string GetSegmentClass(string? emailStatus) =>
		_activeStatus == emailStatus
			? "ats-segment-btn ats-bulk-subjects-segment active"
			: "ats-segment-btn ats-bulk-subjects-segment";

	private string FileName =>
		_file?.FileName
		?? Upload?.FileName
		?? "Bulk upload";

	private string HeaderSubtitle
	{
		get
		{
			var packageType = _file?.PackageType ?? Upload?.PackageType;
			var orderType = _file?.OrderType ?? Upload?.OrderType;
			var dateCreated = _file?.DateCreated ?? Upload?.DateCreated;

			var parts = new List<string>();

			if (!string.IsNullOrWhiteSpace(packageType))
			{
				parts.Add(packageType);
			}

			if (!string.IsNullOrWhiteSpace(orderType))
			{
				parts.Add(orderType);
			}

			if (dateCreated.HasValue)
			{
				parts.Add($"uploaded {FormatRelative(dateCreated)}");
			}

			return parts.Count == 0
				? "Subjects created from this file"
				: string.Join(" · ", parts);
		}
	}

	private static string FormatName(BulkUploadSubjectListDTO subject)
	{
		var middleInitial = string.IsNullOrWhiteSpace(subject.MiddleInitial)
			? string.Empty
			: $" {subject.MiddleInitial.Trim()}.";

		var fullName = $"{subject.FirstName}{middleInitial} {subject.LastName}".Trim();

		return string.IsNullOrWhiteSpace(fullName) ? "Unnamed subject" : fullName;
	}

	private static string GetInitials(string? firstName, string? lastName)
	{
		var firstInitial = string.IsNullOrWhiteSpace(firstName)
			? string.Empty
			: firstName.Trim()[0].ToString();

		var lastInitial = string.IsNullOrWhiteSpace(lastName)
			? string.Empty
			: lastName.Trim()[0].ToString();

		var initials = $"{firstInitial}{lastInitial}".ToUpperInvariant();

		return string.IsNullOrWhiteSpace(initials) ? "?" : initials;
	}

	// Processing is the email job's internal claim state; a requestor only needs to
	// know the invitation has not gone out yet.
	private static string GetEmailStatusLabel(string? emailSentStatus) => emailSentStatus switch
	{
		SubjectEmailSentStatus.Done => "Sent",
		SubjectEmailSentStatus.Error => "Failed",
		SubjectEmailSentStatus.Pending or SubjectEmailSentStatus.Processing => "Pending",
		_ => "Unknown"
	};

	private static string GetEmailStatusClass(string? emailSentStatus) => emailSentStatus switch
	{
		SubjectEmailSentStatus.Done => "is-done",
		SubjectEmailSentStatus.Error => "is-failed",
		SubjectEmailSentStatus.Pending or SubjectEmailSentStatus.Processing => "is-pending",
		_ => "is-unknown"
	};

	private static string GetFormStatusLabel(string? applicationFormStatus) => applicationFormStatus switch
	{
		SubjectApplicationFormStatus.Done => "Completed",
		SubjectApplicationFormStatus.Withdrawn => "Withdrawn",
		SubjectApplicationFormStatus.Pending => "Awaiting subject",
		_ => "Unknown"
	};

	private static string GetFormStatusClass(string? applicationFormStatus) => applicationFormStatus switch
	{
		SubjectApplicationFormStatus.Done => "is-done",
		SubjectApplicationFormStatus.Withdrawn => "is-failed",
		SubjectApplicationFormStatus.Pending => "is-pending",
		_ => "is-unknown"
	};

	private static string FormatAbsolute(DateTime? timestamp) =>
		timestamp?.ToLocalTime().ToString("MMMM dd, yyyy h:mm tt") ?? "—";

	private static string FormatRelative(DateTime? timestamp)
	{
		if (timestamp is not { } value)
		{
			return "—";
		}

		var elapsed = DateTime.UtcNow - DateTime.SpecifyKind(value, DateTimeKind.Utc);

		// A clock skew between the browser and the server can make a fresh row look
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

	private string EmptyTitle => HasActiveFilter
		? "No matching subjects"
		: "No subjects yet";

	private string EmptySubtitle => HasActiveFilter
		? "No subject in this file matches the current filter. Clear the search or pick another status."
		: "This file has not been parsed yet. Subjects appear here within seconds of the upload being picked up.";

	private bool HasActiveFilter =>
		_activeStatus is not null || !string.IsNullOrWhiteSpace(_searchString);

	private sealed record StatusSegment(string? Value, string Label, string Modifier);
}
