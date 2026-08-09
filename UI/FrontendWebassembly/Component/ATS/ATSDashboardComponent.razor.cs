namespace FrontendWebassembly.Component.ATS;

public partial class ATSDashboardComponent
{
	private const string AllRequesters = "All";
	private const double StackedChartWidth = 400;
	private const double StackedChartHeight = 245;
	private const double StackedChartLeft = 35;
	private const double StackedChartRight = 8;
	private const double StackedChartTop = 8;
	private const double StackedChartBottom = 25;

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

	private TableComponent<TransactionRow>? transactionsTable;
	private ATSDashboardDTO _dashboard = new();
	private IReadOnlyList<TransactionRow> _recentOrders = [];
	private string _selectedRequester = AllRequesters;
	private bool _isYtdHireLoading = true;
	private bool _isCandidateResponseLoading = true;
	private bool _isTurnaroundTimeLoading = true;
	private bool _isCompletionRateLoading = true;
	private bool _isRecentOrdersLoading = true;
	private bool _shouldReloadRecentOrders;

	private IReadOnlyList<StackedAreaLayer> _ytdHireLayers = [];
	private int _ytdHireGrandTotal;
	private IReadOnlyList<ChartAxisTick> _ytdHireYAxisTicks = [];
	private IReadOnlyList<ChartAxisTick> _ytdHireXAxisTicks = [];
	private YtdHireHoverPoint? _ytdHireHover;
	private double[] _candidateResponseData = [];
	private string[] _candidateResponseLabels = [];
	private double[] _completionRateData = [];
	private string[] _completionRateLabels = [];
	private List<ChartSeries<double>> _turnaroundTimeSeries = [];
	private string[] _turnaroundTimeLabels = [];

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
		LineStrokeWidth = 3,
		ShowDataMarkers = true,
		ShowLegend = false,
		YAxisRequireZeroPoint = true,
		XAxisLines = false,
		YAxisLines = false,
		YAxisFormat = "0"
	};

	private bool HasYtdHireData =>
		_dashboard.YtdHireSeries.Any(series => series.Points.Any(point => point.Count > 0));

	private bool HasCandidateResponseData =>
		_dashboard.CandidateResponseRate.Categories.Sum(category => category.Count) > 0;

	private bool HasTurnaroundTimeData =>
		_dashboard.TurnaroundTimeTrend.Any(series =>
			series.Points.Any(point => point.Count > 0));

	private bool HasCompletionRateData =>
		_dashboard.CompletionRate.Categories.Sum(category => category.Count) > 0;

	private bool IsAnyDashboardSectionLoading =>
		_isYtdHireLoading
		|| _isCandidateResponseLoading
		|| _isTurnaroundTimeLoading
		|| _isCompletionRateLoading
		|| _isRecentOrdersLoading;

	protected override async Task OnInitializedAsync()
	{
		await base.OnInitializedAsync();
		if (!IsPageAuthorized)
		{
			return;
		}

		await LoadDashboardAsync();
	}

	private async Task OnRequesterChanged(string requester)
	{
		if (_selectedRequester == requester)
		{
			return;
		}

		_selectedRequester = requester;
		await LoadDashboardAsync();
	}

	private async Task LoadDashboardAsync()
	{
		SetDashboardSectionsLoading(true);

		try
		{
			var requester = _selectedRequester == AllRequesters
				? null
				: _selectedRequester;

			_dashboard = await DashboardService.GetDashboardAsync(requester);
			ApplyDashboardData();
		}
		catch (Exception)
		{
			_dashboard = new ATSDashboardDTO();
			ApplyDashboardData();
			Snackbar.Add("Failed to load ATS dashboard data.", Severity.Error);
		}
		finally
		{
			SetChartSectionsLoading(false);
			_shouldReloadRecentOrders = true;
		}
	}

	protected override async Task OnAfterRenderAsync(bool firstRender)
	{
		await base.OnAfterRenderAsync(firstRender);

		if (!_shouldReloadRecentOrders || transactionsTable?.TableRef is null)
		{
			return;
		}

		_shouldReloadRecentOrders = false;
		try
		{
			await transactionsTable.TableRef.ReloadServerData();
		}
		finally
		{
			_isRecentOrdersLoading = false;
			await InvokeAsync(StateHasChanged);
		}
	}

	private void SetDashboardSectionsLoading(bool isLoading)
	{
		SetChartSectionsLoading(isLoading);
		_isRecentOrdersLoading = isLoading;
	}

	private void SetChartSectionsLoading(bool isLoading)
	{
		_isYtdHireLoading = isLoading;
		_isCandidateResponseLoading = isLoading;
		_isTurnaroundTimeLoading = isLoading;
		_isCompletionRateLoading = isLoading;
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

		_recentOrders = _dashboard.RecentOrders
			.Select(order => new TransactionRow(
				order.Ticket,
				order.SubjectName,
				order.OrderStatus,
				order.HitStatus,
				order.OrderCreatedAt,
				order.OrderCompletedAt))
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
			_ytdHireGrandTotal = 0;
			_ytdHireYAxisTicks = [];
			_ytdHireXAxisTicks = [];
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
		var plotWidth = StackedChartWidth - StackedChartLeft - StackedChartRight;
		var plotHeight = StackedChartHeight - StackedChartTop - StackedChartBottom;

		_ytdHireYAxisTicks = Enumerable.Range(0, 6)
			.Select(index =>
			{
				var value = axisMaximum * index / 5d;
				return new ChartAxisTick(
					FormatSvgNumber(StackedChartTop + plotHeight * (1 - value / axisMaximum)),
					Math.Round(value).ToString("0"));
			})
			.ToArray();

		_ytdHireXAxisTicks = sourceSeries[0].Points
			.Take(pointCount)
			.Select((point, index) => new ChartAxisTick(
				FormatSvgNumber(GetStackedChartX(index, pointCount, plotWidth)),
				point.PeriodStart.ToString("MMM")))
			.ToArray();

		var layers = new List<StackedAreaLayer>(sourceSeries.Count);
		for (var seriesIndex = 0; seriesIndex < sourceSeries.Count; seriesIndex++)
		{
			var source = sourceSeries[seriesIndex];
			// Every requester uses its own value for its Y coordinate. The areas share
			// the zero baseline and are deliberately not stacked cumulatively.
			var hoverPoints = Enumerable.Range(0, pointCount)
				.Select(index =>
				{
					var sourcePoint = index < source.Points.Count
						? source.Points[index]
						: null;
					var value = sourcePoint?.Count ?? 0;

					return new StackedAreaPoint(
						GetStackedChartX(index, pointCount, plotWidth),
						GetStackedChartY(value, axisMaximum, plotHeight),
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
					GetStackedChartX(index, pointCount, plotWidth),
					GetStackedChartY(0, axisMaximum, plotHeight)));

			layers.Add(new StackedAreaLayer(
				source.Name,
				GetPaletteColor(AreaChartColors, seriesIndex),
				string.Join(" ", upperPoints.Concat(lowerPoints)),
				string.Join(" ", upperPoints),
				hoverPoints));
		}

		_ytdHireLayers = layers;
	}

	private void ShowYtdHireHover(StackedAreaLayer layer, StackedAreaPoint point)
	{
		_ytdHireHover = new YtdHireHoverPoint(
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

	private static string GetYtdHireTooltipStyle(YtdHireHoverPoint point)
	{
		var tooltipX = Math.Clamp(point.X, 80, StackedChartWidth - 80);
		var left = tooltipX / StackedChartWidth * 100;
		var top = point.Y / StackedChartHeight * 100;

		return FormattableString.Invariant($"left: {left:0.###}%; top: {top:0.###}%;");
	}

	private static string GetYtdHireTooltipClass(YtdHireHoverPoint point) =>
		point.Y < 65
			? "ats-stacked-area-tooltip ats-stacked-area-tooltip-below"
			: "ats-stacked-area-tooltip";

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

	private static double GetStackedChartX(int index, int pointCount, double plotWidth) =>
		pointCount == 1
			? StackedChartLeft + plotWidth / 2
			: StackedChartLeft + plotWidth * index / (pointCount - 1d);

	private static double GetStackedChartY(double value, double axisMaximum, double plotHeight) =>
		StackedChartTop + plotHeight * (1 - value / axisMaximum);

	private static string FormatSvgPoint(double x, double y) =>
		$"{FormatSvgNumber(x)},{FormatSvgNumber(y)}";

	private static string FormatSvgNumber(double value) =>
		FormattableString.Invariant($"{value:0.###}");

	private static string GetAreaLegendColor(int index) => GetPaletteColor(AreaChartColors, index);

	private static string GetPaletteColor(IReadOnlyList<string> palette, int index) =>
		palette[index % palette.Count];

	private Task<TableData<TransactionRow>> LoadTransactionData(
		TableState state,
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var items = _recentOrders
			.Skip(state.Page * state.PageSize)
			.Take(state.PageSize)
			.ToArray();

		return Task.FromResult(new TableData<TransactionRow>
		{
			Items = items,
			TotalItems = _recentOrders.Count
		});
	}

	private record TransactionRow(
		string? Ticket,
		string? Subject,
		string? Status,
		string? Result,
		DateTime? DateEndorsed,
		DateTime? CompletionDate);

	private sealed record StackedAreaLayer(
		string Name,
		string Color,
		string PolygonPoints,
		string LinePoints,
		IReadOnlyList<StackedAreaPoint> Points);

	private sealed record StackedAreaPoint(
		double X,
		double Y,
		string Month,
		int Value);

	private sealed record YtdHireHoverPoint(
		string SeriesName,
		string Color,
		string Month,
		int Value,
		double X,
		double Y);

	private sealed record ChartAxisTick(string Position, string Label);
}
