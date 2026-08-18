using FrontendWebassembly.DTO.EmploymentVerification;


namespace FrontendWebassembly.Pages.EmploymentVerification;

public partial class EmploymentVerification
{
	/// <summary>Candidates from ATS that still need a verification email.</summary>
	private readonly List<ATSInProgressEmploymentRecordDTO> Candidates = [];

	/// <summary>Requests already raised from this module, with their outcome.</summary>
	private readonly List<SentVerificationRequestDTO> SentRequests = [];

	private const string NeedsRequestView = "needs";
	private const string TrackingView = "tracking";

	private string _activeView = NeedsRequestView;
	private ATSInProgressEmploymentRecordDTO? SelectedCandidate;
	private bool _isLoading = true;
	private bool _isSubmitting;
	private bool _isReloading;
	private bool _shouldFocusDrawer;
	private ElementReference _drawerElement;

	// Each view keeps its own term so switching tabs does not carry a filter
	// across to a list where it means something different.
	private string _candidateSearch = string.Empty;
	private string _trackingSearch = string.Empty;

	private bool IsNeedsRequestView => _activeView == NeedsRequestView;

	/// <summary>
	/// Candidates matching the current term. Both lists are already held in
	/// memory, so filtering stays client side and needs no server round trip.
	/// </summary>
	private List<ATSInProgressEmploymentRecordDTO> FilteredCandidates =>
		string.IsNullOrWhiteSpace(_candidateSearch)
			? Candidates
			: Candidates
				.Where(candidate =>
					Matches(candidate.CandidateName, _candidateSearch) ||
					Matches(candidate.Employer, _candidateSearch) ||
					Matches(candidate.Position, _candidateSearch) ||
					Matches(candidate.HrEmail, _candidateSearch))
				.ToList();

	private List<SentVerificationRequestDTO> FilteredSentRequests =>
		string.IsNullOrWhiteSpace(_trackingSearch)
			? SentRequests
			: SentRequests
				.Where(request =>
					Matches(request.CandidateName, _trackingSearch) ||
					Matches(request.PreviousEmployer, _trackingSearch) ||
					Matches(request.Position, _trackingSearch) ||
					Matches(request.HrEmail, _trackingSearch) ||
					Matches(GetDisplayStatus(request), _trackingSearch))
				.ToList();

	private static bool Matches(string? value, string term) =>
		!string.IsNullOrWhiteSpace(value) &&
		value.Contains(term.Trim(), StringComparison.OrdinalIgnoreCase);

	private string ActiveSearch =>
		IsNeedsRequestView ? _candidateSearch : _trackingSearch;

	private bool HasActiveSearch => !string.IsNullOrWhiteSpace(ActiveSearch);

	private void OnSearchChanged(ChangeEventArgs args)
	{
		var value = args.Value?.ToString() ?? string.Empty;

		if (IsNeedsRequestView)
		{
			_candidateSearch = value;
		}
		else
		{
			_trackingSearch = value;
		}
	}

	private void ClearSearch()
	{
		if (IsNeedsRequestView)
		{
			_candidateSearch = string.Empty;
		}
		else
		{
			_trackingSearch = string.Empty;
		}
	}

	/// <summary>
	/// Refetches both lists. Guarded so a double click cannot start a second
	/// request while the first is still running.
	/// </summary>
	private async Task ReloadAsync()
	{
		if (_isReloading || _isLoading)
		{
			return;
		}

		_isReloading = true;

		try
		{
			await LoadAsync(showSkeleton: false);
		}
		finally
		{
			_isReloading = false;
		}
	}

	protected override async Task OnInitializedAsync()
	{
		await base.OnInitializedAsync();

		if (!IsPageAuthorized)
		{
			return;
		}

		await LoadAsync();
	}

	/// <summary>
	/// Loads both lists. A refresh passes <paramref name="showSkeleton"/> as false
	/// so the existing rows stay on screen instead of collapsing to the loading
	/// card while the request is in flight.
	/// </summary>
	private async Task LoadAsync(bool showSkeleton = true)
	{
		_isLoading = showSkeleton;

		try
		{
			var candidateResult = await VerificationService.GetInProgressATSRecordsAsync();
			var sentResult = await VerificationService.GetSentRequestsAsync();

			Candidates.Clear();
			SentRequests.Clear();

			if (!string.IsNullOrWhiteSpace(candidateResult.ErrorMessage))
			{
				Snackbar.Add(candidateResult.ErrorMessage, Severity.Error);
			}
			else
			{
				Candidates.AddRange(candidateResult.Data ?? []);
			}

			if (!string.IsNullOrWhiteSpace(sentResult.ErrorMessage))
			{
				Snackbar.Add(sentResult.ErrorMessage, Severity.Error);
			}
			else
			{
				SentRequests.AddRange(sentResult.Data ?? []);
			}
		}
		catch (Exception exception)
		{
			Candidates.Clear();
			SentRequests.Clear();
			Snackbar.Add(
				$"Employment Verification API error: {exception.Message}",
				Severity.Error);
		}
		finally
		{
			_isLoading = false;
		}
	}

