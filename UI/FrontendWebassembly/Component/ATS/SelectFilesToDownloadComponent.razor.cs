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

	private int TotalDocuments => 6;

	private int AvailableDocumentCount =>
		(!string.IsNullOrWhiteSpace(ReportResult?.ResumeFileName) ? 1 : 0)
		+ (!string.IsNullOrWhiteSpace(ReportResult?.IdUploadedFileName) ? 1 : 0)
		+ (!string.IsNullOrWhiteSpace(ReportResult?.DiplomaFileName) ? 1 : 0)
		+ (!string.IsNullOrWhiteSpace(ReportResult?.CoeFileName) ? 1 : 0)
		+ (!string.IsNullOrWhiteSpace(ReportResult?.ConsentFormFileName) ? 1 : 0)
		+ (!string.IsNullOrWhiteSpace(ReportResult?.UploadedReportFileName) ? 1 : 0);

	private bool HasSelectedFile =>
		ResumeSelected || GovernmentIdSelected || DiplomaSelected || CoeSelected || ConsentSelected || ReportSelected;

	public async Task DownloadDocumentsAsync()
	{
		if (!HasSelectedFile)
			return;

		DownloadRequest.FileDocuments.Clear();

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
