using FrontendWebassembly.Component.Generic;
using Microsoft.AspNetCore.Components;

namespace FrontendWebassembly.Component.ATS;

public partial class SearchReportComponent
{
	private TableComponent<ReportRow>? reportsTable;
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

	private async Task DownloadReport(ReportRow row)
	{
		await JS.InvokeVoidAsync("console.log", $"Downloading report {row.Ticket}");
	}

	private async Task OpenResultDialog<TComponent>(string title)
	where TComponent : ComponentBase
	{
		var options = new DialogOptions
		{
			CloseButton = true,
			MaxWidth = MaxWidth.Large,
			FullWidth = true
		};

		var dialog = await DialogService.ShowAsync<TComponent>(title, options);
		var result = await dialog.Result;
	}

	private async Task OpenResultTriggerDialog()
		=> await OpenResultDialog<ATSResultComponent>("Subject Result");

}
