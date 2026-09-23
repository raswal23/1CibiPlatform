namespace FrontendWebassembly.Pages.EmploymentVerification;

public partial class NeedsRequest
{
	/// <summary>Candidates from ATS that still need a verification email.</summary>
	private readonly List<ATSInProgressEmploymentRecordDTO> _candidates = [];

	private TableComponent<ATSInProgressEmploymentRecordDTO>? _candidatesTable;
	private ElementReference _drawerElement;

	private string _searchString = string.Empty;
	private bool _isSubmitting;
	private bool _shouldFocusDrawer;

	private ATSInProgressEmploymentRecordDTO? SelectedCandidate { get; set; }

	private bool HasSearch => !string.IsNullOrWhiteSpace(_searchString);

	/// <summary>
	/// Candidates matching the current term. The endpoint returns the whole list in
	/// one call, so filtering stays client side and needs no server round trip. If
	/// that endpoint ever becomes keyset-paginated, this moves to
	/// <c>CrudPageBase.LoadCursorPagedDataAsync</c> the way Contacts does.
	/// </summary>
	private List<ATSInProgressEmploymentRecordDTO> FilteredCandidates =>
		!HasSearch
			? _candidates
			: _candidates
				.Where(candidate =>
					EmploymentVerificationDisplay.Matches(candidate.CandidateName, _searchString) ||
					EmploymentVerificationDisplay.Matches(candidate.Employer, _searchString) ||
					EmploymentVerificationDisplay.Matches(candidate.Position, _searchString) ||
					EmploymentVerificationDisplay.Matches(candidate.HrEmail, _searchString))
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

	/// <summary>
	/// Refetches the list. TableComponent already guards against a second click while
	/// this is in flight, so no re-entrancy flag is needed here.
	/// </summary>
	private Task ReloadAsync() => LoadAsync();

	private void ViewCandidate(ATSInProgressEmploymentRecordDTO candidate)
	{
		SelectedCandidate = candidate;

		// Focus moves to the drawer on the next render so Escape reaches it and
		// keyboard users are not left behind on the table row.
		_shouldFocusDrawer = true;
	}

	private void CloseCandidate() => SelectedCandidate = null;

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

	private async Task SendSelectedRequestAsync()
	{
		if (SelectedCandidate is null || string.IsNullOrWhiteSpace(SelectedCandidate.HrEmail))
		{
			Snackbar.Add("The selected record does not have an HR email.", Severity.Warning);
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
				EmploymentStartDate = EmploymentVerificationDisplay.ToDateTime(SelectedCandidate.StartDate)
					?? DateTime.UtcNow.AddYears(-2),
				EmploymentEndDate = EmploymentVerificationDisplay.ToDateTime(SelectedCandidate.EndDate)
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

			// The candidate now has an open request, so it leaves this list and
			// appears under Tracking.
			await LoadAsync();
		}
		finally
		{
			_isSubmitting = false;
		}
	}
}
