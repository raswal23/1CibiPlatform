namespace ATS.Services.Dashboard;

public class DashboardService : IDashboardService
{
	// Everything the dashboard renders lives inside the current year (the YTD series)
	// or the trailing 7-day turnaround window. Loading a year is generous for both and
	// keeps the query bounded as the table grows.
	private static readonly int DashboardWindowMonths = 12;

	/// <summary>How many orders the "recent orders" panel carries.</summary>
	private const int RecentOrderCount = 25;

	private readonly IATSRepository _atsRepository;
	private readonly IAtsAccessScopeResolver _accessScopeResolver;

	public DashboardService(
		IATSRepository atsRepository,
		IAtsAccessScopeResolver accessScopeResolver)
	{
		_atsRepository = atsRepository;
		_accessScopeResolver = accessScopeResolver;
	}

	public async Task<ATSDashboardDTO> GetDashboardAsync(
		string? requester,
		CancellationToken cancellationToken)
	{
		// The role ladder lives in AtsAccessScopeResolver now - this used to be an
		// inline copy of it.
		if (await _accessScopeResolver.ResolveAsync(cancellationToken) is not { } scope)
		{
			return new ATSDashboardDTO();
		}

		var now = DateTime.UtcNow;
		var yearStart = new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);

		// Whichever is earlier: the start of this calendar year, or 12 months back. In
		// January the rolling window is the wider of the two.
		var windowStart = yearStart < now.AddMonths(-DashboardWindowMonths)
			? yearStart
			: now.AddMonths(-DashboardWindowMonths);

		var invitations = await _atsRepository.GetDashboardDataAsync(
			scope.AuthorizedClientIds,
			scope.RequiredOwnerId,
			windowStart,
			cancellationToken);

