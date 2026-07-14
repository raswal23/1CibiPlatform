namespace FrontendWebassembly.Component.ATS;

public partial class SearchReportComponent
{
	private bool _showSubjectResult = false;

	private ReportRow? _selectedReport;
	private TableComponent<ReportRow>? reportsTable;
	private MudForm? form;
	private TicketDetails ticketDetails = new();
	private Subject subject = new();
	private FileDetails fileDetails = new();
	private bool IsLoaded = true;

	[Parameter]
	public AddRoleDTO Role { get; set; } = new AddRoleDTO();

	private class Subject
	{
		public string SubjectName { get; set; } = "Antonio Aguinaldo";
		public string Score { get; set; } = "85%";
	}
	private DateRange? _dateRange { get; set; }
	private readonly List<ReportRow> _dummyReports = new()
	{
		new ReportRow(false, "Antonio Aguinaldo", "2025 - 00123456", "In Progress", "Pending", "October 25, 2025", "Basic"),
		new ReportRow(false, "Antonio Aguinaldo", "2025 - 00129876", "Completed", "Clear", "October 21, 2025", "Basic 2"),
		new ReportRow(false, "Antonio Aguinaldo", "2024 - 00124356", "Completed", "Not Clear", "October 20, 2024", "Lite"),
		new ReportRow(false, "Antonio Aguinaldo", "2023 - 00198765", "Completed", "Clear", "October 18, 2019", "Package 1"),
		new ReportRow(false, "Antonio Aguinaldo", "2019 - 00198765", "Completed", "Clear", "October 10, 2018", "AirBNB")
	};

	private class ReportRow
	{
		public bool Selected { get; set; }
		public string Subject { get; set; }
		public string Ticket { get; set; }
		public string Status { get; set; }
		public string Result { get; set; }
		public string DateCompleted { get; set; }
		public string ReportType { get; set; }

		public ReportRow(bool selected, string subject, string ticket, string status, string result, string dateCompleted, string reportType)
		{
			Selected = selected;
			Subject = subject;
			Ticket = ticket;
			Status = status;
			Result = result;
			DateCompleted = dateCompleted;
			ReportType = reportType;
		}
	}

	private Task<TableData<ReportRow>> LoadReportData(TableState state, CancellationToken cancellationToken)
	{
		var filtered = _dummyReports.ToList();

		return Task.FromResult(new TableData<ReportRow>
		{
			Items = filtered,
			TotalItems = filtered.Count
		});
	}

	private async Task DownloadSelected()
	{
		var selected = _dummyReports.Where(r => r.Selected).ToList();
		if (!selected.Any())
		{
			await JS.InvokeVoidAsync("console.warn", "No reports selected for download.");
			return;
		}

		await JS.InvokeVoidAsync("console.log", $"Downloading {selected.Count} reports.", selected.Select(r => r.Ticket));
	}

	private void OpenResultTriggerDialog(ReportRow row)
	{
		_selectedReport = row;
		_showSubjectResult = true;
	}

	private void BackToQueryResults()
	{
		_showSubjectResult = false;
	}
	//private async Task OpenResultTriggerDialog()
	//	=> await OpenResultDialog<ATSResultComponent>("Subject Result");

	private class TicketDetails
	{
		public string TicketNumber { get; set; } = "2025 - 00123456";
		public string Status { get; set; } = "Completed";
		public string Result { get; set; } = "Clear";
		public string ReportType { get; set; } = "Basic";
		public string AptitudeTest { get; set; } = "Passed - 90%";
		public string LiveInterview { get; set; } = "Completed";
		public string Grammar { get; set; } = "Passed - 85%";
		public string Comprehension { get; set; } = "Good";
		public string Relativeness { get; set; } = "Good";
	}

	private class FileDetails
	{
		public string Resume { get; set; } = "TonCV.pdf";
		public string ID { get; set; } = "TonID.jpg";
		public string COE { get; set; } = "TonCOE.pdf";
		public string Diploma { get; set; } = "TonTOR.pd";
		public string BiometricPhoto { get; set; } = "123-456.jpg";
		public string ConsentForm { get; set; } = "Consent_Form.pdf";
		public string FinalReport { get; set; } = "2025-00123456";
		public string UploadedDate { get; set; } = "October 19, 2025";
	}

	private async Task Download()
	{
	}

}
