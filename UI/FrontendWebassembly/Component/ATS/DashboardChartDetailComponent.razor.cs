namespace FrontendWebassembly.Component.ATS;

public partial class DashboardChartDetailComponent
{
	private const string AllRequesters = "All";
	private const double DetailChartWidth = 900;
	private const double DetailChartHeight = 480;
	private const double DetailChartLeft = 64;
	private const double DetailChartRight = 18;
	private const double DetailChartTop = 20;
	private const double DetailChartBottom = 48;

	private static readonly string[] AreaChartColors =
	[
		"#4D8DE8",
		"#9B7ADB",
		"#F2B765",
		"#16A9BE",
		"#1055A6",
		"#E06C75"
	];

	private static readonly string[] CategoryChartColors =
	[
		"#16A9BE",
		"#4D7FEA",
		"#1055A6",
		"#9C79DF"
	];

	[Parameter]
	public string? ChartKey { get; set; }

	[Parameter]
	public string? Requester { get; set; }

	[Parameter]
	public ATSDashboardDTO DashboardData { get; set; } = new();

	[CascadingParameter]
	private IMudDialogInstance MudDialog { get; set; } = default!;

	private ATSDashboardDTO _dashboard = new();
	private string _normalizedChartKey = string.Empty;
	private string _chartTitle = "Dashboard Chart";
	private bool _isSupportedChart;

	private IReadOnlyList<DetailAreaLayer> _ytdHireLayers = [];
	private IReadOnlyList<DetailAxisTick> _ytdHireYAxisTicks = [];
	private IReadOnlyList<DetailAxisTick> _ytdHireXAxisTicks = [];
	private DetailYtdHireHoverPoint? _ytdHireHover;
	private int _ytdHireGrandTotal;
	private double[] _candidateResponseData = [];
	private string[] _candidateResponseLabels = [];
	private List<ChartSeries<double>> _turnaroundTimeSeries = [];
	private string[] _turnaroundTimeLabels = [];
	private double[] _completionRateData = [];
	private string[] _completionRateLabels = [];

	private readonly DonutChartOptions _donutChartOptions = new()
	{
		ChartPalette = CategoryChartColors,
		DonutRingRatio = 0.42,
		ShowLegend = false,
		ShowValues = false
	};

	private readonly LineChartOptions _lineChartOptions = new()
	{
		ChartPalette = AreaChartColors,
		LineStrokeWidth = 4,
		ShowDataMarkers = true,
		ShowLegend = false,
		YAxisRequireZeroPoint = true,
		XAxisLines = false,
		YAxisLines = false,
		YAxisFormat = "0"
	};

	private string ChartTitle => _chartTitle;

	private string RequesterDisplayName =>
		string.IsNullOrWhiteSpace(Requester) ? AllRequesters : Requester;

	private bool HasYtdHireData =>
		_dashboard.YtdHireSeries.Any(series => series.Points.Any(point => point.Count > 0));

	private bool HasCandidateResponseData =>
		_dashboard.CandidateResponseRate.Categories.Sum(category => category.Count) > 0;

	private bool HasTurnaroundTimeData =>
		_dashboard.TurnaroundTimeTrend.Any(series =>
			series.Points.Any(point => point.Count > 0));

	private bool HasCompletionRateData =>
		_dashboard.CompletionRate.Categories.Sum(category => category.Count) > 0;

	protected override void OnParametersSet()
	{
		base.OnParametersSet();
		_normalizedChartKey = ChartKey?.Trim().ToLowerInvariant() ?? string.Empty;
		_isSupportedChart = ATSDashboardChartKeys.TryGetTitle(_normalizedChartKey, out _chartTitle);
		_ytdHireHover = null;
		_dashboard = DashboardData;

		if (_isSupportedChart)
		{
			ApplyDashboardData();
		}
	}

	private void ApplyDashboardData()
	{
		BuildYtdHireChart();

		_candidateResponseData = _dashboard.CandidateResponseRate.Categories
			.Select(category => (double)category.Count)
			.ToArray();
		_candidateResponseLabels = _dashboard.CandidateResponseRate.Categories
			.Select(category => category.Name)
			.ToArray();

		_turnaroundTimeLabels = _dashboard.TurnaroundTimeTrend
			.FirstOrDefault()?
			.Points
			.Select(point => point.Date.ToString("MMM d"))
			.ToArray() ?? [];
		_turnaroundTimeSeries = _dashboard.TurnaroundTimeTrend
			.Select(series => new ChartSeries<double>
			{
				Name = series.Name,
				Data = series.Points.Select(point => (double)point.Count).ToArray()
			})
			.ToList();

		_completionRateData = _dashboard.CompletionRate.Categories
			.Select(category => (double)category.Count)
			.ToArray();
		_completionRateLabels = _dashboard.CompletionRate.Categories
			.Select(category => category.Name)
			.ToArray();
	}

