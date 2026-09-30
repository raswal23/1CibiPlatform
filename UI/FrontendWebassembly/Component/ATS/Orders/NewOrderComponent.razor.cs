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
	[Inject] private CheckBulkFileName CheckBulkFileName { get; set; } = default!;
	private IReadOnlyList<PackageDetailsDTO> availablePackages = Array.Empty<PackageDetailsDTO>();

	private bool IsDataScreening => subject.AutoChasing == false;

	// Unclassified (null AutoChasing) packages match neither type on purpose:
	// "not set" must never pass for Data (or Manual).
	private IReadOnlyList<PackageDetailsDTO> FilteredPackages =>
		subject.AutoChasing is null
			? Array.Empty<PackageDetailsDTO>()
			: availablePackages.Where(package => package.AutoChasing == subject.AutoChasing).ToArray();

	private IReadOnlyList<PackageDetailsDTO> FilteredBulkPackages =>
		bulkUploadFileDetailsDTO.AutoChasing is null
			? Array.Empty<PackageDetailsDTO>()
			: availablePackages.Where(package => package.AutoChasing == bulkUploadFileDetailsDTO.AutoChasing).ToArray();

	private void OnBulkScreeningTypeChanged(bool? screeningType)
	{
		bulkUploadFileDetailsDTO.AutoChasing = screeningType;

		if (screeningType is not null)
		{
			// The field just unlocked; a blink telling the user it is locked
			// would now be lying.
			screeningNoteBlinkCts?.Cancel();
			isScreeningNoteBlinking = false;
		}

		// The previously chosen package may not belong to the new type.
		if (bulkUploadFileDetailsDTO.PackageType is not null
			&& !FilteredBulkPackages.Any(package => package.PackageName == bulkUploadFileDetailsDTO.PackageType))
		{
			bulkUploadFileDetailsDTO.PackageType = null;
		}
	}

	// MudDatePicker works in DateTime?; the DTO stores DateOnly?.
	private DateTime? CandidateDateOfBirth
	{
		get => subject.DateOfBirth?.ToDateTime(TimeOnly.MinValue);
		set => subject.DateOfBirth = value is { } date ? DateOnly.FromDateTime(date) : null;
	}

	// Both tabs show the same note in the same three states; only the copy differs,
	// because what the choice changes is per-candidate fields on one tab and CSV
	// columns on the other.
	private static string ScreeningNoteIconFor(bool? screeningType) => screeningType switch
	{
		true => Icons.Material.Outlined.MarkEmailRead,
		false => Icons.Material.Outlined.Storage,
		null => Icons.Material.Outlined.Info
	};

	private static string ScreeningNoteTitleFor(bool? screeningType) => screeningType switch
	{
		true => "Manual screening",
		false => "Data screening",
		null => "Screening type"
	};

	private string ScreeningNoteIcon => ScreeningNoteIconFor(subject.AutoChasing);

	private string ScreeningNoteTitle => ScreeningNoteTitleFor(subject.AutoChasing);

	private string ScreeningNoteText => subject.AutoChasing switch
	{
		true => "An application form invitation will be emailed to the candidate to fill out their details.",
		false => "No application form is sent to the candidate — only the Date of birth, SSS number and TIN number in Personal information are required.",
		null => "Select a screening type first: Manual sends an application form to the candidate, while Data does not and instead requires the Date of birth, SSS number and TIN number fields."
	};

	private string BulkScreeningNoteIcon => ScreeningNoteIconFor(bulkUploadFileDetailsDTO.AutoChasing);

	private string BulkScreeningNoteTitle => ScreeningNoteTitleFor(bulkUploadFileDetailsDTO.AutoChasing);

	// Named after the CSV headers rather than the form labels: this is what the
	// operator has to type into a spreadsheet, and the upload is rejected on the
	// header text itself.
	private string BulkScreeningNoteText => bulkUploadFileDetailsDTO.AutoChasing switch
	{
		true => "An application form invitation will be emailed to every candidate in the file to fill out their details.",
		false => "No application form is sent to the candidates — every CSV row must include the DateOfBirth (MM/dd/yyyy), SSSNumber and TINNumber columns.",
		null => "Select a screening type first: Manual emails an application form to every candidate in the file, while Data does not and instead requires DateOfBirth, SSSNumber and TINNumber columns in the CSV."
	};

	// Blinks the screening note when the package select is clicked while it is
	// still locked. The token restarts the blink on every click instead of letting
	// an earlier click's delay switch a newer blink off early.
	private bool isScreeningNoteBlinking;
	private CancellationTokenSource? screeningNoteBlinkCts;

	private void OnScreeningTypeChanged(bool? screeningType)
	{
		subject.AutoChasing = screeningType;

		if (screeningType is not null)
		{
			// The field just unlocked; a blink telling the user it is locked
			// would now be lying.
			screeningNoteBlinkCts?.Cancel();
			isScreeningNoteBlinking = false;
		}

		// The previously chosen package may not belong to the new type.
		if (subject.SelectPackage is not null
			&& !FilteredPackages.Any(package => package.PackageName == subject.SelectPackage))
		{
			subject.SelectPackage = null;
		}
	}

	// The wrapper around the disabled select receives the click (a disabled input
	// never raises one itself) and blinks the note for a moment. Re-clicking
	// restarts the animation from the first flash. Shared by both tabs - only one
	// is rendered at a time, so one blink flag is enough.
	private async Task OnPackageFieldClickedAsync()
	{
		var screeningType = isBulkMode
			? bulkUploadFileDetailsDTO.AutoChasing
			: subject.AutoChasing;

		if (screeningType is not null)
			return;

		screeningNoteBlinkCts?.Cancel();
		screeningNoteBlinkCts?.Dispose();
		screeningNoteBlinkCts = new CancellationTokenSource();

		var token = screeningNoteBlinkCts.Token;

		// Drop the class for one render so a click mid-blink restarts the CSS
		// animation instead of being ignored.
		isScreeningNoteBlinking = false;
		await InvokeAsync(StateHasChanged);

		isScreeningNoteBlinking = true;
		await InvokeAsync(StateHasChanged);

		try
		{
			// Matches the CSS: 3 blinks x 0.5s.
			await Task.Delay(1500, token);
		}
		catch (TaskCanceledException)
		{
			return;
		}

		isScreeningNoteBlinking = false;
		await InvokeAsync(StateHasChanged);
	}

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

		// The blink belongs to the tab it started on; carrying it across would flash
		// the other tab's note for no reason the user can connect to a click.
		screeningNoteBlinkCts?.Cancel();
		isScreeningNoteBlinking = false;
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
		bulkUploadFileDetailsDTO.AutoChasing = null;

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

		screeningNoteBlinkCts?.Cancel();
		screeningNoteBlinkCts?.Dispose();
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

		// Data screening sends no application form, so the confirmation copy
		// must not promise an email the candidate will never receive.
		var confirmParam = new DialogParameters
		{
			{
				nameof(YesNoDialogComponent.Title),
				"Submit Candidate"
			},
			{
				nameof(YesNoDialogComponent.Message),
				"Depending on the selected screening type, an email invitation may be sent to the candidate to complete the required information."
			},
			{
				nameof(YesNoDialogComponent.ConfirmText),
				"Proceed"
			},
			{
				nameof(YesNoDialogComponent.InformationMessage),
				"By clicking 'Proceed', you attest and confirm that you have obtained the necessary and valid consent from the concerned individual(s) authorizing CIBI Information, Inc. to collect, process, verify, and validate their personal information for the purpose of conducting the requested background verification. You further confirm that the individual(s) have been appropriately informed of the nature and purpose of the background verification and that such consent was obtained prior to submitting this request."
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
				Snackbar.Add(
					IsDataScreening
						? "The data screening order has been created."
						: "An email invitation will be sent to your candidate.",
					Severity.Success);

				subject = new EmailInvitationRequestDTO { RushNormal = "Normal" };

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

		// The screening type decides which columns the file must carry, so it is checked
		// before the preview rather than alongside the rest of the form.
		if (bulkUploadFileDetailsDTO.AutoChasing is null)
		{
			Snackbar.Add("Screening type is required", Severity.Error);
			return;
		}

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

		var fileNameError = await CheckBulkFileName.ValidateAsync(bulkUploadFileDetailsDTO.FileName);
		if (fileNameError is not null)
		{
			Snackbar.Add(fileNameError, Severity.Error);
			return;
		}

		var previewData = await BuildCsvPreview();

		// Check for required headers based on screening type
		var expectedHeaders = CsvPreviewParser.HeadersFor(bulkUploadFileDetailsDTO.AutoChasing == false);
		var missingHeaders = expectedHeaders
			.Where(expected => !previewData.Headers.Contains(expected, StringComparer.OrdinalIgnoreCase))
			.ToList();

		// The template columns themselves must lead the file in the standard sequence.
		// Name the columns actually absent when that is the failure, otherwise call
		// out the ordering - both before upload, not after the file is accepted.
		var hasCanonicalHeaderSequence = previewData.Headers.Count >= expectedHeaders.Count
			&& expectedHeaders
				.Select((expected, index) =>
					string.Equals(previewData.Headers[index], expected, StringComparison.OrdinalIgnoreCase))
				.All(matches => matches);

		if (!hasCanonicalHeaderSequence)
		{
			Snackbar.Add(
				missingHeaders.Count > 0
					? $"Missing required column(s): {string.Join(", ", missingHeaders)}. Please use the bulk upload template."
					: $"Columns must appear in the template order: {string.Join(", ", expectedHeaders)}.",
				Severity.Error);
			return;
		}

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
			: "Upload has been disabled. Blank details are not allowed (Middle Initial is optional).";

		var parameters = new DialogParameters
		{
			{ nameof(PreviewComponent.Headers), previewData.Headers },
			{ nameof(PreviewComponent.Rows), previewData.Rows },
			{ nameof(PreviewComponent.Message), previewMessage },
			{ nameof(PreviewComponent.IsDataScreening), bulkUploadFileDetailsDTO.AutoChasing == false }
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

		// Excel ANSI exports are Windows-1252 with no BOM; a plain StreamReader decoded
		// Ñ/ñ to U+FFFD in the preview. IBrowserFile streams are single-read, so the
		// decoder buffers the bytes before choosing an encoding.
		var csvContent = await CsvTextDecoder.DecodeAsync(stream);

		// For preview purposes, show ALL columns in the CSV regardless of screening type
		// The actual validation still happens based on screening type in the backend
		return CsvPreviewParser.ParseForPreview(csvContent);
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
