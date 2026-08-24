namespace FrontendWebassembly.Component.ATS;

public partial class SelectFilesToDownloadComponent
{
	[Parameter]
	public ATSResultDetailsDTO? ReportResult { get; set; }

	/// <summary>The order whose documents these are. Sent instead of storage keys.</summary>
	[Parameter]
	public Guid EmailInvitationId { get; set; }

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

		// Send which kinds of document we want; the server resolves the storage keys
		// itself, under the caller's access scope. It used to accept keys from here,
		// which meant the browser could name any object in the bucket.
		DownloadRequest.EmailInvitationRequestId = EmailInvitationId;
		DownloadRequest.DocumentTypes.Clear();

		if (BiometricPhotoSelected)
			DownloadRequest.DocumentTypes.Add(AtsDocumentTypes.BiometricPhoto);

		if (ResumeSelected)
			DownloadRequest.DocumentTypes.Add(AtsDocumentTypes.Resume);

		if (GovernmentIdSelected)
			DownloadRequest.DocumentTypes.Add(AtsDocumentTypes.GovernmentId);

		if (DiplomaSelected)
			DownloadRequest.DocumentTypes.Add(AtsDocumentTypes.Diploma);

		if (CoeSelected)
			DownloadRequest.DocumentTypes.Add(AtsDocumentTypes.Coe);

		if (ConsentSelected)
			DownloadRequest.DocumentTypes.Add(AtsDocumentTypes.ConsentForm);

		if (ReportSelected)
			DownloadRequest.DocumentTypes.Add(AtsDocumentTypes.Report);

		var downloadResponse = await ReportService.DownloadDocumentsAsync(DownloadRequest);

		if (!downloadResponse.IsSuccess || downloadResponse.Data is null)
		{
			Snackbar.Add(downloadResponse.ErrorDetail, Severity.Error);
			return;
		}

		var response = downloadResponse.Data;
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