	private void BuildYtdHireChart()
	{
		_ytdHireHover = null;

		var sourceSeries = _dashboard.YtdHireSeries;
		var pointCount = sourceSeries.FirstOrDefault()?.Points.Count ?? 0;
		if (sourceSeries.Count == 0 || pointCount == 0)
		{
			_ytdHireLayers = [];
			_ytdHireYAxisTicks = [];
			_ytdHireXAxisTicks = [];
			_ytdHireGrandTotal = 0;
			return;
		}

		var monthlyTotals = Enumerable.Range(0, pointCount)
			.Select(index => sourceSeries.Sum(series =>
				index < series.Points.Count ? series.Points[index].Count : 0))
			.ToArray();
		_ytdHireGrandTotal = monthlyTotals.Sum();

		var maximumIndividualValue = sourceSeries
			.SelectMany(series => series.Points)
			.Max(point => point.Count);
		var axisMaximum = GetAxisMaximum(maximumIndividualValue);
		var plotWidth = DetailChartWidth - DetailChartLeft - DetailChartRight;
		var plotHeight = DetailChartHeight - DetailChartTop - DetailChartBottom;

		_ytdHireYAxisTicks = Enumerable.Range(0, 6)
			.Select(index =>
			{
				var value = axisMaximum * index / 5d;
				return new DetailAxisTick(
					DetailChartTop + plotHeight * (1 - value / axisMaximum),
					Math.Round(value).ToString("0"));
			})
			.ToArray();

		_ytdHireXAxisTicks = sourceSeries[0].Points
			.Take(pointCount)
			.Select((point, index) => new DetailAxisTick(
				GetChartX(index, pointCount, plotWidth),
				point.PeriodStart.ToString("MMM")))
			.ToArray();

		var layers = new List<DetailAreaLayer>(sourceSeries.Count);
		for (var seriesIndex = 0; seriesIndex < sourceSeries.Count; seriesIndex++)
		{
			var source = sourceSeries[seriesIndex];
			var hoverPoints = Enumerable.Range(0, pointCount)
				.Select(index =>
				{
					var sourcePoint = index < source.Points.Count
						? source.Points[index]
						: null;
					var value = sourcePoint?.Count ?? 0;

					return new DetailAreaPoint(
						GetChartX(index, pointCount, plotWidth),
						GetChartY(value, axisMaximum, plotHeight),
						sourcePoint?.PeriodStart.ToString("MMMM") ?? string.Empty,
						value);
				})
				.ToArray();
			var upperPoints = hoverPoints
				.Select(point => FormatSvgPoint(point.X, point.Y))
				.ToArray();
			var lowerPoints = Enumerable.Range(0, pointCount)
				.Reverse()
				.Select(index => FormatSvgPoint(
					GetChartX(index, pointCount, plotWidth),
					GetChartY(0, axisMaximum, plotHeight)));

			layers.Add(new DetailAreaLayer(
				source.Name,
				GetPaletteColor(AreaChartColors, seriesIndex),
				string.Join(" ", upperPoints.Concat(lowerPoints)),
				string.Join(" ", upperPoints),
				hoverPoints));
		}

		_ytdHireLayers = layers;
	}

	private void CloseDialog()
	{
		MudDialog.Close();
	}

	private void ShowYtdHireHover(DetailAreaLayer layer, DetailAreaPoint point)
	{
		_ytdHireHover = new DetailYtdHireHoverPoint(
			layer.Name,
			layer.Color,
			point.Month,
			point.Value,
			point.X,
			point.Y);
	}

	private void ClearYtdHireHover()
	{
		_ytdHireHover = null;
	}

	private static string GetYtdHireTooltipStyle(DetailYtdHireHoverPoint point)
	{
		var tooltipX = Math.Clamp(point.X, 120, DetailChartWidth - 120);
		var left = tooltipX / DetailChartWidth * 100;
		var top = point.Y / DetailChartHeight * 100;

		return FormattableString.Invariant($"left: {left:0.###}%; top: {top:0.###}%;");
	}

	private static string GetYtdHireTooltipClass(DetailYtdHireHoverPoint point) =>
		point.Y < 90
			? "ats-detail-area-tooltip ats-detail-area-tooltip-below"
			: "ats-detail-area-tooltip";

	private static string GetYAxisLabelStyle(double position)
	{
		var top = position / DetailChartHeight * 100;
		return FormattableString.Invariant($"top: {top:0.###}%;");
	}

	private static double GetAxisMaximum(int maximumValue)
	{
		if (maximumValue <= 0)
		{
			return 5;
		}

		var roughStep = maximumValue / 5d;
		var magnitude = Math.Pow(10, Math.Floor(Math.Log10(roughStep)));
		var normalizedStep = roughStep / magnitude;
		var niceStep = normalizedStep <= 1 ? 1 : normalizedStep <= 2 ? 2 : normalizedStep <= 5 ? 5 : 10;
		return Math.Ceiling(maximumValue / (niceStep * magnitude)) * niceStep * magnitude;
	}

	private static double GetChartX(int index, int pointCount, double plotWidth) =>
		pointCount == 1
			? DetailChartLeft + plotWidth / 2
			: DetailChartLeft + plotWidth * index / (pointCount - 1d);

	private static double GetChartY(double value, double axisMaximum, double plotHeight) =>
		DetailChartTop + plotHeight * (1 - value / axisMaximum);

	private static string FormatSvgPoint(double x, double y) =>
		$"{FormatSvgNumber(x)},{FormatSvgNumber(y)}";

	private static string FormatSvgNumber(double value) =>
		FormattableString.Invariant($"{value:0.###}");

	private static string GetAreaLegendColor(int index) =>
		GetPaletteColor(AreaChartColors, index);

	private static string GetCategoryLegendColor(int index) =>
		GetPaletteColor(CategoryChartColors, index);

	private static string GetPaletteColor(IReadOnlyList<string> palette, int index) =>
		palette[index % palette.Count];

	private sealed record DetailAreaLayer(
		string Name,
		string Color,
		string PolygonPoints,
		string LinePoints,
		IReadOnlyList<DetailAreaPoint> Points);

	private sealed record DetailAreaPoint(
		double X,
		double Y,
		string Month,
		int Value);

	private sealed record DetailYtdHireHoverPoint(
		string SeriesName,
		string Color,
		string Month,
		int Value,
		double X,
		double Y);

	private sealed record DetailAxisTick(double Position, string Label);
}
