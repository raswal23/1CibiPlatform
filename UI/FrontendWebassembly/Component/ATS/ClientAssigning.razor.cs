namespace FrontendWebassembly.Component.ATS;

public partial class ClientAssigning
{
	private string searchString = string.Empty;
	private TableComponent<ClientAssignmentDetailsDTO>? assignmentsTable;

	private async Task<TableData<ClientAssignmentDetailsDTO>> LoadAssignmentData(
		TableState state,
		CancellationToken cancellationToken) =>
		await LoadPagedDataAsync(state, async (page, pageSize) =>
		{
			var result = await ClientAssignmentService.GetAssignmentsAsync(
				page,
				pageSize,
				searchString,
				cancellationToken);
			return new PaginatedResult<ClientAssignmentDetailsDTO>(
				result.PageIndex,
				result.PageSize,
				result.TotalRecords,
				result.Items);
		});

	private async Task EditAssignment(ClientAssignmentDetailsDTO assignment)
	{
		var parameters = new DialogParameters<EditClientAssignmentComponent>
		{
			{ component => component.CurrentAssignment, assignment }
		};
		var options = new DialogOptions
		{
			NoHeader = true,
			MaxWidth = MaxWidth.Small,
			FullWidth = true,
			BackdropClick = false
		};

		var dialog = await DialogService.ShowAsync<EditClientAssignmentComponent>(
			"Assign Client",
			parameters,
			options);
		var result = await dialog.Result;
		if (result is null || result.Canceled)
			return;

		Snackbar.Add("Client assignment saved successfully", Severity.Success);
		if (assignmentsTable is not null)
			await assignmentsTable.ReloadServerData();
	}

	private static string FormatDate(DateTime? value) =>
		value.HasValue ? value.Value.ToString("MMMM dd, yyyy") : "—";

	private static string GetUserInitials(string? userName)
	{
		var parts = userName?.Split(
			' ',
			StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
		if (parts.Length == 0)
			return "?";
		if (parts.Length == 1)
			return parts[0][..Math.Min(2, parts[0].Length)].ToUpperInvariant();
		return $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant();
	}
}