	private void SetView(string view)
	{
		_activeView = view;
		SelectedCandidate = null;
	}

	private string GetSegmentClass(string view) =>
		_activeView == view
			? "ev-segment-btn active"
			: "ev-segment-btn";

	private async Task SendSelectedRequestAsync()
	{
		if (SelectedCandidate is null || string.IsNullOrWhiteSpace(SelectedCandidate.HrEmail))
		{
			Snackbar.Add(
				"The selected record does not have an HR email.",
				Severity.Warning);
			return;
		}

		_isSubmitting = true;

		try
		{
			var request = new CreateEmploymentVerificationRequestDTO
			{
				AtsSubjectId = SelectedCandidate.SubjectId,
				CandidateName = SelectedCandidate.CandidateName,
				PreviousEmployer = "Google", //SelectedCandidate.Employer
				Position = string.IsNullOrWhiteSpace(SelectedCandidate.Position)
					? "Not provided"
					: SelectedCandidate.Position,
				HrEmail = "contract.fullstackdev@cibi.com.ph",//SelectedCandidate.HrEmail
				EmploymentStartDate = ToDateTime(SelectedCandidate.StartDate)
					?? DateTime.UtcNow.AddYears(-2),
				EmploymentEndDate = ToDateTime(SelectedCandidate.EndDate)
					?? DateTime.UtcNow.AddMonths(-6)
			};
			var result = await VerificationService.CreateAndSendAsync(request);

			if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
			{
				Snackbar.Add(result.ErrorMessage, Severity.Error);
				return;
			}

			Snackbar.Add(result.Detail, Severity.Success);
			CloseCandidate();

			// The candidate now has an open request, so it leaves the needs-request
			// list and appears under tracking.
			await LoadAsync();
		}
		finally
		{
			_isSubmitting = false;
		}
	}

	private void GoToOnePlatform() =>
		Nav.NavigateTo("/");

	private void ViewCandidate(ATSInProgressEmploymentRecordDTO candidate)
	{
		SelectedCandidate = candidate;

		// Focus moves to the drawer on the next render so Escape reaches it and
		// keyboard users are not left behind on the table row.
		_shouldFocusDrawer = true;
	}

	private void CloseCandidate() =>
		SelectedCandidate = null;

	/// <summary>
	/// Escape closes the drawer. Backdrop clicks deliberately do not, so an
	/// accidental click outside cannot discard the request being reviewed.
	/// </summary>
	private void HandleDrawerKeyDown(KeyboardEventArgs args)
	{
		if (args.Key == "Escape")
		{
			CloseCandidate();
		}
	}

	protected override async Task OnAfterRenderAsync(bool firstRender)
	{
		if (!_shouldFocusDrawer)
		{
			return;
		}

		_shouldFocusDrawer = false;
		await _drawerElement.FocusAsync();
	}

	private string GetSendButtonText() =>
		_isSubmitting
			? "Sending…"
			: "Send verification email";

	private static string GetEmploymentPeriod(DateOnly? startDate, DateOnly? endDate)
	{
		if (startDate is null && endDate is null)
		{
			return "Not provided";
		}

		var start = startDate?.ToString("MMM yyyy") ?? "Unknown";
		var end = endDate?.ToString("MMM yyyy") ?? "Present";

		return $"{start} – {end}";
	}

	private static DateTime? ToDateTime(DateOnly? value) =>
		value?.ToDateTime(TimeOnly.MinValue);

	/// <summary>
	/// Share of sent requests that came back confirmed. Reported as an em dash
	/// until something has actually been sent, rather than as 0%.
	/// </summary>
	private string ResponseRate
	{
		get
		{
			var answered = SentRequests.Count(request =>
				request.Status is "Verified" or "Rejected");

			if (SentRequests.Count == 0)
			{
				return "—";
			}

			return $"{answered * 100 / SentRequests.Count}%";
		}
	}

	private int CountByStatus(string status) =>
		SentRequests.Count(request => request.Status == status);

	private static string GetRespondedOn(SentVerificationRequestDTO request)
	{
		var respondedAt = request.VerifiedAt ?? request.RejectedAt;

		return respondedAt?.ToLocalTime().ToString("MMM dd, yyyy") ?? "—";
	}

	/// <summary>
	/// A sent request whose link has lapsed is shown as expired: the backend
	/// releases the candidate for a new request at that point, but the row itself
	/// keeps its stored status.
	/// </summary>
	private static string GetDisplayStatus(SentVerificationRequestDTO request) =>
		request.Status == "Sent" && request.TokenExpiresAt < DateTime.UtcNow
			? "Expired"
			: request.Status;
}
