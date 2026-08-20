namespace FrontendWebassembly.Component.ATS;

public partial class DisputeDialogOrderComponent
{
	private const string OtherDisputeCategory = "Others";
	private MudForm? disputeForm;
	private bool isMarkingAsDisputed;
	private bool isUploading;
	private string? selectedDisputeCategory;
	private string otherReason = string.Empty;

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

	private string? SelectedDisputeCategory
	{
		get => selectedDisputeCategory;
		set
		{
			if (string.Equals(selectedDisputeCategory, value, StringComparison.Ordinal))
				return;

			selectedDisputeCategory = value;

			if (!string.Equals(value, OtherDisputeCategory, StringComparison.Ordinal))
				otherReason = string.Empty;
		}
	}

	private bool IsOtherDisputeSelected =>
		string.Equals(SelectedDisputeCategory, OtherDisputeCategory, StringComparison.Ordinal);

	void Cancel() => SubmitDisputeOrderDialog!.Cancel();

	async Task SendDisputeAsync()
	{
		await disputeForm!.ValidateAsync();

		if (!disputeForm.IsValid)
			return;

		if (IsOtherDisputeSelected && string.IsNullOrWhiteSpace(otherReason))
		{
			otherReason = string.Empty;
			await disputeForm.ValidateAsync();
			return;
		}

		var requestToSend = new DisputeOrderRequestDTO
		{
			EmailInvitationId = EmailInvitationId,
			DisputeReason = IsOtherDisputeSelected
				? otherReason.Trim()
				: SelectedDisputeCategory
		};
		var submissionSucceeded = false;

		try
		{
			isUploading = true;
			await InvokeAsync(StateHasChanged);

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

			if (result is null || result.Canceled)
				return;

			isMarkingAsDisputed = true;
			await InvokeAsync(StateHasChanged);

			submissionSucceeded = await DisputeOrderService.MarkAsDisputedAsync(requestToSend);

			if (!submissionSucceeded)
			{
				Snackbar.Add("Failed to mark order as disputed.", Severity.Error);
			}
		}
		catch (Exception ex)
		{
			Snackbar.Add(ex.Message, Severity.Error);
		}
		finally
		{
			isUploading = false;
			isMarkingAsDisputed = false;
			await InvokeAsync(StateHasChanged);
		}

		if (submissionSucceeded)
			SubmitDisputeOrderDialog!.Close();
	}

	private void CloseDialog()
	{
		SubmitDisputeOrderDialog!.Cancel();
	}
}
