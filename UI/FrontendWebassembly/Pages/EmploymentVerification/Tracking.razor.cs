namespace FrontendWebassembly.Pages.EmploymentVerification;

public partial class Tracking
{
	/// <summary>Requests already raised from this module, with their outcome.</summary>
	private readonly List<SentVerificationRequestDTO> _sentRequests = [];

	private TableComponent<SentVerificationRequestDTO>? _requestsTable;

	private string _searchString = string.Empty;

	private bool HasSearch => !string.IsNullOrWhiteSpace(_searchString);

	/// <summary>
	/// Requests matching the current term. The endpoint returns the whole list in one
	/// call, so filtering stays client side. Status is matched on the *displayed*
	/// value, so searching "expired" finds rows the database still stores as "Sent".
	/// </summary>
	private List<SentVerificationRequestDTO> FilteredRequests =>
		!HasSearch
			? _sentRequests
			: _sentRequests
				.Where(request =>
					EmploymentVerificationDisplay.Matches(request.CandidateName, _searchString) ||
					EmploymentVerificationDisplay.Matches(request.PreviousEmployer, _searchString) ||
					EmploymentVerificationDisplay.Matches(request.Position, _searchString) ||
					EmploymentVerificationDisplay.Matches(request.HrEmail, _searchString) ||
					EmploymentVerificationDisplay.Matches(
						EmploymentVerificationDisplay.GetDisplayStatus(request),
						_searchString))
				.ToList();

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
		var result = await VerificationService.GetSentRequestsAsync();

		_sentRequests.Clear();

		if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
		{
			Snackbar.Add(result.ErrorMessage, Severity.Error);
			return;
		}

		_sentRequests.AddRange(result.Data ?? []);
	}

	private Task ReloadAsync() => LoadAsync();

	private int CountByStatus(string status) =>
		_sentRequests.Count(request => request.Status == status);

	/// <summary>
	/// Maps a verification status onto the shared .ats-status-pill vocabulary rather
	/// than declaring an EV-specific set of pills, so a status chip looks identical
	/// here and on the ATS boards. "Sent" is the only actively-changing state, and
	/// <c>processing</c> is the class whose dot pulses to say so.
	/// </summary>
	private static string GetStatusCssClass(SentVerificationRequestDTO request) =>
		EmploymentVerificationDisplay.GetDisplayStatus(request) switch
		{
			"Verified" => "ats-status-pill done",
			"Rejected" => "ats-status-pill error",
			"Sent" => "ats-status-pill processing",
			"Pending" => "ats-status-pill pending",
			_ => "ats-status-pill unknown"
		};
}
