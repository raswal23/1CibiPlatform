namespace FrontendWebassembly.Pages.EmploymentVerification;

public partial class NeedsRequest
{
	/// <summary>
	/// Employment slots from ATS with no live verification request. One entry per
	/// employer slot, so a candidate with three former employers appears three times.
	/// </summary>
	private readonly List<ATSInProgressEmploymentRecordDTO> _candidates = [];

	private TableComponent<ATSInProgressEmploymentRecordDTO>? _candidatesTable;

	private string _searchString = string.Empty;

	private bool HasSearch => !string.IsNullOrWhiteSpace(_searchString);

	/// <summary>
	/// Records matching the current term. The endpoint returns the whole list in one
	/// call, so filtering stays client side and needs no server round trip.
	/// </summary>
	private List<ATSInProgressEmploymentRecordDTO> FilteredCandidates =>
		!HasSearch
			? _candidates
			: _candidates
				.Where(candidate =>
					EmploymentVerificationDisplay.Matches(candidate.CandidateName, _searchString) ||
					EmploymentVerificationDisplay.Matches(candidate.Employer, _searchString) ||
					EmploymentVerificationDisplay.Matches(candidate.Position, _searchString) ||
					EmploymentVerificationDisplay.Matches(candidate.SupervisorEmail, _searchString))
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
		var result = await VerificationService.GetInProgressATSRecordsAsync();

		_candidates.Clear();

		if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
		{
			Snackbar.Add(result.ErrorMessage, Severity.Error);
			return;
		}

		_candidates.AddRange(result.Data ?? []);
	}

	private Task ReloadAsync() => LoadAsync();

	/// <summary>
	/// Why a record is still here. Everything on this screen is by definition not yet
	/// sent - the server already filtered out slots with a live request - so the only
	/// question is whether the next pass will pick it up, and if not, what is missing.
	/// </summary>
	/// <remarks>
	/// These states are derived, not stored. A sent request becomes a row in the
	/// verification table and leaves this list entirely; there is no "queued" record
	/// anywhere, only the absence of a sent one.
	/// </remarks>
	private static string GetQueueStatus(ATSInProgressEmploymentRecordDTO candidate)
	{
		if (!candidate.PermissionToContact)
		{
			return "No consent";
		}

		if (string.IsNullOrWhiteSpace(candidate.SupervisorEmail))
		{
			return "No email";
		}

		// Deliberately does not claim "Queued". The job will only write to an address
		// the contact directory lists, and this screen does not know whether it does -
		// checking per row would mean a lookup per row. "Pending check" is honest about
		// that; the Tracking tab shows what actually went out.
		return "Pending check";
	}

	/// <summary>
	/// Maps the queue state onto the shared .ats-status-pill vocabulary so a chip looks
	/// the same here as on the ATS boards.
	/// </summary>
	private static string GetQueueStatusCssClass(ATSInProgressEmploymentRecordDTO candidate) =>
		GetQueueStatus(candidate) switch
		{
			"Pending check" => "ats-status-pill processing",
			"No consent" => "ats-status-pill unknown",
			_ => "ats-status-pill pending"
		};
}
