namespace FrontendWebassembly.Component.ATS;

public partial class NewOrderComponent
{
	private MudForm? candidateForm;
	private MudForm? bulkForm;
	private EmailInvitationRequestDTO subject = new();
	private BulkUploadFileDetailsDTO bulkUploadFileDetailsDTO = new();
	private MudFileUpload<IBrowserFile> bulkFileUpload = default!;
	private bool isSavingCandidate = false;
	private bool isUploadingBulk = false;

	private TableComponent<EmailInvitationRequestListDTO>? lockedUsersTable;
	private string? _searchString;
	private bool isResending = false;
	protected override async Task OnInitializedAsync()
	{
		
		EndorsementSubmissionService.ATSResponseReceived += OnATSResponse;
		await EndorsementSubmissionService.StartAsync();

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

		var confirmParam = new DialogParameters
		{
			{ nameof(ConfirmationDialogComponent.Message),
			  "Do you want to save the candidate's information?" }
		};

		var dialog = await DialogService.ShowAsync<ConfirmationDialogComponent>(
			"Confirmation",
			confirmParam);

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
				var successParam = new DialogParameters
				{
					{
						nameof(SuccessSaveComponent.Message),
						"Successfully saved the candidate's information."
					}
				};

				await DialogService.ShowAsync<SuccessSaveComponent>(
					"Success",
					successParam);

					subject.RushNormal = null;

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

		var previewData = await BuildCsvPreview();

		var hasData = previewData.Rows.Any(row => 
					row.Any(cell => !string.IsNullOrWhiteSpace(cell)));

		if (!hasData)
		{
			await DialogService.ShowMessageBoxAsync(
				"Empty Excel File",
				"The Excel file is empty.");

			return;
		}

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
			await InvokeAsync(StateHasChanged);

			await Task.Yield();

			var isSent = await EndorsementSubmissionService
			.InsertBulkSubjectAsync(bulkUploadFileDetailsDTO);

			if (isSent)
			{
				var successParams = new DialogParameters
			{
				{
					nameof(SuccessSaveComponent.Message),
					"Successfully uploaded the bulk candidates' information."
				}
			};

				await DialogService.ShowAsync<SuccessSaveComponent>(
					"Success",
					successParams);
				
				bulkUploadFileDetailsDTO.OrderType = null;

				await bulkForm.ResetAsync();
			}
		}
		finally
		{
			isUploadingBulk = false;

		}

	}

	public class ExcelPreviewData
	{
		public List<string> Headers { get; set; } = [];
		public List<List<string>> Rows { get; set; } = [];
	}

	private async Task<ExcelPreviewData> BuildCsvPreview()
	{
		var result = new ExcelPreviewData();

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
		if (await bulkFileUpload.RemoveFileAsync(file))
		{
			bulkUploadFileDetailsDTO.BulkFile = null;
			bulkUploadFileDetailsDTO.FileName = null;
			return;
		}
	}

	private string searchString
	{
		get => _searchString!;
		set => UpdateSearch(ref _searchString!, value, lockedUsersTable!);
	}

	private async Task<TableData<EmailInvitationRequestListDTO>> LoadWithdrawnServerData(TableState state, CancellationToken cancellationToken)
		=> await LoadPagedDataAsync(state, (page, pageSize) =>
			EndorsementSubmissionService.GetWithdrawnEmailInvitationRequestsAsync(page, pageSize, searchString));

	private void UpdateSearch<T>(ref string field, string value, TableComponent<T> table) where T : class
	{
		if (field != value)
		{
			field = value;
			table?.TableRef!.ReloadServerData();
		}
	}

	private async Task ConfirmResendApplicationForm(Guid emailInvitationId)
	{
		var confirmParam = new DialogParameters
		{
			{ nameof(ConfirmationDialogComponent.Message),
			  "Do you want to resend the application form?" }
		};

		var dialog = await DialogService.ShowAsync<ConfirmationDialogComponent>(
			"Confirmation",
			confirmParam);

		var result = await dialog.Result;

		if (result!.Canceled)
			return;

		await ResendApplicationForm(emailInvitationId);
	}

	private async Task ResendApplicationForm(Guid emailInvitationId)
	{
		try
		{
			isResending = true;
			await InvokeAsync(StateHasChanged);

			var success = await EndorsementSubmissionService.ResendApplicationFormAsync(emailInvitationId);

			if (!success)
			{
				Snackbar.Add("Failed to resend application form.", Severity.Error);
				return;
			}

			if (lockedUsersTable?.TableRef != null)
			{
				await lockedUsersTable.TableRef.ReloadServerData();

				await InvokeAsync(StateHasChanged);
				await Task.Yield();
			}

			Snackbar.Add("Application form resent successfully.", Severity.Success);
		}
		finally
		{
			isResending = false;
			await InvokeAsync(StateHasChanged);
		}
	}
}
