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
	private bool BiometricPhotoSelected;

	private int TotalDocuments => 6;

	private int AvailableDocumentCount =>
		(!string.IsNullOrWhiteSpace(ReportResult?.BiometricPhotoFileName) ? 1 : 0)
		+ (!string.IsNullOrWhiteSpace(ReportResult?.ResumeFileName) ? 1 : 0)
		+ (!string.IsNullOrWhiteSpace(ReportResult?.IdUploadedFileName) ? 1 : 0)
		+ (!string.IsNullOrWhiteSpace(ReportResult?.DiplomaFileName) ? 1 : 0)
		+ (!string.IsNullOrWhiteSpace(ReportResult?.CoeFileName) ? 1 : 0)
		+ (!string.IsNullOrWhiteSpace(ReportResult?.ConsentFormFileName) ? 1 : 0)
		+ (!string.IsNullOrWhiteSpace(ReportResult?.UploadedReportFileName) ? 1 : 0);

	private bool HasSelectedFile =>
		BiometricPhotoSelected || ResumeSelected || GovernmentIdSelected || DiplomaSelected || CoeSelected || ConsentSelected || ReportSelected;

	private int SelectedFileCount =>
		(BiometricPhotoSelected ? 1 : 0)
		+ (ResumeSelected ? 1 : 0)
		+ (GovernmentIdSelected ? 1 : 0)
		+ (DiplomaSelected ? 1 : 0)
		+ (CoeSelected ? 1 : 0)
		+ (ConsentSelected ? 1 : 0)
		+ (ReportSelected ? 1 : 0);

	public async Task DownloadDocumentsAsync()
	{
		if (!HasSelectedFile)
		{
			Snackbar.Add("Please select at least one file first.", Severity.Warning);
			return;
		}

		DownloadRequest.FileDocuments.Clear();

		if (BiometricPhotoSelected)
			DownloadRequest.FileDocuments.Add(new DownloadIndividualDocuments
			{
				FileKey = ReportResult!.BiometricPhotoFileKey!,
				FileName = ReportResult.BiometricPhotoFileName
			});

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
