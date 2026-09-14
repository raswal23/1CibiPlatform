using FrontendWebassembly.Services.ATS.Report;
using Microsoft.JSInterop;

namespace FrontendWebassembly.Component.ATS;

public partial class ApplicationFormPreviewComponent
{
	[Parameter]
	public ApplicationFormPreviewDTO? Preview { get; set; }

	[Parameter]
	public Guid EmailInvitationId { get; set; }

	[CascadingParameter]
	private IMudDialogInstance MudDialog { get; set; } = default!;

	[Inject]
	private IReportService ReportService { get; set; } = default!;

	[Inject]
	private IJSRuntime JS { get; set; } = default!;

	[Inject]
	private ISnackbar Snackbar { get; set; } = default!;

	private bool isDownloading;

	private void Close()
	{
		MudDialog.Cancel();
	}

	// The server renders the same preview data into a PDF (QuestPDF), so the
	// downloaded document always matches what this dialog shows.
	private async Task DownloadPdfAsync()
	{
		if (isDownloading)
			return;

		isDownloading = true;

		try
		{
			var downloadResponse = await ReportService.DownloadApplicationFormPreviewAsync(EmailInvitationId);

			if (!downloadResponse.IsSuccess || downloadResponse.Data is null)
			{
				Snackbar.Add(downloadResponse.ErrorDetail, Severity.Error);
				return;
			}

			var response = downloadResponse.Data;
			var fileBytes = await response.Content.ReadAsByteArrayAsync();

			var fileName =
				response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
				?? $"{Preview?.SubjectName?.Replace(" ", "_") ?? "Application"}_Application_Form.pdf";

			await JS.InvokeVoidAsync("downloadFile", fileName, "application/pdf", fileBytes);
		}
		finally
		{
			isDownloading = false;
		}
	}
}
