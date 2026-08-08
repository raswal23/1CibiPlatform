namespace FrontendWebassembly.Component.ATS;

public partial class DisputeDialogOrderComponent
{
	private MudForm? disputeForm;
	private bool isMarkingAsDisputed = false;

	private bool isUploading = false;

	[Inject]
	private IDialogService DialogService { get; set; } = default!;
	[Inject]
	private ISnackbar Snackbar { get; set; } = default!;
	[Inject]
	private IDisputeOrderService DisputeOrderService { get; set; } = default!;

	[CascadingParameter]
	IMudDialogInstance? SubmitDisputeOrderDialog { get; set; }
	[Parameter]
	public Guid EmailInvitationId { get; set; }

	[Parameter]
	public DateTime? OrderCreatedAt { get; set; }

	[Parameter]
	public string? SubjectName { get; set; }
	private DisputeOrderRequestDTO disputeRequest = new();
	private string? otherReason = string.Empty;

	void Cancel() => SubmitDisputeOrderDialog!.Cancel();

	async Task SendDisputeAsync()
	{
		await disputeForm!.ValidateAsync();

		if (!disputeForm.IsValid)
			return;

		try
		{
			isUploading = true;
			isMarkingAsDisputed = true;
			await InvokeAsync(StateHasChanged);

			if (disputeRequest.DisputeReason == "Others")
			{
				disputeRequest.DisputeReason = otherReason;
			}

			disputeRequest.EmailInvitationId = EmailInvitationId;
			disputeRequest.OrderCreatedAt = OrderCreatedAt;
			disputeRequest.SubjectName = SubjectName;

			var confirmParam = new DialogParameters
			{
				{
					nameof(YesNoDialogComponent.Title),
					"Dispute Application"
				},
				{
					nameof(YesNoDialogComponent.Message),
					"Please be advised that this action will dispute the candidate application."
				},
				{
					nameof(YesNoDialogComponent.ConfirmText),
					"Dispute"
				},
				{
					nameof(YesNoDialogComponent.InformationMessage),
					"Clicking 'Dispute' will mark the application as disputed."
				},
				{
					nameof(YesNoDialogComponent.AvatarIcon),Icons.Material.Filled.WarningAmber
				},
				{
					nameof(YesNoDialogComponent.AvatarColor),Color.Warning
				},
				{
					nameof(YesNoDialogComponent.InfoColor),Color.Warning
				},
				{
					nameof(YesNoDialogComponent.InfoBGColor),"#FFF8E1"
				},
				{
					nameof(YesNoDialogComponent.ThemeButtonColor),"theme-button-warning"
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
			{
				isUploading = false;
				return;
			}
				

			var success = await DisputeOrderService.MarkAsDisputedAsync(disputeRequest);

			if (!success)
			{
				Snackbar.Add("Failed to mark order as disputed.", Severity.Error);
				isUploading = false;
				return;
			}
		}
		catch (Exception)
		{
			Snackbar.Add("Failed to dispute order as disputed.", Severity.Error);
		}
		finally
		{
			isMarkingAsDisputed = false;
			await InvokeAsync(StateHasChanged);
		}

		SubmitDisputeOrderDialog!.Close();
		
	}

	private void CloseDialog()
	{
		SubmitDisputeOrderDialog!.Cancel();
	}
}
