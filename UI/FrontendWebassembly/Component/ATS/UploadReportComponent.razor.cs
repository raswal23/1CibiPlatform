namespace FrontendWebassembly.Component.ATS;

public partial class UploadReportComponent
{
	[Parameter]
	public Guid EmailInvitationRequestId { get; set; }
	[Parameter]
	public EventCallback OnUploadSucceeded { get; set; }
	[Parameter]
	public EventCallback OnCancel { get; set; }

	[CascadingParameter]
	private IMudDialogInstance MudDialog { get; set; } = default!;
	private MudForm? uploadReportForm;

	private ReportDetailsDTO reportDetails = new();
	private string? reportFileName;

	private bool isUploading = false;
	private MudFileUpload<IBrowserFile> reportFileUpload = default!;

	private async Task OnReportFileUpload(InputFileChangeEventArgs e)
	{
		var result = FileValidationService.ValidateExtension(e.File.Name, ".pdf");

		if (!result.IsValid)
		{
			Snackbar.Add(result.ErrorMessage!, Severity.Error);
			return;
		}

		if (e.File is not null)
		{
			reportDetails.ReportFile = e.File;
			reportFileName = e.File.Name;
		}

		return;
	}

	private async Task RemoveFileFromUploadsAsync(IBrowserFile file)
	{
		if (await reportFileUpload.RemoveFileAsync(file))
		{
			reportDetails.ReportFile = null;
			reportFileName = null;
			return;
		}
	}

	private void CloseDialog()
	{
		MudDialog.Close();
	}

   private async Task SubmitUploadReport()
	{
		await uploadReportForm!.ValidateAsync();

		if (!uploadReportForm.IsValid)
			return;

		if (EmailInvitationRequestId == Guid.Empty)
		{
			Snackbar.Add("Email invitation request ID is required.", Severity.Error);
			return;
		}

		if (string.IsNullOrWhiteSpace(reportDetails.HitStatus) || string.IsNullOrWhiteSpace(reportDetails.ReportStatus) || reportDetails.ReportFile is null)
		{
			Snackbar.Add("Please complete required fields and upload a report file.", Severity.Error);
			return;
		}

		try
		{
			isUploading = true;
			await InvokeAsync(StateHasChanged);

			reportDetails.EmailInvitationRequestId = EmailInvitationRequestId;

           var success = await ReportUploadService.UploadReportAsync(reportDetails);

			if (!success)
			{
				Snackbar.Add("Failed to upload report.", Severity.Error);
				return;
			}

			await OnUploadSucceeded.InvokeAsync();

			Snackbar.Add("Report uploaded successfully.", Severity.Success);

			reportDetails = new ReportDetailsDTO
			{
				EmailInvitationRequestId = EmailInvitationRequestId
			};

			await uploadReportForm.ResetAsync();
		}
		finally
		{
			isUploading = false;
			await InvokeAsync(StateHasChanged);
		}

	}

}
