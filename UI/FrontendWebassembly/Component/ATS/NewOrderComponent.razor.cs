namespace FrontendWebassembly.Component.ATS;

public partial class NewOrderComponent
{
	private MudForm? candidateForm;
	private MudForm? bulkForm;
	private EmailInvitationRequestDTO subject = new() { RushNormal = "Normal" };
	private BulkUploadFileDetailsDTO bulkUploadFileDetailsDTO = new() { OrderType = "Normal" };
	private MudFileUpload<IBrowserFile> bulkFileUpload = default!;
	private bool isSavingCandidate = false;
	private bool isUploadingBulk = false;
	private bool isPreview = false;
	private bool isBulkMode = false;
	private bool isLoadingPackages = true;
	private IReadOnlyList<PackageDetailsDTO> availablePackages = Array.Empty<PackageDetailsDTO>();

	protected override async Task OnInitializedAsync()
	{
		await base.OnInitializedAsync();
		if (!IsPageAuthorized)
			return;

		await LoadAvailablePackagesAsync();

		EndorsementSubmissionService.ATSResponseReceived += OnATSResponse;
		await EndorsementSubmissionService.StartAsync();

	}

	private async Task LoadAvailablePackagesAsync()
	{
		try
		{
			var accessResponse = await ATSUserManagementService.GetMyAtsAccessAsync();

			if (!accessResponse.IsSuccess || accessResponse.Data is null)
			{
				Snackbar.Add(accessResponse.ErrorDetail, Severity.Error);
				return;
			}

			var access = accessResponse.Data;
			var clientId = access.RoleId == 1 ? null : access.ClientId;
			if (access.RoleId != 1 && clientId is not > 0)
			{
				Snackbar.Add("No client is assigned to your user account.", Severity.Warning);
				return;
			}

			var packagesResponse = await PackageManagementService.GetAllPackagesAsync(clientId: clientId);

			if (!packagesResponse.IsSuccess || packagesResponse.Data is null)
			{
				Snackbar.Add(packagesResponse.ErrorDetail, Severity.Error);
				return;
			}

			availablePackages = packagesResponse.Data
				.Where(package => package.IsActive)
				.DistinctBy(package => package.PackageId)
				.OrderBy(package => package.PackageName)
				.ToArray();
		}
		finally
		{
			isLoadingPackages = false;
		}
	}

	private void SetOrderMode(bool bulk)
	{
		isBulkMode = bulk;
	}

	private string GetSegmentClass(bool bulk)
		=> isBulkMode == bulk ? "ats-segment-btn active" : "ats-segment-btn";

	private static string GetSpeedCardClass(string? selectedValue, string cardValue)
		=> selectedValue == cardValue ? "ats-speed-card selected" : "ats-speed-card";

	private void SetCandidateSpeed(string speed)
	{
		subject.RushNormal = speed;
	}

	private string GetCandidateNormalCardClass()
		=> GetSpeedCardClass(subject.RushNormal, "Normal");

	private string GetCandidateRushCardClass()
		=> GetSpeedCardClass(subject.RushNormal, "Rush");

	private string GetBulkNormalCardClass()
		=> GetSpeedCardClass(bulkUploadFileDetailsDTO.OrderType, "Normal");

	private string GetBulkRushCardClass()
		=> GetSpeedCardClass(bulkUploadFileDetailsDTO.OrderType, "Rush");

	private void SelectCandidateNormal() => SetCandidateSpeed("Normal");
	private void SelectCandidateRush() => SetCandidateSpeed("Rush");
	private void SelectBulkNormal() => SetBulkSpeed("Normal");
	private void SelectBulkRush() => SetBulkSpeed("Rush");

	private void SetBulkSpeed(string speed)
	{
		bulkUploadFileDetailsDTO.OrderType = speed;
	}

	private async Task ResetBulkFormAsync()
	{
		bulkUploadFileDetailsDTO.BulkFile = null;
		bulkUploadFileDetailsDTO.FileName = null;
		bulkUploadFileDetailsDTO.OrderType = "Normal";
		bulkUploadFileDetailsDTO.PackageType = null;

		if (bulkForm is not null)
			await bulkForm.ResetAsync();
	}
	private async Task DownloadTemplate()
	{
		var response = await EndorsementSubmissionService.DownloadBulkTemplateAsync();

		if (!response.IsSuccess)
		{
			Snackbar.Add(response.ErrorDetail, Severity.Error);
			return;
		}

		NavigationManager.NavigateTo(response.Data!);
	}

	// The hub event is a plain Action, so the handler has to be void at the delegate
	// boundary. Keep the body in an async Task and observe it here rather than letting
	// an `async void` throw into the SignalR callback, where nothing can catch it.
	private void OnATSResponse(string message)
	{
		_ = OnATSResponseAsync(message);
	}

	private async Task OnATSResponseAsync(string message)
	{
		try
		{
			await InvokeAsync(() =>
			{
				Snackbar.Add(message, Severity.Success);
				StateHasChanged();
			});
		}
		catch (ObjectDisposedException)
		{
			// The component went away between the notification arriving and the render.
			// Nothing to show, and nothing worth logging.
		}
	}

	public void Dispose()
	{
		// EndorsementSubmissionService lives for the lifetime of the app, so without
		// this every visit to this page left another subscription behind - the user saw
		// one notification per visit, and eventually none at all once the disposed
		// components started throwing.
		EndorsementSubmissionService.ATSResponseReceived -= OnATSResponse;
	}

