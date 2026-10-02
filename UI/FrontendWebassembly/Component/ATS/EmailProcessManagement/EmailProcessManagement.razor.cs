using FrontendWebassembly.ShareData.ATS;
using FrontendWebassembly.SharedService;

namespace FrontendWebassembly.Component.ATS;

/// <summary>
/// The copy list each ATS notice sends to - who is Cc'd on a withdrawal, a dispute, an application
/// form, a follow-up or a submitted form.
/// </summary>
/// <remarks>
/// The dialogs close with a DTO and this page does the posting, which is the same split the sender
/// -account board beside it uses: one place owns the service call, the snackbar and the reload, so
/// the dialogs stay forms.
///
/// See docs/features/ats-email-process/ats-email-process.md.
/// </remarks>
public partial class EmailProcessManagement
{
	private List<EmailProcessDetailsDTO> _emailProcesses = [];
	private string _searchString = string.Empty;
	private bool _isLoading;
	private bool _isBusy;

	// Filtered in the browser rather than on the server. The list is one row per notice - five
	// today - so a round trip per keystroke would buy nothing and the rows would flicker as each
	// response landed.
	private IEnumerable<EmailProcessDetailsDTO> FilteredEmailProcesses =>
		string.IsNullOrWhiteSpace(_searchString)
			? _emailProcesses
			: _emailProcesses.Where(row =>
				row.EmailProcess.Contains(_searchString, StringComparison.OrdinalIgnoreCase)
				|| AtsEmailProcesses.Label(row.EmailProcess).Contains(_searchString, StringComparison.OrdinalIgnoreCase)
				|| row.CCEmail.Contains(_searchString, StringComparison.OrdinalIgnoreCase));

	private string EmptyTitle => _emailProcesses.Count == 0
		? "No copy lists"
		: "No matching copy lists";

	private string EmptyMessage => _emailProcesses.Count == 0
		? "Register the notice a team should be copied on."
		: "Nothing here matches that search.";

	private static readonly DialogOptions FormDialogOptions = new()
	{
		NoHeader = true,
		MaxWidth = MaxWidth.Small,
		FullWidth = true,
		BackdropClick = false
	};

	protected override async Task OnInitializedAsync()
	{
		await base.OnInitializedAsync();

		// This component carries no Require* attributes of its own - the Email Management host
		// gates the tab - so the flag is true whenever the host rendered it. Kept rather than
		// deleted because SecurePageBase only populates AccessibleATSModuleIds inside this call,
		// and dropping the await would leave that set empty for anything that later reads it.
		if (!IsPageAuthorized)
		{
			return;
		}

		await LoadEmailProcessesAsync();
	}

	private async Task LoadEmailProcessesAsync()
	{
		if (_isLoading)
		{
			return;
		}

		_isLoading = true;

		try
		{
			var response = await EmailProcessService.GetEmailProcessesAsync();

			if (!response.IsSuccess || response.Data is null)
			{
				Snackbar.Add(response.ErrorDetail, Severity.Error);
				return;
			}

			_emailProcesses = response.Data;
		}
		finally
		{
			_isLoading = false;
		}
	}

	private void OnSearchChanged(string value) => _searchString = value;

	/// <summary>
	/// The addresses in a row's copy list, using the same splitter the chip input and the server
	/// both use - so the table cannot show an address the notice would not actually send to.
	/// </summary>
	private static IReadOnlyList<string> CopiedMailboxes(EmailProcessDetailsDTO row) =>
		EmailCopyListDraft.Split(row.CCEmail);

	/// <summary>
	/// The addresses behind a "+N more" chip, so the ones the cell hides are still readable without
	/// opening the dialog.
	/// </summary>
	/// <remarks>
	/// A method rather than an inline <c>string.Join(", ", …)</c> in the <c>title</c> attribute:
	/// Razor reads the attribute value up to the next double quote, so a comma-and-space separator
	/// written as a literal there closes the attribute early and the generated lambda does not
	/// compile.
	/// </remarks>
	private static string OverflowTooltip(IReadOnlyList<string> mailboxes) =>
		string.Join(", ", mailboxes.Skip(3));

	// ---------- Add ----------

	private async Task AddEmailProcessAsync()
	{
		var dialog = await DialogService.ShowAsync<AddEmailProcessComponent>(
			"Add email process", FormDialogOptions);

		var result = await dialog.Result;

		if (result is null || result.Canceled || result.Data is not AddEmailProcessDTO added)
		{
			return;
		}

		_isBusy = true;

		try
		{
			var response = await EmailProcessService.AddEmailProcessAsync(added);

			if (!response.IsSuccess || response.Data is null)
			{
				// Carries the server's own reason. The common one is "a copy list for this notice
				// already exists", because every environment is seeded with a row per notice - so
				// this is the message an operator sees when they add one that is already listed,
				// and it is deliberately the server's wording rather than a guess made here.
				Snackbar.Add(response.ErrorDetail, Severity.Error);
				return;
			}

			Snackbar.Add(
				$"Copy list for {AtsEmailProcesses.Label(response.Data.EmailProcess)} registered.",
				Severity.Success);

			await LoadEmailProcessesAsync();
		}
		finally
		{
			_isBusy = false;
		}
	}

	// ---------- Edit ----------

	private async Task EditEmailProcessAsync(EmailProcessDetailsDTO row)
	{
		var parameters = new DialogParameters<EditEmailProcessComponent>
		{
			{ component => component.Process, row }
		};

		var dialog = await DialogService.ShowAsync<EditEmailProcessComponent>(
			"Edit email process", parameters, FormDialogOptions);

		var result = await dialog.Result;

		if (result is null || result.Canceled || result.Data is not EditEmailProcessDTO edit)
		{
			return;
		}

		_isBusy = true;

		try
		{
			var response = await EmailProcessService.EditEmailProcessAsync(edit);

			if (!response.IsSuccess || response.Data is null)
			{
				Snackbar.Add(response.ErrorDetail, Severity.Error);
				return;
			}

			// Named from the response rather than from the row, so a rename says where the list
			// moved to instead of where it came from.
			Snackbar.Add(
				$"Copy list for {AtsEmailProcesses.Label(response.Data.EmailProcess)} saved.",
				Severity.Success);

			await LoadEmailProcessesAsync();
		}
		finally
		{
			_isBusy = false;
		}
	}
}
