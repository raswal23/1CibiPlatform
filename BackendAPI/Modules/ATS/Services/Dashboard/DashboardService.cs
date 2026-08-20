namespace ATS.Services.Dashboard;

public class DashboardService : IDashboardService
{
	private readonly IATSRepository _atsRepository;
	private readonly IUserClientRepository _userClientRepository;
	private readonly ICurrentUser _currentUser;

	public DashboardService(
		IATSRepository atsRepository,
		IUserClientRepository userClientRepository,
		ICurrentUser currentUser)
	{
		_atsRepository = atsRepository;
		_userClientRepository = userClientRepository;
		_currentUser = currentUser;
	}

	public async Task<ATSDashboardDTO> GetDashboardAsync(
		string? requester,
		CancellationToken cancellationToken)
	{
		if (!_currentUser.IsAuthenticated
			|| _currentUser.UserId is not { } userId
			|| userId == Guid.Empty)
		{
			return new ATSDashboardDTO();
		}

		IReadOnlyCollection<int>? clientIds;
		Guid? requiredRequestorId;
		if (_currentUser.IsPlatformSuperAdmin)
		{
			clientIds = null;
			requiredRequestorId = null;
		}
		else if (_currentUser.AtsRoleId is not { } roleId)
		{
			return new ATSDashboardDTO();
		}
		else if (roleId is AtsRoleIds.PlatformManager or AtsRoleIds.Admin)
		{
			var assignments = await _userClientRepository.GetUserClientAssignmentsAsync(
				[userId],
				cancellationToken);
			clientIds = assignments
				.Select(assignment => assignment.ClientId)
				.Distinct()
				.ToArray();
			requiredRequestorId = null;
		}
		else if (roleId is AtsRoleIds.User or AtsRoleIds.Uploader
			&& _currentUser.AtsClientId is { } clientId)
		{
			clientIds = [clientId];
			requiredRequestorId = userId;
		}
		else
		{
			return new ATSDashboardDTO();
		}

		var invitations = await _atsRepository.GetDashboardDataAsync(
			clientIds,
			requiredRequestorId,
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
		var recentOrders = invitations
			.OrderByDescending(invitation => invitation.OrderCreatedAt)
			.ThenByDescending(invitation => invitation.EmailInvitationID)
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