		return CreateDashboard(invitations, requester);
	}

	private static ATSDashboardDTO CreateDashboard(
		IReadOnlyList<EmailInvitationRequest> authorizedInvitations,
		string? requester)
	{
		var now = DateTime.UtcNow;
		var yearStart = new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
		var yearEnd = yearStart.AddYears(1);
		var requesterOptions = authorizedInvitations
			.Where(invitation => !string.IsNullOrEmpty(invitation.Requestor))
			.Select(invitation => invitation.Requestor!)
			.Distinct()
			.OrderBy(value => value)
			.ToArray();

		var allYtdHireRows = authorizedInvitations
			.Where(invitation => invitation.OrderCreatedAt.HasValue
				&& invitation.OrderCreatedAt >= yearStart
				&& invitation.OrderCreatedAt < yearEnd
				&& !string.IsNullOrWhiteSpace(invitation.Requestor))
			.Select(invitation => new DashboardHireRow(
				invitation.Requestor!,
				invitation.OrderCreatedAt!.Value))
			.ToArray();
		var ytdHireRows = string.IsNullOrWhiteSpace(requester)
			? allYtdHireRows
			: allYtdHireRows.Where(row => row.Requestor == requester).ToArray();
		var invitations = string.IsNullOrWhiteSpace(requester)
			? authorizedInvitations
			: authorizedInvitations.Where(invitation => invitation.Requestor == requester).ToArray();

		var ytdPeriods = Enumerable.Range(0, 12)
			.Select(monthOffset => yearStart.AddMonths(monthOffset))
			.ToArray();
		var ytdHireSeries = ytdHireRows
			.GroupBy(row => row.Requestor)
			.OrderByDescending(group => group.Count())
			.ThenBy(group => group.Key)
			.Select(group =>
			{
				var countLookup = group
					.GroupBy(row => (row.OrderCreatedAt.Year, row.OrderCreatedAt.Month))
					.ToDictionary(monthGroup => monthGroup.Key, monthGroup => monthGroup.Count());

				return new DashboardVolumeSeriesDTO
				{
					Name = group.Key,
					Points = ytdPeriods.Select(periodStart => new DashboardVolumePointDTO
					{
						PeriodStart = periodStart,
						Count = countLookup.GetValueOrDefault((periodStart.Year, periodStart.Month))
					}).ToArray()
				};
			})
			.ToArray();

		var sentInvitations = invitations
			.Where(invitation => invitation.EmailSentStatus == EmailStatus.Done)
			.ToArray();
		var completedResponses = sentInvitations.Count(invitation =>
			invitation.ApplicationFormStatus == ApplicationFormStatus.Done
			|| invitation.FormCompletedAt.HasValue);
		var incompleteResponses = sentInvitations.Count(invitation =>
			invitation.ApplicationFormStatus != ApplicationFormStatus.Done
			&& !invitation.FormCompletedAt.HasValue
			&& invitation.ApplicationFormStatus == ApplicationFormStatus.Withdrawn);
		var notStartedResponses = sentInvitations.Length - completedResponses - incompleteResponses;

		var reportRows = invitations
			.SelectMany(invitation => (invitation.ReportDetails ?? [])
				.Select(report => new DashboardReportRow
				{
					ReportStatus = report.ReportStatus,
					HitStatus = report.HitStatus,
					ReportUploadedAt = report.ReportUploadedAt,
					RushNormal = invitation.RushNormal
				}))
			.ToArray();
		var serviceLevelRows = reportRows.Where(IsServiceLevelReport).ToArray();
		var latestTurnaroundDate = serviceLevelRows
			.Select(report => (DateTime?)report.ReportUploadedAt)
			.Max();
		var turnaroundEndDate = latestTurnaroundDate?.Date ?? now.Date;
		var turnaroundStart = turnaroundEndDate.AddDays(-6);
		var turnaroundPeriods = Enumerable.Range(0, 7)
			.Select(dayOffset => turnaroundStart.AddDays(dayOffset))
			.ToArray();
		var turnaroundTimeTrend = new (string Name, Func<DashboardReportRow, bool> Matches)[]
			{
				("Complete", report => report.ReportStatus == ReportStatus.CompleteFinalReport),
				("Closed", report => report.ReportStatus == ReportStatus.ClosedFinalReport),
				("Clear", report => string.Equals(report.HitStatus, "Clear", StringComparison.OrdinalIgnoreCase)),
				("Not Clear", report => string.Equals(report.HitStatus, "Not Clear", StringComparison.OrdinalIgnoreCase))
			}
			.Select(series => new TurnaroundTimeSeriesDTO
			{
				Name = series.Name,
				Points = turnaroundPeriods.Select(date => new TurnaroundTimePointDTO
				{
					Date = date,
					Count = serviceLevelRows.Count(report =>
						report.ReportUploadedAt.Date == date.Date
						&& series.Matches(report))
				}).ToArray()
			})
			.ToArray();

		var completeReports = reportRows.Count(report => report.ReportStatus == ReportStatus.CompleteFinalReport);
		var closedReports = reportRows.Count(report => report.ReportStatus == ReportStatus.ClosedFinalReport);
		var initialReports = reportRows.Count(report => report.ReportStatus == ReportStatus.InitialReport);
		var supplementaryReports = reportRows.Count(report => report.ReportStatus == ReportStatus.SupplementaryReport);
		// "Recent" means recent - this used to project and serialise every invitation
		// in scope on every dashboard load.
		var recentOrders = invitations
			.OrderByDescending(invitation => invitation.OrderCreatedAt)
			.ThenByDescending(invitation => invitation.EmailInvitationID)
			.Take(RecentOrderCount)
			.Select(invitation => new DashboardRecentOrderDTO
			{
				SubjectName = $"{invitation.FirstName} {invitation.LastName}".Trim(),
				OrderStatus = invitation.OrderStatus,
				HitStatus = invitation.ReportDetails?
					.OrderByDescending(report => report.ReportUploadedAt)
					.Select(report => report.HitStatus)
					.FirstOrDefault(),
				OrderCreatedAt = invitation.OrderCreatedAt,
				OrderCompletedAt = invitation.OrderCompletedAt
			})
			.ToArray();

		return new ATSDashboardDTO
		{
			Requesters = requesterOptions,
			YtdHireSeries = ytdHireSeries,
			CandidateResponseRate = new CandidateResponseRateDTO
			{
				Categories = CreateCategories(
					("Completed", completedResponses),
					("Incomplete", incompleteResponses),
					("Not Started", notStartedResponses))
			},
			TurnaroundTimeTrend = turnaroundTimeTrend,
			CompletionRate = new CompletionRateDTO
			{
				Categories = CreateCategories(
					("Complete", completeReports),
					("Closed", closedReports),
					("Initial", initialReports),
					("Supplementary", supplementaryReports))
			},
			RecentOrders = recentOrders
		};
	}

	private static IReadOnlyList<DashboardCategoryDTO> CreateCategories(
		params (string Name, int Count)[] categoryCounts)
	{
		var total = categoryCounts.Sum(category => category.Count);
		return categoryCounts.Select(category => new DashboardCategoryDTO
		{
			Name = category.Name,
			Count = category.Count,
			Percentage = total == 0
				? 0
				: Math.Round(category.Count * 100d / total, 1)
		}).ToArray();
	}

	private static bool IsServiceLevelReport(DashboardReportRow report)
	{
		var hasServiceLevel = string.Equals(report.RushNormal?.Trim(), "Normal", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(report.RushNormal?.Trim(), "Rush", StringComparison.OrdinalIgnoreCase);

		return hasServiceLevel
			&& (report.ReportStatus == ReportStatus.CompleteFinalReport
			|| report.ReportStatus == ReportStatus.ClosedFinalReport
			|| string.Equals(report.HitStatus, "Clear", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(report.HitStatus, "Not Clear", StringComparison.OrdinalIgnoreCase));
	}

	private sealed record DashboardHireRow(string Requestor, DateTime OrderCreatedAt);

	private sealed record DashboardReportRow
	{
		public string? ReportStatus { get; init; }
		public string? HitStatus { get; init; }
		public DateTime ReportUploadedAt { get; init; }
		public string? RushNormal { get; init; }
	}
}
