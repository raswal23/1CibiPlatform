namespace FrontendWebassembly.Component.ATS;

public partial class SelectFilesToDownloadComponent
{
	[Parameter]
	public ATSResultDetailsDTO? ReportResult { get; set; }

	public DownloadIndividualDocumentsRequestDTO DownloadRequest { get; set; } = new();

	[CascadingParameter]
	private IMudDialogInstance MudDialog { get; set; } = default!;

	private bool ResumeSelected;
	private bool GovernmentIdSelected;
	private bool DiplomaSelected;
	private bool CoeSelected;
	private bool ConsentSelected;
	private bool ReportSelected;

	public async Task DownloadDocumentsAsync()
	{

		if (ResumeSelected)
			DownloadRequest.FileDocuments.Add(new DownloadIndividualDocuments
			{
				FileKey = ReportResult!.ResumeFileKey!,
				FileName = ReportResult.ResumeFileName
			});

		if (GovernmentIdSelected)
			DownloadRequest.FileDocuments.Add(new DownloadIndividualDocuments
			{
				FileKey = ReportResult!.IdUploadedFileKey!,
				FileName = ReportResult.IdUploadedFileName
			});

		if (DiplomaSelected)
			DownloadRequest.FileDocuments.Add(new DownloadIndividualDocuments
			{
				FileKey = ReportResult!.DiplomaFileKey!,
				FileName = ReportResult.DiplomaFileName
			});

		if (CoeSelected)
			DownloadRequest.FileDocuments.Add(new DownloadIndividualDocuments
			{
				FileKey = ReportResult!.CoeFileKey!,
				FileName = ReportResult.CoeFileName
			});

		if (ConsentSelected)
			DownloadRequest.FileDocuments.Add(new DownloadIndividualDocuments
			{
				FileKey = ReportResult!.ConsentFormFileKey!,
				FileName = ReportResult.ConsentFormFileName
			});

		if (ReportSelected)
			DownloadRequest.FileDocuments.Add(new DownloadIndividualDocuments
			{
				FileKey = ReportResult!.UploadedReportFileKey!,
				FileName = ReportResult.UploadedReportFileName
			});

		DownloadRequest.SubjectName = ReportResult!.SubjectName;

		var response = await ReportService.DownloadDocumentsAsync(DownloadRequest);

		var fileBytes = await response.Content.ReadAsByteArrayAsync();

		var fileName =
			response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
			?? $"{ReportResult!.SubjectName!.Replace(" ", "_")}.zip";

		await JS.InvokeVoidAsync("downloadFile", fileName, "application/zip", fileBytes);
	}

	public async Task Cancel()
	{
		MudDialog.Cancel();
	}
}
