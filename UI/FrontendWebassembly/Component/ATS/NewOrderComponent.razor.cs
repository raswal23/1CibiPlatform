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
			var access = await ATSUserManagementService.GetMyAtsAccessAsync();
			var clientId = access.RoleId == 1 ? null : access.ClientId;
			if (access.RoleId != 1 && clientId is not > 0)
			{
				Snackbar.Add("No client is assigned to your user account.", Severity.Warning);
				return;
			}

			var packages = await PackageManagementService.GetAllPackagesAsync(clientId: clientId);

			availablePackages = packages
				.Where(package => package.IsActive)
				.DistinctBy(package => package.PackageId)
				.OrderBy(package => package.PackageName)
				.ToArray();
		}
		catch (Exception)
		{
			Snackbar.Add("Unable to load the packages assigned to your client.", Severity.Error);
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
		var url = await EndorsementSubmissionService.DownloadBulkTemplateAsync();

		NavigationManager.NavigateTo(url!);
	}

	private async void OnATSResponse(string message)
	{
		await InvokeAsync(() =>
		{
			Snackbar.Add(message, Severity.Success);
			StateHasChanged();
		});
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
			Snackbar.Add("Processing speed is required",Severity.Error);
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

			var isSent =
			await EndorsementSubmissionService
				.InsertEmailInvitationRequestAsync(subject);

			if (isSent)
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

		var hasData = previewData.Rows.Any(row => 
					row.Any(cell => !string.IsNullOrWhiteSpace(cell)));

		if (!hasData)
		{
			Snackbar.Add("The CSV file is empty.", Severity.Error);
			return;
		}

		isPreview = true;
		StateHasChanged();

		var parameters = new DialogParameters
		{
			{ nameof(PreviewComponent.Headers), previewData.Headers },
			{ nameof(PreviewComponent.Rows), previewData.Rows },
			{ nameof(PreviewComponent.Message), "Upload has been disabled. Blank detail is not allowed." }
		};

		var options = new DialogOptions
		{
			MaxWidth = MaxWidth.Large,
			FullWidth = true,
			CloseButton = true
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

			var isSent = await EndorsementSubmissionService
			.InsertBulkSubjectAsync(bulkUploadFileDetailsDTO);

			if (isSent)
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

	public class CSVPreviewData
	{
		public List<string> Headers { get; set; } = [];
		public List<List<string>> Rows { get; set; } = [];
	}

	private async Task<CSVPreviewData> BuildCsvPreview()
	{
		var result = new CSVPreviewData();

		using var stream = bulkUploadFileDetailsDTO.BulkFile!.OpenReadStream();

		using var reader = new StreamReader(stream);

		var csvContent = await reader.ReadToEndAsync();

		var lines = csvContent
			.Split(new[] { "\r\n", "\n" },
				StringSplitOptions.RemoveEmptyEntries);

		if (lines.Length == 0)
			return result;

		result.Headers = lines[0]
			.Split(',')
			.Select(x => x.Trim())
			.ToList();

		foreach (var line in lines.Skip(1))
		{
			result.Rows.Add(
				line.Split(',')
					.Select(x => x.Trim())
					.ToList());
		}

		return result;
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
