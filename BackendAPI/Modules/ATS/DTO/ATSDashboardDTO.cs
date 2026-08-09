namespace ATS.DTO;

public record ATSDashboardDTO
{
	public IReadOnlyList<string> Requesters { get; init; } = [];
	public IReadOnlyList<DashboardVolumeSeriesDTO> YtdHireSeries { get; init; } = [];
	public CandidateResponseRateDTO CandidateResponseRate { get; init; } = new();
	public IReadOnlyList<TurnaroundTimeSeriesDTO> TurnaroundTimeTrend { get; init; } = [];
	public CompletionRateDTO CompletionRate { get; init; } = new();
	public IReadOnlyList<DashboardRecentOrderDTO> RecentOrders { get; init; } = [];
}

public record DashboardVolumeSeriesDTO
{
	public string Name { get; init; } = string.Empty;
	public IReadOnlyList<DashboardVolumePointDTO> Points { get; init; } = [];
}

public record DashboardVolumePointDTO
{
	public DateTime PeriodStart { get; init; }
	public int Count { get; init; }
}

public record CandidateResponseRateDTO
{
	public IReadOnlyList<DashboardCategoryDTO> Categories { get; init; } = [];
}

public record TurnaroundTimeSeriesDTO
{
	public string Name { get; init; } = string.Empty;
	public IReadOnlyList<TurnaroundTimePointDTO> Points { get; init; } = [];
}

public record TurnaroundTimePointDTO
{
	public DateTime Date { get; init; }
	public int Count { get; init; }
}

public record DashboardCategoryDTO
{
	public string Name { get; init; } = string.Empty;
	public int Count { get; init; }
	public double Percentage { get; init; }
}

public record CompletionRateDTO
{
	public IReadOnlyList<DashboardCategoryDTO> Categories { get; init; } = [];
}

public record DashboardRecentOrderDTO
{
	public string? Ticket { get; init; }
	public string? SubjectName { get; init; }
	public string? OrderStatus { get; init; }
	public string? HitStatus { get; init; }
	public DateTime? OrderCreatedAt { get; init; }
	public DateTime? OrderCompletedAt { get; init; }
}
