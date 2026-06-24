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
		bulkUploadFileDetailsDTO.BulkFile = e.File;
		bulkUploadFileDetailsDTO.FileName = e.File.Name;

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
}