	private async Task OnBulkFileUpload(InputFileChangeEventArgs e)
	{

		var result = FileValidationService.ValidateExtension(e.File.Name, ".csv");

		if (!result.IsValid)
		{
			Snackbar.Add(result.ErrorMessage!, Severity.Error);
			return;
		}

		if (e.File is not null)
		{
			bulkUploadFileDetailsDTO.BulkFile = e.File;
			bulkUploadFileDetailsDTO.FileName = e.File.Name;
		}

		return;
	}

	private async Task OnSubmitCandidate()
	{
		await candidateForm!.ValidateAsync();

		if (!candidateForm.IsValid)
			return;

		if (string.IsNullOrWhiteSpace(subject.RushNormal))
		{
			Snackbar.Add("Processing speed is required", Severity.Error);
			return;
		}

		var confirmParam = new DialogParameters
		{
			{
				nameof(YesNoDialogComponent.Title),
				"Submit Candidate"
			},
			{
				nameof(YesNoDialogComponent.Message),
				"Please be advised that this action will send an email invitation to your candidate."
			},
			{
				nameof(YesNoDialogComponent.ConfirmText),
				"Proceed"
			},
			{
				nameof(YesNoDialogComponent.InformationMessage),
				"Clicking 'Proceed' will  send an email invitation. Would you like to proceed?"
			}
		};

		var options = new DialogOptions
		{
			NoHeader = true,
			MaxWidth = MaxWidth.ExtraSmall,
			FullWidth = true
		};

		var dialog = await DialogService.ShowAsync<YesNoDialogComponent>(null, confirmParam, options);

		var result = await dialog.Result;

		if (result!.Canceled)
			return;

		try
		{
			isSavingCandidate = true;

			await InvokeAsync(StateHasChanged);
			await Task.Yield();

			var sendResponse =
			await EndorsementSubmissionService
				.InsertEmailInvitationRequestAsync(subject);

			if (!sendResponse.IsSuccess)
			{
				Snackbar.Add(sendResponse.ErrorDetail, Severity.Error);
				return;
			}

			if (sendResponse.Data)
			{
				Snackbar.Add("An email invitation will be sent to your candidate.", Severity.Success);

				subject.RushNormal = "Normal";

				await candidateForm.ResetAsync();
			}
		}
		finally
		{
			isSavingCandidate = false;
		}
	}

	private async Task OnSubmitBulk()
	{
		await bulkForm!.ValidateAsync();

		if (!bulkForm.IsValid)
			return;

		if (string.IsNullOrWhiteSpace(bulkUploadFileDetailsDTO.OrderType))
		{
			Snackbar.Add("Processing speed is required", Severity.Error);
			return;
		}

		if (bulkUploadFileDetailsDTO.BulkFile is null)
		{
			Snackbar.Add("File is required", Severity.Error);
			return;
		}

		var previewData = await BuildCsvPreview();

		if (previewData.Rows.Count == 0)
		{
			Snackbar.Add("The CSV file is empty.", Severity.Error);
			return;
		}

		isPreview = true;
		StateHasChanged();

		// Say so when the preview is a sample - a silent cap reads as "this is the whole
		// file", and the operator is approving an import on the strength of it.
		var previewMessage = previewData.IsTruncated
			? $"Showing the first {previewData.Rows.Count} of {previewData.TotalRowCount} rows. All rows will be uploaded."
			: "Upload has been disabled. Blank detail is not allowed.";

		var parameters = new DialogParameters
		{
			{ nameof(PreviewComponent.Headers), previewData.Headers },
			{ nameof(PreviewComponent.Rows), previewData.Rows },
			{ nameof(PreviewComponent.Message), previewMessage }
		};

		var options = new DialogOptions
		{
			MaxWidth = MaxWidth.Large,
			FullWidth = true,
			NoHeader = true
		};

		isPreview = false;

		var dialog = await DialogService.ShowAsync<PreviewComponent>(
			"Preview Upload",
			parameters,
			options);

		var result = await dialog.Result;

		if (result!.Canceled)
			return;

		try
		{
			isUploadingBulk = true;
			StateHasChanged();

			var uploadResponse = await EndorsementSubmissionService
			.InsertBulkSubjectAsync(bulkUploadFileDetailsDTO);

			if (!uploadResponse.IsSuccess)
			{
				Snackbar.Add(uploadResponse.ErrorDetail, Severity.Error);
				return;
			}

			if (uploadResponse.Data)
			{
				Snackbar.Add("Bulk upload successful. An email invitation will be sent to your candidates.", Severity.Success);

				bulkUploadFileDetailsDTO.OrderType = "Normal";
				bulkUploadFileDetailsDTO.BulkFile = null;

				await bulkForm.ResetAsync();

				StateHasChanged();
			}
		}
		finally
		{
			isUploadingBulk = false;

		}
	}

	private async Task<CsvPreviewParser.CsvPreviewResult> BuildCsvPreview()
	{
		// The 25 MB ceiling matches what InsertBulkSubjectAsync uploads. Without an
		// explicit limit this used Blazor's 512 KB default and threw on any larger
		// file - so a 600 KB CSV failed at preview while being perfectly uploadable.
		using var stream = bulkUploadFileDetailsDTO.BulkFile!
			.OpenReadStream(maxAllowedSize: 25 * 1024 * 1024);

		using var reader = new StreamReader(stream);

		var csvContent = await reader.ReadToEndAsync();

		// Quote-aware, so the preview matches what CsvHelper parses server-side.
		return CsvPreviewParser.Parse(csvContent);
	}

	private async Task RemoveFileFromUploadsAsync(IBrowserFile file)
	{
		if (await bulkFileUpload!.RemoveFileAsync(file))
		{
			bulkUploadFileDetailsDTO.BulkFile = null;
			bulkUploadFileDetailsDTO.FileName = null;
		}
	}
}
