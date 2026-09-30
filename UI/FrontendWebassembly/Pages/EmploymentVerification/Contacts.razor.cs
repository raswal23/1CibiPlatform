namespace FrontendWebassembly.Pages.EmploymentVerification;

using FrontendWebassembly.Component.EmploymentVerification;

public partial class Contacts
{
	private readonly CursorTableLoader<EmploymentVerificationContactDTO> _contactsLoader = new();

	private TableComponent<EmploymentVerificationContactDTO>? _contactsTable;
	private string _searchString = string.Empty;

	/// <summary>
	/// The directory is genuinely large - 7,357 seeded rows - so this is the one EV
	/// table that pages on the server rather than filtering a list held in memory.
	/// </summary>
	/// <remarks>
	/// The signature passed to <c>LoadCursorPagedDataAsync</c> must name every filter
	/// that invalidates the keyset walk. Only the search term does today; a status
	/// filter added later has to join it, or the loader keeps walking cursors minted
	/// under the old predicate and silently skips rows.
	/// </remarks>
	private Task<TableData<EmploymentVerificationContactDTO>> LoadContactsAsync(
		TableState state,
		CancellationToken cancellationToken) =>
		LoadCursorPagedDataAsync(
			_contactsLoader,
			state,
			_searchString,
			(cursor, pageSize) => ContactDirectoryService.GetContactsAsync(
				cursor,
				pageSize,
				_searchString,
				cancellationToken));

	private async Task AddContactAsync()
	{
		var options = new DialogOptions
		{
			NoHeader = true,
			MaxWidth = MaxWidth.Small,
			FullWidth = true,
			BackdropClick = false
		};

		var dialog = await DialogService.ShowAsync<AddContactComponent>(
			"Add Contact",
			new DialogParameters<AddContactComponent>(),
			options);

		var result = await dialog.Result;

		if (result is null || result.Canceled)
		{
			return;
		}

		var contact = (AddEmploymentVerificationContactDTO)result.Data!;
		var response = await ContactDirectoryService.AddContactAsync(contact);

		if (!response.IsSuccess)
		{
			// Carries the 409 detail from the duplicate guard, which names the company
			// and mailbox that already exist.
			Snackbar.Add(response.ErrorDetail, Severity.Error);
			return;
		}

		Snackbar.Add("Contact added successfully", Severity.Success);
		await ReloadTableAsync();
	}

	private async Task EditContactAsync(EmploymentVerificationContactDTO contact)
	{
		var parameters = new DialogParameters<EditContactComponent>
		{
			{ component => component.Contact, contact }
		};

		var options = new DialogOptions
		{
			NoHeader = true,
			MaxWidth = MaxWidth.Small,
			FullWidth = true,
			BackdropClick = false
		};

		var dialog = await DialogService.ShowAsync<EditContactComponent>(
			"Edit Contact",
			parameters,
			options);

		var result = await dialog.Result;

		if (result is null || result.Canceled)
		{
			return;
		}

		var edited = (EditEmploymentVerificationContactDTO)result.Data!;
		var response = await ContactDirectoryService.EditContactAsync(edited);

		if (!response.IsSuccess)
		{
			Snackbar.Add(response.ErrorDetail, Severity.Error);
			return;
		}

		Snackbar.Add("Contact updated successfully", Severity.Success);
		await ReloadTableAsync();
	}

	/// <summary>
	/// A write invalidates the cached first page and count server side, so the walk
	/// restarts from page 0 rather than resuming on a cursor minted before the change.
	/// </summary>
	private async Task ReloadTableAsync()
	{
		if (_contactsTable?.TableRef is null)
		{
			return;
		}

		_contactsTable.TableRef.CurrentPage = 0;
		await _contactsTable.TableRef.ReloadServerData();
	}
}
