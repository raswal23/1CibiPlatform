namespace FrontendWebassembly.Component.ATS;

public partial class DisputeDialogOrderComponent : IDisposable
{
	private const string OtherDisputeCategory = "Others";
	private MudForm? disputeForm;
	private bool isMarkingAsDisputed;
	private bool isUploading;
	private string? selectedDisputeCategory;
	private string otherReason = string.Empty;

	// Blinks the note under "Please specify" when the field is clicked while it is
	// still locked. The token restarts the blink on every click instead of letting
	// an earlier click's delay switch a newer blink off early.
	private bool isSpecifyNoteBlinking;
	private CancellationTokenSource? specifyNoteBlinkCts;

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

			if (string.Equals(value, OtherDisputeCategory, StringComparison.Ordinal))
			{
				// The field just unlocked; a blink telling the user it is locked
				// would now be lying.
				specifyNoteBlinkCts?.Cancel();
				isSpecifyNoteBlinking = false;
			}
			else
			{
				otherReason = string.Empty;
			}
		}
	}

	private bool IsOtherDisputeSelected =>
		string.Equals(SelectedDisputeCategory, OtherDisputeCategory, StringComparison.Ordinal);

	// The wrapper around the disabled field receives the click (a disabled input
	// never raises one itself) and blinks the note for a moment. Re-clicking
	// restarts the animation from the first flash.
	private async Task OnSpecifyFieldClickedAsync()
	{
		if (IsOtherDisputeSelected)
			return;

		specifyNoteBlinkCts?.Cancel();
		specifyNoteBlinkCts?.Dispose();
		specifyNoteBlinkCts = new CancellationTokenSource();

		var token = specifyNoteBlinkCts.Token;

		// Drop the class for one render so a click mid-blink restarts the CSS
		// animation instead of being ignored.
		isSpecifyNoteBlinking = false;
		await InvokeAsync(StateHasChanged);

		isSpecifyNoteBlinking = true;
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

		isSpecifyNoteBlinking = false;
		await InvokeAsync(StateHasChanged);
	}

	public void Dispose()
	{
		specifyNoteBlinkCts?.Cancel();
		specifyNoteBlinkCts?.Dispose();
	}

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

			// DisputeReason keeps the meaning it has always had - it is what gets persisted, and
			// for Billing/Report that has always been the category label rather than free text.
			// DisputeCategory travels alongside it only so the acknowledgement email can show the
			// category and the "Others" free text as two separate lines.
			DisputeReason = IsOtherDisputeSelected
				? otherReason.Trim()
				: SelectedDisputeCategory,
			DisputeCategory = SelectedDisputeCategory
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
					nameof(YesNoDialogComponent.InfoBGColor),"var(--c-warn-bg)"
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

			var response = await DisputeOrderService.MarkAsDisputedAsync(requestToSend);

			if (!response.IsSuccess)
			{
				Snackbar.Add(response.ErrorDetail, Severity.Error);
				return;
			}

			submissionSucceeded = response.Data;

			if (!submissionSucceeded)
			{
				Snackbar.Add("Failed to mark order as disputed.", Severity.Error);
			}
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
