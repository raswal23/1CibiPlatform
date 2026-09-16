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
	private bool NbiSelected;
	private bool DiplomaSelected;
	private bool Coe1Selected;
	private bool Coe2Selected;
	private bool Coe3Selected;
	private bool ConsentSelected;
	private bool ReportSelected;
	private bool BiometricPhotoSelected;

	// Older orders carry a single un-numbered COE; it shows in the COE 1 slot so
	// they stay downloadable from this dialog.
	private string? Coe1DisplayFileName =>
		!string.IsNullOrWhiteSpace(ReportResult?.Coe1FileName)
			? ReportResult?.Coe1FileName
			: ReportResult?.CoeFileName;

	private int TotalDocuments => 10;

	private int AvailableDocumentCount =>
		(!string.IsNullOrWhiteSpace(ReportResult?.BiometricPhotoFileName) ? 1 : 0)
		+ (!string.IsNullOrWhiteSpace(ReportResult?.ResumeFileName) ? 1 : 0)
		+ (!string.IsNullOrWhiteSpace(ReportResult?.IdUploadedFileName) ? 1 : 0)
		+ (!string.IsNullOrWhiteSpace(ReportResult?.NbiClearanceFileName) ? 1 : 0)
		+ (!string.IsNullOrWhiteSpace(ReportResult?.DiplomaFileName) ? 1 : 0)
		+ (!string.IsNullOrWhiteSpace(Coe1DisplayFileName) ? 1 : 0)
		+ (!string.IsNullOrWhiteSpace(ReportResult?.Coe2FileName) ? 1 : 0)
		+ (!string.IsNullOrWhiteSpace(ReportResult?.Coe3FileName) ? 1 : 0)
		+ (!string.IsNullOrWhiteSpace(ReportResult?.ConsentFormFileName) ? 1 : 0)
		+ (!string.IsNullOrWhiteSpace(ReportResult?.UploadedReportFileName) ? 1 : 0);

	private bool HasSelectedFile =>
		BiometricPhotoSelected || ResumeSelected || GovernmentIdSelected || NbiSelected || DiplomaSelected
		|| Coe1Selected || Coe2Selected || Coe3Selected || ConsentSelected || ReportSelected;

	private int SelectedFileCount =>
		(BiometricPhotoSelected ? 1 : 0)
		+ (ResumeSelected ? 1 : 0)
		+ (GovernmentIdSelected ? 1 : 0)
		+ (NbiSelected ? 1 : 0)
		+ (DiplomaSelected ? 1 : 0)
		+ (Coe1Selected ? 1 : 0)
		+ (Coe2Selected ? 1 : 0)
		+ (Coe3Selected ? 1 : 0)
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

		if (NbiSelected)
			DownloadRequest.DocumentTypes.Add(AtsDocumentTypes.NbiClearance);

		if (DiplomaSelected)
			DownloadRequest.DocumentTypes.Add(AtsDocumentTypes.Diploma);

		if (Coe1Selected)
		{
			// The COE 1 slot shows the legacy un-numbered COE when no Emp1 file
			// exists; ask the server for whichever one is actually on record.
			DownloadRequest.DocumentTypes.Add(
				!string.IsNullOrWhiteSpace(ReportResult?.Coe1FileName)
					? AtsDocumentTypes.Coe1
					: AtsDocumentTypes.Coe);
		}

		if (Coe2Selected)
			DownloadRequest.DocumentTypes.Add(AtsDocumentTypes.Coe2);

		if (Coe3Selected)
			DownloadRequest.DocumentTypes.Add(AtsDocumentTypes.Coe3);

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

	private bool isLoadingPreview;

	// The form's personal details are the first thing an applicant saves, so a
	// filled-form date - or any file from the form - means there are answers to
	// show. An order whose invitation was never answered has nothing to preview.
	private bool CanPreviewForm =>
		!string.IsNullOrWhiteSpace(ReportResult?.FilledFormAt)
		|| !string.IsNullOrWhiteSpace(ReportResult?.ResumeFileName)
		|| !string.IsNullOrWhiteSpace(ReportResult?.ConsentFormFileName);

	private async Task OpenFormPreviewAsync()
	{
		if (isLoadingPreview)
			return;

		isLoadingPreview = true;

		try
		{
			var previewResponse = await ReportService.GetApplicationFormPreviewAsync(EmailInvitationId);

			if (!previewResponse.IsSuccess || previewResponse.Data is null)
			{
				Snackbar.Add(previewResponse.ErrorDetail, Severity.Error);
				return;
			}

			var parameters = new DialogParameters
			{
				{ nameof(ApplicationFormPreviewComponent.Preview), previewResponse.Data },
				{ nameof(ApplicationFormPreviewComponent.EmailInvitationId), EmailInvitationId }
			};

			var options = new DialogOptions
			{
				NoHeader = true,
				MaxWidth = MaxWidth.Medium,
				FullWidth = true
			};

			var dialog = await DialogService.ShowAsync<ApplicationFormPreviewComponent>(
				"Application form preview",
				parameters,
				options);

			await dialog.Result;
		}
		finally
		{
			isLoadingPreview = false;
		}
	}
}
