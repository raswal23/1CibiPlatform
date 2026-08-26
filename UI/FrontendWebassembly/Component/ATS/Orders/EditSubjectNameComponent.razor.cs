namespace FrontendWebassembly.Component.ATS;

public partial class EditSubjectNameComponent
{
	private MudForm? SubjectNameForm;

	[CascadingParameter]
	private IMudDialogInstance? SubjectNameDialog { get; set; }

	[Inject]
	private IReportService ReportService { get; set; } = default!;

	[Inject]
	private ISnackbar Snackbar { get; set; } = default!;

	[Parameter]
	public ReportListDTO Report { get; set; } = new();

	private EditSubjectNameDTO EditSubject { get; set; } = new();

	private bool IsSubmitting { get; set; }

	private string PreviewName => BuildSubjectName(
		EditSubject.FirstName,
		EditSubject.MiddleInitial,
		EditSubject.LastName) is { Length: > 0 } name
		? name
		: "Subject name";

	private string Initials
	{
		get
		{
			var parts = new[] { EditSubject.FirstName, EditSubject.LastName }
				.Where(part => !string.IsNullOrWhiteSpace(part))
				.Select(part => char.ToUpperInvariant(part!.Trim()[0]));

			var value = string.Concat(parts);

			return string.IsNullOrEmpty(value) ? "?" : value;
		}
	}

	// Save stays disabled until something actually differs, so a no-op dialog
	// cannot fire a needless request.
	private bool HasChanges =>
		!string.Equals(Normalize(EditSubject.FirstName), Normalize(Report.FirstName), StringComparison.Ordinal) ||
		!string.Equals(Normalize(EditSubject.MiddleInitial), Normalize(Report.MiddleInitial), StringComparison.Ordinal) ||
		!string.Equals(Normalize(EditSubject.LastName), Normalize(Report.LastName), StringComparison.Ordinal);

	protected override void OnParametersSet()
	{
		EditSubject = new EditSubjectNameDTO
		{
			EmailInvitationRequestId = Report.EmailInvitationRequestId,
			FirstName = Report.FirstName,
			MiddleInitial = Report.MiddleInitial,
			LastName = Report.LastName
		};
	}

	private void Cancel() => SubjectNameDialog!.Cancel();

	private async Task Submit()
	{
		if (IsSubmitting)
			return;

		await SubjectNameForm!.ValidateAsync();

		if (!SubjectNameForm!.IsValid)
			return;

		IsSubmitting = true;

		try
		{
			var payload = new EditSubjectNameDTO
			{
				EmailInvitationRequestId = Report.EmailInvitationRequestId,
				FirstName = Normalize(EditSubject.FirstName),
				MiddleInitial = Normalize(EditSubject.MiddleInitial),
				LastName = Normalize(EditSubject.LastName)
			};

			var response = await ReportService.EditSubjectNameAsync(payload);

			if (!response.IsSuccess || response.Data is null)
			{
				Snackbar.Add(
					string.IsNullOrWhiteSpace(response.ErrorDetail)
						? "The subject name could not be updated."
						: response.ErrorDetail,
					Severity.Error);

				return;
			}

			Snackbar.Add("Subject name updated successfully", Severity.Success);
			SubjectNameDialog!.Close(DialogResult.Ok(response.Data));
		}
		finally
		{
			IsSubmitting = false;
		}
	}

	private static string Normalize(string? value) =>
		string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

	private static string BuildSubjectName(string? firstName, string? middleName, string? lastName) =>
		string.Join(
			' ',
			new[] { firstName, middleName, lastName }
				.Where(part => !string.IsNullOrWhiteSpace(part))
				.Select(part => part!.Trim()));
}
