namespace FrontendWebassembly.Component.ATS;

public partial class UploadReportComponent
{
	string hitstatus;
	string reportstatus;
	string name;
	IBrowserFile report;
	string reportfilename;
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
			report = e.File;
			reportfilename = e.File.Name;
		}

		return;
	}

	private async Task RemoveFileFromUploadsAsync(IBrowserFile file)
	{
		if (await reportFileUpload.RemoveFileAsync(file))
		{
			report = null;
			reportfilename = null;
			return;
		}
	}

	private void SubmitUploadReport()
	{

	}

}
