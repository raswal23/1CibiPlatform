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

	private bool IsNeedsRequestView => _activeView == NeedsRequestView;

	protected override async Task OnInitializedAsync()
	{
		await base.OnInitializedAsync();

		if (!IsPageAuthorized)
		{
			return;
		}

		await LoadAsync();
	}

	private async Task LoadAsync()
	{
		_isLoading = true;

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
				PreviousEmployer = SelectedCandidate.Employer,
				Position = string.IsNullOrWhiteSpace(SelectedCandidate.Position)
					? "Not provided"
					: SelectedCandidate.Position,
				HrEmail = SelectedCandidate.HrEmail,
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

	private void ViewCandidate(ATSInProgressEmploymentRecordDTO candidate) =>
		SelectedCandidate = candidate;

	private void CloseCandidate() =>
		SelectedCandidate = null;

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
