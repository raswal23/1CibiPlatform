namespace FrontendWebassembly.Pages.CNX;

public partial class CNX
{
	private TableComponent<CandidateResponseDTO>? candidatesTable;
	private List<CandidateResponseDTO> currentPageData = new();
	private string? _searchStringCandidate;
	private bool isLoading = false;

	private void UpdateSearch<T>(ref string field, string value, TableComponent<T> table) where T : class
	{
		if (field != value)
		{
			field = value;
			table?.TableRef.ReloadServerData();
		}
	}

	private string searchStringCandidate
	{
		get => _searchStringCandidate!;
		set => UpdateSearch(ref _searchStringCandidate!, value, candidatesTable!);
	}

	private async Task<TableData<CandidateResponseDTO>> LoadCandidatesData(
	TableState state,
	CancellationToken cancellationToken)
	{
		isLoading = true;
		StateHasChanged(); // refresh UI immediately

		try
		{
			var pageNumber = (state.Page + 1).ToString();

			var result = await CandidateService.GetCandidates(
				searchStringCandidate,
				pageNumber,
				cancellationToken);

			currentPageData = result.Candidate ?? new List<CandidateResponseDTO>();

			return new TableData<CandidateResponseDTO>
			{
				TotalItems = result.Total,
				Items = currentPageData
			};
		}
		finally
		{
			isLoading = false;
			StateHasChanged(); // refresh UI again
		}
	}

	private async Task DownloadCandidateData()
	{
		if (currentPageData == null || !currentPageData.Any())
			return;

		var fileName = $"Candidates_Page_{DateTime.Now:MM-dd-yyyy(HH-mm-ss)}.xlsx";

		var dataForExcel = currentPageData.Select(c => new Dictionary<string, object>
		{
			["Candidate ID"] = c.CandidateId.ToString(),
			["Job Requisition ID"] = c.JobRequisitionId!,
			["First Name"] = c.FirstName!,
			["Middle Name"] = c.MiddleName!,
			["Last Name"] = c.LastName!,
			["Date Of Birth"] = c.DateOfBirth!,
			["Email"] = c.Email!,
			["User Phone Number"] = c.UserPhoneNumber!,
			["Marital Status"] = c.MaritalStatus!,
			["Package Account Name"] = c.PackageAccountName!,
			["CampaignTitle"] = c.CampaignTitle!,
			["Msa"] = c.Msa!,
			["Job Requisition Primary Location"] = c.JobRequisitionPrimaryLocation!,
			["Gender"] = c.Gender!,
			["Hire Date"] = c.HireDate!,
			["School Name"] = c.SchoolName!,
			["Education"] = c.Education!,
			["City"] = c.City!,
			["Postal Code"] = c.PostalCode!,
			["Address Line1"] = c.AddressLine1!,
			["SSS Number"] = c.SssNumber!,
			["Extracted SSS Number"] = c.ExtractedSssNumber!,
			["TIN Number"] = c.TinNumber!,
			["Extracted TIN Number"] = c.ExtractedTinNumber!,
			["PhilHealth Number"] = c.PhilhealthNumber!,
			["Extracted PhilHealth Number"] = c.ExtractedPhilhealthNumber!,
			["Pag-IBIG Number"] = c.PagIbigNumber!,
			["Extracted Pag-IBIG Number"] = c.ExtractedPagIbigNumber!
		}).ToList();

		await JS.InvokeVoidAsync("downloadExcel", fileName, dataForExcel);
	}
}
