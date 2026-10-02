namespace FrontendWebassembly.Component.ATS;

public partial class DisputeDialogOrderComponent : IDisposable
{
	private MudForm? disputeForm;
	private bool isMarkingAsDisputed;
	private bool isUploading;
	private string? selectedDisputeCategory;
	private string specifyReason = string.Empty;

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

			// The field unlocks on the FIRST selection and stays unlocked - all three
			// categories need their own description - so a blink telling the user it is
			// locked would now be lying. What they already typed is kept: switching
			// Billing to Report does not make the sentence they wrote wrong.
			specifyNoteBlinkCts?.Cancel();
			isSpecifyNoteBlinking = false;
		}
	}

	private bool IsCategorySelected => !string.IsNullOrWhiteSpace(SelectedDisputeCategory);

	// The wrapper around the disabled field receives the click (a disabled input
	// never raises one itself) and blinks the note for a moment. Re-clicking
	// restarts the animation from the first flash.
	private async Task OnSpecifyFieldClickedAsync()
	{
		if (IsCategorySelected)
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

		// Belt and braces around MudForm: while no category is selected the field is disabled, and a
		// disabled control is not guaranteed to carry its Required rule into the form's verdict. The
		// radio group's own Required rule fails first in that case, so this only ever fires for a
		// selected category with an empty description - collapsing whitespace to empty so the
		// second pass renders "Please specify a reason" rather than silently accepting spaces.
		if (string.IsNullOrWhiteSpace(specifyReason))
		{
			specifyReason = string.Empty;
			await disputeForm.ValidateAsync();
			return;
		}

		var requestToSend = new DisputeOrderRequestDTO
		{
			EmailInvitationId = EmailInvitationId,

			// Every category now carries its own free text, so the two fields no longer collapse:
			// DisputeCategory is always the label and DisputeReason is always what the filer typed.
			// The label is what gets persisted and shown in the dispute list - see
			// ATSRepository.MarkAsDisputedAsync, which writes DisputeCategory into the column of the
			// same name - and the reason is what the acknowledgement email renders as its details
			// line.
			DisputeReason = specifyReason.Trim(),
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
