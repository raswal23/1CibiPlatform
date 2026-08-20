namespace FrontendWebassembly.Component.ATS;

public partial class WithdrawnApplicationComponent
{
	private TableComponent<EmailInvitationRequestListDTO>? lockedUsersTable;
	private string? _searchString;

	private Guid? _loadingEmailInvitationId;

	private static string GetInitials(string? firstName, string? lastName)
	{
		var firstInitial = string.IsNullOrWhiteSpace(firstName) ? string.Empty : firstName.Trim()[0].ToString();
		var lastInitial = string.IsNullOrWhiteSpace(lastName) ? string.Empty : lastName.Trim()[0].ToString();

		return $"{firstInitial}{lastInitial}".ToUpperInvariant();
	}

	private async Task ConfirmResendApplicationForm(Guid emailInvitationId)
	{

		var confirmParam = new DialogParameters
		{
			{
				nameof(YesNoDialogComponent.Title),
				"Resend Application"
			},
			{
				nameof(YesNoDialogComponent.Message),
				"Please be advised that this action will resend the application form."
			},
			{
				nameof(YesNoDialogComponent.ConfirmText),
				"Resend"
			},
			{
				nameof(YesNoDialogComponent.InformationMessage),
				"Clicking \"Resend\" will resend the application form via email."
			},
			{
				nameof(YesNoDialogComponent.ConfirmIcon), Icons.Material.Outlined.Refresh
			},
			{
				nameof(YesNoDialogComponent.ConfirmActionAsync),
				(Func<Task<bool>>)(() => ResendApplicationForm(emailInvitationId))
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
				nameof(YesNoDialogComponent.InfoBGColor),"#FCF1DD"
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
		await dialog.Result;
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

	private string searchString
	{
		get => _searchString!;
		set => UpdateSearch(ref _searchString!, value, lockedUsersTable!);
	}

	private async Task<bool> ResendApplicationForm(Guid emailInvitationId)
	{
		_loadingEmailInvitationId = emailInvitationId;
		await InvokeAsync(StateHasChanged);

		try
		{
			bool success;

			try
			{
				success = await EndorsementSubmissionService.ResendApplicationFormAsync(emailInvitationId);
			}
			catch (Exception)
			{
				Snackbar.Add("Failed to resend application form.", Severity.Error);
				return false;
			}

			if (!success)
			{
				Snackbar.Add("Failed to resend application form.", Severity.Error);
				return false;
			}

			if (lockedUsersTable?.TableRef != null)
			{
				try
				{
					await lockedUsersTable.TableRef.ReloadServerData();
				}
				catch (Exception)
				{
					Snackbar.Add("Application form was resent, but the list could not be refreshed.", Severity.Warning);
				}
			}

			Snackbar.Add("Application form resent successfully.", Severity.Success);
			return true;
		}
		finally
		{
			_loadingEmailInvitationId = null;
			await InvokeAsync(StateHasChanged);
		}
	}
}
