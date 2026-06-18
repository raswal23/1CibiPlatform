namespace FrontendWebassembly.Component.PhilSys;

public partial class PhilSysResultComponent
{
	[Parameter]
	public UpdateFaceLivenessSessionResponseDTO? VerificationResult { get; set; }
	[Parameter]
	public string? WebHookUrl { get; set; }

	private List<(string Label, string Value)>? InfoRows;

	private string? FaceUrl;

	protected override void OnInitialized()
	{
		FaceUrl = VerificationResult?.data_subject?.face_url;
		InfoRows = new()
	{
		("PhilSys Card Number", VerificationResult?.data_subject?.national_id_number ?? "No Data Found"),
		("Digital ID Number", VerificationResult?.data_subject?.digital_id ?? "No Data Found"),
		("First Name", VerificationResult?.data_subject?.first_name ?? "No Data Found"),
		("Middle Name", VerificationResult?.data_subject?.middle_name ?? "No Data Found"),
		("Last Name", VerificationResult?.data_subject?.last_name ?? "No Data Found"),
		("Suffix",  VerificationResult?.data_subject?.suffix ?? "No Data Found"),
		("Gender", VerificationResult?.data_subject?.gender ?? "No Data Found"),
		("Birth Date", VerificationResult?.data_subject?.birth_date ?? "No Data Found"),
		("Place of Birth", VerificationResult?.data_subject?.place_of_birth ?? "No Data Found"),
		("Permanent Address", VerificationResult?.data_subject?.full_address ?? "No Data Found"),
		("Present Address", VerificationResult?.data_subject?.present_full_address ?? "No Data Found"),
		("Marital Status", VerificationResult?.data_subject?.marital_status ?? "No Data Found"),
		("Mobile Number", VerificationResult?.data_subject?.mobile_number ?? "No Data Found"),
		("Email", VerificationResult?.data_subject?.email ?? "No Data Found"),
		("Blood Type", VerificationResult?.data_subject?.blood_type ?? "No Data Found")
	};
	}

	private async Task DownloadApiResponse()
	{
		if (VerificationResult == null)
			return;

		var json = JsonSerializer.Serialize(
			new
			{
				VerificationResult.idv_session_id,
				VerificationResult.verified,
				VerificationResult.data_subject
			},
			new JsonSerializerOptions { WriteIndented = true }
		);

		var fileName = $"ApiResponse_{DateTime.Now:MM-dd-yyyy (hh-mm-ss tt)}.json";

		await JS.InvokeVoidAsync("downloadFileFromBlazor", fileName, "application/json", json);
	}

	private void SearchAgain()
	{
		NavigationManager.NavigateTo("/philsys/idv");
	}
}
