namespace FrontendWebassembly.Component.ATS;

public partial class WithdrawnApplicationTableComponent
{
	private TableComponent<EmailInvitationRequestListDTO>? lockedUsersTable;
	private string? _searchString;
	private string searchString
	{
		get => _searchString!;
		set => UpdateSearch(ref _searchString!, value, lockedUsersTable!);
	}

	private bool isResending = false;

	private async Task<TableData<EmailInvitationRequestListDTO>> LoadWithdrawnServerData(TableState state, CancellationToken cancellationToken)
		=> await LoadPagedDataAsync(state, (page, pageSize) =>
			EndorsementService.GetWithdrawnEmailInvitationRequestsAsync(page, pageSize, searchString));

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

			var success = await EndorsementService.ResendApplicationFormAsync(emailInvitationId);

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
