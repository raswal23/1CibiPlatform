using FrontendWebassembly.Component.Generic;

namespace FrontendWebassembly.Component.ATS;

public partial class ATSDashboardComponent
{
	private TableComponent<TransactionRow>? transactionsTable;
	private static readonly int[] JobAcceptanceRates = { 85, 70, 92, 60 };

	private readonly List<TransactionRow> _dummyTransactions = new()
	{
		new TransactionRow("2025 - 00123456", "Antonio Aguinaldo", "Completed", "Clear"),
		new TransactionRow("2025 - 00129876", "Teodoro Mabini", "Completed", "Clear"),
		new TransactionRow("2025 - 00124356", "Maria Quezon", "Completed", "Not Clear"),
		new TransactionRow("2025 - 00198765", "Angela Osmena", "Pending", "In Progress")
	};

	private record TransactionRow(string Ticket, string Subject, string Status, string Result);

	private Task<TableData<TransactionRow>> LoadTransactionData(TableState state, CancellationToken cancellationToken)
	{
		return Task.FromResult(new TableData<TransactionRow>
		{
			Items = _dummyTransactions,
			TotalItems = _dummyTransactions.Count
		});
	}


	private static readonly string[] Departments = { "IT", "Marketing", "Sales", "HR" };

	private static readonly string[] ChartPalette =
	{
		"#102247", // 0%
        "#2a77ae", // 33%
        "#68c0d6", // 66%
        "#a0e0eb"  // 100%
    };


	DonutChartOptions donutChartOptions = new()
	{
		ChartPalette = ChartPalette,
	};

	private List<ChartSeries<double>> BarSeries = new()
	{
		new ChartSeries<double>
		{
			Name = "Compliance",
			Data = new double[] { 150, 220, 300, 280, 350 }
		}
	};

	private string[] BarLabels = new[] { "Jan", "Feb", "Mar", "Apr", "May" };

	private List<ChartSeries<double>> LineSeries = new()
	{
		new ChartSeries<double>
		{
			Name = "Tech Hiring",
			Data = new double[] { 10, 15, 20, 14, 18 }
		},
		new ChartSeries<double>
		{
			Name = "Operations Hiring",
			Data = new double[] { 8, 12, 16, 13, 15 }
		}
	};

	private string[] LineLabels = new[] { "Week 1", "Week 2", "Week 3", "Week 4", "Week 5" };

	private LineChartOptions lineChartOptions = new()
	{
		LineStrokeWidth = 5,
		ShowDataMarkers = true
	};

	private BarChartOptions barChartOptions = new()
	{
		BarSpacingRatio = 0.05,
		BarWidthRatio = 10,
	};


}
