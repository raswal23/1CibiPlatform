namespace ATS.Data.Repository;

public class ATSRepository : IATSRepository
{

	private readonly ATSDBContext _dbcontext;

	public ATSRepository(ATSDBContext dbcontext)
	{
		_dbcontext = dbcontext;
	}

	public async Task<bool> AddPersonalDetailsAsync(PersonalDetails personalDetails)
	{
		await _dbcontext.PersonalDetails.AddAsync(personalDetails);
		return true;
	}

	public async Task<bool> AddAddressDetailsAsync(AddressDetails addressDetails)
	{
		await _dbcontext.AddressDetails.AddAsync(addressDetails);
		return true;
	}

	public async Task<bool> AddEducationalBackgroundAsync(EducationalBackground educationalBackground)
	{
		await _dbcontext.EducationalBackgrounds.AddAsync(educationalBackground);
		return true;
	}

	public async Task<bool> AddLicensesDetailsAsync(LicensesDetails licensesDetails)
	{
		await _dbcontext.LicensesDetails.AddAsync(licensesDetails);
		return true;
	}

	public async Task<bool> AddProfessionalExperiencesAsync(ProfessionalExperiences professionalExperiences)
	{
		await _dbcontext.ProfessionalExperiences.AddAsync(professionalExperiences);
		return true;
	}

	public async Task<bool> AddReferenceDetailsAsync(ReferenceDetails referenceDetails)
	{
		await _dbcontext.ReferenceDetails.AddAsync(referenceDetails);
		return true;
	}

	public async Task<EmailIdAndApplicationFormPathDTO> GetEmailIdAndApplicationFormPathAsync(string hashToken,
						CancellationToken cancellationToken)
	{
		return await _dbcontext.EmailInvitationRequests
				.AsNoTracking()
				.Where(af => af.HashToken == hashToken)
				.Select(af => new EmailIdAndApplicationFormPathDTO
				{
					EmailId = af.EmailInvitationID,
					ExpiresAt = af.HashTokenExpiration,
					Status = af.ApplicationFormStatus
				})
				.FirstOrDefaultAsync(cancellationToken) ?? new EmailIdAndApplicationFormPathDTO();
	}

	public async Task<bool> AddSignatureDetailsAsync(SignatureDetails signatureDetails)
	{
		await _dbcontext.SignatureDetails.AddAsync(signatureDetails);
		return true;
	}

	public async Task<bool> AddEmailInvitationRequestAsync(EmailInvitationRequest emailInvitationRequest)
	{
		await _dbcontext.EmailInvitationRequests.AddAsync(emailInvitationRequest);
		await _dbcontext.SaveChangesAsync();
		return true;
	}

	public async Task<bool> AddBulkUploadFileDetailsAsync(BulkUploadFileDetails bulkUploadFileDetails)
	{
		await _dbcontext.BulkUploadFileDetails.AddAsync(bulkUploadFileDetails);
		await _dbcontext.SaveChangesAsync();
		return true;
	}

	public async Task<List<BulkUploadFileDetails>> GetBulkUploadFileDetailsAsync()
	{
		return await _dbcontext.BulkUploadFileDetails
			.AsNoTracking()
			.Where(bf => bf.Status == BulkFileStatus.Pending)
			.OrderBy(bf => bf.FileID)
			.Take(10)
			.ToListAsync();
	}

	// An invitation is retried until this many failed sends, then it stays Error for a
	// human to look at - a mistyped or dead address must not consume the daily quota
	// forever.
	private const int MaxEmailSendAttempts = 5;

	public async Task<List<EmailInvitationRequest>> GetPendingEmailInvitationRequestsAsync()
	{
		// Claim and return in one statement. FOR UPDATE SKIP LOCKED lets a concurrent
		// worker step over rows another worker is already claiming instead of blocking,
		// and the Processing write is what keeps the claim after this transaction ends.
		// EF cannot express SKIP LOCKED, so this is raw SQL.
		return await _dbcontext.EmailInvitationRequests
			.FromSqlRaw(
				"""
				UPDATE ats."EmailInvitationRequest"
				SET "EmailSentStatus" = {0},
					"EmailClaimedAt" = {1}
				WHERE "EmailInvitationID" IN (
					SELECT "EmailInvitationID"
					FROM ats."EmailInvitationRequest"
					WHERE ("EmailSentStatus" = {2}
						OR ("EmailSentStatus" = {3} AND "EmailSendAttempts" < {4}))
					ORDER BY "OrderCreatedAt"
					LIMIT {5}
					FOR UPDATE SKIP LOCKED
				)
				RETURNING *;
				""",
				EmailStatus.Processing,
				DateTime.UtcNow,
				EmailStatus.Pending,
				EmailStatus.Error,
				MaxEmailSendAttempts,
				100)
			.AsNoTracking()
			.ToListAsync();
	}

	public async Task<int> ReleaseStaleEmailInvitationClaimsAsync(TimeSpan staleAfter)
	{
		// A crash mid-send leaves rows stuck in Processing with no live worker. Anything
		// claimed longer ago than staleAfter goes back to Pending for the next tick.
		var cutoff = DateTime.UtcNow.Subtract(staleAfter);

		return await _dbcontext.EmailInvitationRequests
			.Where(x => x.EmailSentStatus == EmailStatus.Processing
					 && x.EmailClaimedAt != null
					 && x.EmailClaimedAt < cutoff)
			.ExecuteUpdateAsync(setters => setters
				.SetProperty(x => x.EmailSentStatus, x => EmailStatus.Pending)
				.SetProperty(x => x.EmailClaimedAt, x => null));
	}

	public async Task<bool> AddBulkEmailInvitationRequestAsync(List<EmailInvitationRequest> emailInvitationRequests)
	{
		await _dbcontext.EmailInvitationRequests.AddRangeAsync(emailInvitationRequests);
		await _dbcontext.SaveChangesAsync();
		return true;
	}

	public async Task<bool> UpdateBulkEmailInvitationRequestForSentEmailAsync(List<EmailInvitationRequest> emailInvitationRequests)
	{
		var ids = emailInvitationRequests.Select(x => x.EmailInvitationID).ToList();

		await _dbcontext.EmailInvitationRequests
			.Where(x => ids.Contains(x.EmailInvitationID))
			.ExecuteUpdateAsync(setters => setters
			.SetProperty(x => x.EmailSentStatus, x => EmailStatus.Done)
			.SetProperty(x => x.EmailSentAt, x => DateTime.UtcNow)
			.SetProperty(x => x.EmailClaimedAt, x => null));

		return true;
	}

	public async Task<bool> UpdateBulkEmailInvitationRequestForNotSentEmailAsync(List<EmailInvitationRequest> emailInvitationRequests)
	{
		var ids = emailInvitationRequests.Select(x => x.EmailInvitationID).ToList();

		await _dbcontext.EmailInvitationRequests
			.Where(x => ids.Contains(x.EmailInvitationID))
			.ExecuteUpdateAsync(setters => setters
			.SetProperty(x => x.EmailSentStatus, x => EmailStatus.Error)
			.SetProperty(x => x.EmailClaimedAt, x => null)
			.SetProperty(x => x.EmailSendAttempts, x => x.EmailSendAttempts + 1));

		return true;
	}

	public async Task<bool> UpdateEmailInvitationRequestForFilledUpFormAsync(Guid emailInvitationRequestId)
	{

		await _dbcontext.EmailInvitationRequests
			.Where(x => x.EmailInvitationID == emailInvitationRequestId)
			.ExecuteUpdateAsync(setters => setters
			.SetProperty(x => x.ApplicationFormStatus, x => ApplicationFormStatus.Done)
			.SetProperty(x => x.FormCompletedAt, x => DateTime.UtcNow)
			.SetProperty(
				x => x.OrderStatus,
				x => x.OrderStatus == OrderStatus.Completed
					? x.OrderStatus
					: OrderStatus.InProgress)
			.SetProperty(x => x.NeedsProjection, x => true));

		return true;
	}

	public async Task<bool> UpdateBulkFileDetailsStatusAsync(List<BulkUploadFileDetails> bulkUploadFileDetails)
	{
		var fileIds = bulkUploadFileDetails.Select(x => x.FileID).ToList();

		await _dbcontext.BulkUploadFileDetails
				.Where(x => fileIds.Contains(x.FileID))
				.ExecuteUpdateAsync(setters => setters
				.SetProperty(x => x.Status, x => BulkFileStatus.Done));

		return true;
	}

	public async Task<bool> UpdateSingleEmailInvitationRequestStatusForSentEmailAsync(Guid emailInvitationId)
	{
		await _dbcontext.EmailInvitationRequests.Where(x => x.EmailInvitationID == emailInvitationId)
				.ExecuteUpdateAsync(setters => setters
				.SetProperty(x => x.EmailSentStatus, x => EmailStatus.Done)
				.SetProperty(x => x.EmailSentAt, x => DateTime.UtcNow));

		return true;
	}

	public async Task<bool> UpdateSingleEmailInvitationRequestStatusForNotSentEmailAsync(Guid emailInvitationId)
	{
		await _dbcontext.EmailInvitationRequests.Where(x => x.EmailInvitationID == emailInvitationId)
				.ExecuteUpdateAsync(setters => setters
				.SetProperty(x => x.EmailSentStatus, x => EmailStatus.Error));

		return true;
	}

	public async Task<bool> IsHashTokenValidAsync(string hashToken, CancellationToken cancellationToken)
	{
		return await _dbcontext.EmailInvitationRequests
			.AsNoTracking()
			.AnyAsync(eir => eir.HashToken == hashToken &&
					  eir.HashTokenExpiration > DateTime.UtcNow,
					  cancellationToken);
	}

	public async Task<int> WithdrawnApplicationForm(string hashToken, CancellationToken cancellationToken)
	{
		return await _dbcontext.EmailInvitationRequests.Where(x => x.HashToken == hashToken)
				.ExecuteUpdateAsync(setters => setters
				.SetProperty(x => x.ApplicationFormStatus, x => ApplicationFormStatus.Withdrawn)
				.SetProperty(x => x.OrderStatus, x => OrderStatus.ApplicationWithdrawn));
	}

	public async Task<PaginatedResult<EmailInvitationRequestListDTO>> GetWithdrawnEmailInvitationRequestsAsync(PaginationRequest paginationRequest, AtsQueryScope scope, CancellationToken cancellationToken)
	{
		var usersQuery = ApplyQueryScope(
				_dbcontext.EmailInvitationRequests.AsNoTracking(),
				scope)
			.Where(eir => eir.OrderStatus == OrderStatus.ApplicationWithdrawn);

		var totalRecords = await usersQuery.CountAsync(cancellationToken);

		var items = await usersQuery
					.OrderBy(eir => eir.EmailInvitationID)
					.Skip((paginationRequest.PageIndex - 1) * paginationRequest.PageSize)
					.Take(paginationRequest.PageSize)
					.Select(eir => new EmailInvitationRequestListDTO
					{
						EmailInvitationID = eir.EmailInvitationID,
						EmailAddress = eir.EmailAddress,
						FirstName = eir.FirstName,
						LastName = eir.LastName,
						Requestor = eir.Requestor,
						OrderStatus = eir.OrderStatus,
					})
					.ToListAsync(cancellationToken);

		return new PaginatedResult<EmailInvitationRequestListDTO>(
			paginationRequest.PageIndex,
			paginationRequest.PageSize,
			totalRecords,
			items);
	}

	public async Task<PaginatedResult<EmailInvitationRequestListDTO>> SearchWithdrawnEmailInvitationRequestsAsync(PaginationRequest paginationRequest, AtsQueryScope scope, CancellationToken cancellationToken)
	{
		var usersQuery = ApplyQueryScope(
							_dbcontext.EmailInvitationRequests.AsNoTracking(),
							scope)
							.Where(eir => eir.OrderStatus == OrderStatus.ApplicationWithdrawn)
							.Where(eir =>
								EF.Functions.ILike(eir.FirstName!, $"%{paginationRequest.SearchTerm}%") ||
								EF.Functions.ILike(eir.MiddleInitial ?? string.Empty, $"%{paginationRequest.SearchTerm}%") ||
				EF.Functions.ILike(eir.LastName!, $"%{paginationRequest.SearchTerm}%") ||
				EF.Functions.ILike(eir.Requestor ?? string.Empty, $"%{paginationRequest.SearchTerm}%") ||
				EF.Functions.ILike(eir.EmailAddress!, $"%{paginationRequest.SearchTerm}%"));

		var totalRecords = await usersQuery.CountAsync(cancellationToken);

		var users = await usersQuery
					.OrderBy(eir => eir.EmailInvitationID)
					.Skip((paginationRequest.PageIndex - 1) * paginationRequest.PageSize)
					.Take(paginationRequest.PageSize)
					.Select(eir => new EmailInvitationRequestListDTO
					{
						EmailInvitationID = eir.EmailInvitationID,
						EmailAddress = eir.EmailAddress,
						FirstName = eir.FirstName,
						LastName = eir.LastName,
						Requestor = eir.Requestor,
						OrderStatus = eir.OrderStatus,
					})
					.ToListAsync(cancellationToken);

		return new PaginatedResult<EmailInvitationRequestListDTO>(
		  paginationRequest.PageIndex,
		  paginationRequest.PageSize,
		  totalRecords,
		  users
		);
	}

	public async Task<PaginatedResult<DisputeOrderListDTO>> GetDisputeOrdersAsync(PaginationRequest paginationRequest, AtsQueryScope scope, CancellationToken cancellationToken)
	{
		var disputeWindowStart = DateTime.UtcNow.AddDays(-30);

		var usersQuery = ApplyQueryScope(
				_dbcontext.EmailInvitationRequests.AsNoTracking(),
				scope)
			.Where(eir => eir.OrderStatus == OrderStatus.Completed && eir.OrderCreatedAt.HasValue && eir.OrderCompletedAt!.Value >= disputeWindowStart);

		var totalRecords = await usersQuery.LongCountAsync(cancellationToken);

		var items = await usersQuery
			.OrderByDescending(eir => !string.IsNullOrEmpty(eir.DisputeCategory))
			.ThenByDescending(eir => eir.OrderCreatedAt)
			.ThenBy(eir => eir.EmailInvitationID)
			.Skip((paginationRequest.PageIndex - 1) * paginationRequest.PageSize)
			.Take(paginationRequest.PageSize)
			.Select(eir => new DisputeOrderListDTO
			{
				EmailInvitationID = eir.EmailInvitationID,
				FirstName = eir.FirstName,
				LastName = eir.LastName,
				Requestor = eir.Requestor,
				DisputeCategory = eir.DisputeCategory,
				OrderCreatedAt = eir.OrderCreatedAt,
				OrderCompletedAt = eir.OrderCompletedAt
			})
			.ToListAsync(cancellationToken);

		return new PaginatedResult<DisputeOrderListDTO>(
			paginationRequest.PageIndex,
			paginationRequest.PageSize,
			totalRecords,
			items);
	}

	public async Task<PaginatedResult<DisputeOrderListDTO>> SearchDisputeOrdersAsync(PaginationRequest paginationRequest, AtsQueryScope scope, CancellationToken cancellationToken)
	{
		var disputeWindowStart = DateTime.UtcNow.AddDays(-30);

		var usersQuery = ApplyQueryScope(
				_dbcontext.EmailInvitationRequests.AsNoTracking(),
				scope)
			.Where(eir =>
				(eir.OrderStatus == OrderStatus.Completed && eir.OrderCreatedAt.HasValue && eir.OrderCompletedAt!.Value >= disputeWindowStart) &&
			   (EF.Functions.ILike(eir.FirstName!, $"%{paginationRequest.SearchTerm}%") ||
				EF.Functions.ILike(eir.LastName!, $"%{paginationRequest.SearchTerm}%") ||
				EF.Functions.ILike(eir.Requestor ?? string.Empty, $"%{paginationRequest.SearchTerm}%") ||
				EF.Functions.ILike(eir.EmailAddress!, $"%{paginationRequest.SearchTerm}%")));

		var totalRecords = await usersQuery.LongCountAsync(cancellationToken);

		var items = await usersQuery
			.OrderByDescending(eir => !string.IsNullOrEmpty(eir.DisputeCategory))
			.ThenByDescending(eir => eir.OrderCreatedAt)
			.ThenBy(eir => eir.EmailInvitationID)
			.Skip((paginationRequest.PageIndex - 1) * paginationRequest.PageSize)
			.Take(paginationRequest.PageSize)
			.Select(eir => new DisputeOrderListDTO
			{
				EmailInvitationID = eir.EmailInvitationID,
				FirstName = eir.FirstName,
				LastName = eir.LastName,
				Requestor = eir.Requestor,
				DisputeCategory = eir.DisputeCategory,
				OrderCreatedAt = eir.OrderCreatedAt,
				OrderCompletedAt = eir.OrderCompletedAt,
			})
			.ToListAsync(cancellationToken);

		return new PaginatedResult<DisputeOrderListDTO>
			(
			  paginationRequest.PageIndex,
			  paginationRequest.PageSize,
			  totalRecords,
			  items
			);
	}

	public async Task<bool> MarkAsDisputedAsync(DisputeOrderRequestDTO disputeRequest, CancellationToken cancellationToken)
	{
		var affectedRows = await _dbcontext.EmailInvitationRequests
			.Where(eir => eir.EmailInvitationID == disputeRequest.EmailInvitationId)
			.ExecuteUpdateAsync(setters => setters
				.SetProperty(eir => eir.DisputeCategory, disputeRequest.DisputeReason)
				.SetProperty(eir => eir.DisputedAt, DateTime.UtcNow),
				cancellationToken);

		return affectedRows > 0;
	}

	public async Task<ReportDetails?> GetReportDetailsByStatusAsync(Guid emailInvitationRequestId, string reportStatus, CancellationToken cancellationToken)
	{
		return await _dbcontext.ReportDetails
			.AsNoTracking()
			.FirstOrDefaultAsync(x => x.EmailInvitationRequestId == emailInvitationRequestId && x.ReportStatus == reportStatus, cancellationToken);
	}

	public async Task<bool> AddReportDetailsAsync(ReportDetails reportDetails, CancellationToken cancellationToken)
	{
		await _dbcontext.ReportDetails.AddAsync(reportDetails, cancellationToken);
		await _dbcontext.SaveChangesAsync(cancellationToken);
		return true;
	}

	public async Task<bool> UpdateReportDetailsAsync(ReportDetails reportDetails, CancellationToken cancellationToken)
	{
		var affectedRows = await _dbcontext.ReportDetails
			.Where(x => x.ReportFileId == reportDetails.ReportFileId)
			.ExecuteUpdateAsync(setters => setters
				.SetProperty(x => x.HitStatus, reportDetails.HitStatus)
				.SetProperty(x => x.ReportFileName, reportDetails.ReportFileName)
				.SetProperty(x => x.ReportFileKey, reportDetails.ReportFileKey)
				.SetProperty(x => x.ReportUploadedAt, reportDetails.ReportUploadedAt),
				cancellationToken);

		return affectedRows > 0;
	}

	public async Task<bool> UpdateOrderStatusAsync(Guid EmailInvitationRequestId, string orderStatus, DateTime? orderCompletedAt, CancellationToken cancellationToken)
	{
		var affectedRows = await _dbcontext.EmailInvitationRequests
			.Where(x => x.EmailInvitationID == EmailInvitationRequestId)
			.ExecuteUpdateAsync(setters => setters
				.SetProperty(x => x.OrderStatus,
							 x => x.OrderStatus == OrderStatus.Completed ? x.OrderStatus : orderStatus)
				.SetProperty(x => x.OrderCompletedAt, orderCompletedAt),
				cancellationToken);

		return affectedRows > 0;
	}


	public async Task<bool> AddArchiveReportAsync(ArchiveReport archiveReport, CancellationToken cancellationToken)
	{
		await _dbcontext.ArchiveReports.AddAsync(archiveReport, cancellationToken);
		await _dbcontext.SaveChangesAsync(cancellationToken);
		return true;
	}

	public async Task<ATSDashboardDTO> GetDashboardAsync(
		string? requester,
		AtsQueryScope scope,
		CancellationToken cancellationToken)
	{
		var now = DateTime.UtcNow;
		var yearStart = new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
		var yearEnd = yearStart.AddYears(1);
		var invitations = ApplyQueryScope(
			_dbcontext.EmailInvitationRequests.AsNoTracking(),
			scope);

		var requesterOptions = await invitations
			.Where(x => x.Requestor != null && x.Requestor != string.Empty)
			.Select(x => x.Requestor!)
			.Distinct()
			.OrderBy(x => x)
			.ToListAsync(cancellationToken);

		var allYtdHireRows = await invitations
			.Where(x => x.OrderCreatedAt.HasValue
				&& x.OrderCreatedAt >= yearStart
				&& x.OrderCreatedAt < yearEnd
				&& x.Requestor != null
				&& x.Requestor.Trim() != string.Empty)
			.Select(x => new
			{
				Requestor = x.Requestor!,
				OrderCreatedAt = x.OrderCreatedAt!.Value
			})
			.ToListAsync(cancellationToken);
		var ytdHireRows = string.IsNullOrWhiteSpace(requester)
			? allYtdHireRows
			: allYtdHireRows
				.Where(x => x.Requestor == requester)
				.ToList();

		if (!string.IsNullOrWhiteSpace(requester))
		{
			invitations = invitations.Where(x => x.Requestor == requester);
		}

		var ytdPeriods = Enumerable.Range(0, 12)
			.Select(monthOffset => yearStart.AddMonths(monthOffset))
			.ToArray();

		var ytdHireSeries = ytdHireRows
			.GroupBy(x => x.Requestor)
			.OrderByDescending(group => group.Count())
			.ThenBy(group => group.Key)
			.Select(group =>
			{
				var countLookup = group
					.GroupBy(x => (x.OrderCreatedAt.Year, x.OrderCreatedAt.Month))
					.ToDictionary(monthGroup => monthGroup.Key, monthGroup => monthGroup.Count());

				return new DashboardVolumeSeriesDTO
				{
					Name = group.Key,
					Points = ytdPeriods
						.Select(periodStart => new DashboardVolumePointDTO
						{
							PeriodStart = periodStart,
							Count = countLookup.GetValueOrDefault((periodStart.Year, periodStart.Month))
						})
						.ToArray()
				};
			})
			.ToArray();

		var sentInvitations = invitations.Where(x => x.EmailSentStatus == EmailStatus.Done);
		var responseCounts = await sentInvitations
			.GroupBy(_ => 1)
			.Select(x => new
			{
				Total = x.Count(),
				Completed = x.Count(invitation =>
					invitation.ApplicationFormStatus == ApplicationFormStatus.Done
					|| invitation.FormCompletedAt.HasValue),
				Incomplete = x.Count(invitation =>
					invitation.ApplicationFormStatus != ApplicationFormStatus.Done
					&& !invitation.FormCompletedAt.HasValue
					&& invitation.ApplicationFormStatus == ApplicationFormStatus.Withdrawn)
			})
			.FirstOrDefaultAsync(cancellationToken);

		var completedResponses = responseCounts?.Completed ?? 0;
		var incompleteResponses = responseCounts?.Incomplete ?? 0;
		var notStartedResponses = (responseCounts?.Total ?? 0) - completedResponses - incompleteResponses;

		var reportRows = await invitations
			.SelectMany(invitation => invitation.ReportDetails!
				.Select(report => new DashboardReportRow
				{
					ReportStatus = report.ReportStatus,
					HitStatus = report.HitStatus,
					ReportUploadedAt = report.ReportUploadedAt,
					RushNormal = invitation.RushNormal
				}))
			.ToListAsync(cancellationToken);

		var serviceLevelRows = reportRows
			.Where(IsServiceLevelReport)
			.ToArray();
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
				Points = turnaroundPeriods
					.Select(date => new TurnaroundTimePointDTO
					{
						Date = date,
						Count = serviceLevelRows.Count(report =>
							report.ReportUploadedAt.Date == date.Date
							&& series.Matches(report))
					})
					.ToArray()
			})
			.ToArray();

		var completeReports = reportRows.Count(report => report.ReportStatus == ReportStatus.CompleteFinalReport);
		var closedReports = reportRows.Count(report => report.ReportStatus == ReportStatus.ClosedFinalReport);
		var initialReports = reportRows.Count(report => report.ReportStatus == ReportStatus.InitialReport);
		var supplementaryReports = reportRows.Count(report => report.ReportStatus == ReportStatus.SupplementaryReport);

		var recentOrders = await invitations
			.OrderByDescending(x => x.OrderCreatedAt)
			.ThenByDescending(x => x.EmailInvitationID)
			.Select(x => new DashboardRecentOrderDTO
			{
				SubjectName = $"{x.FirstName} {x.LastName}".Trim(),
				OrderStatus = x.OrderStatus,
				HitStatus = x.ReportDetails!
					.OrderByDescending(report => report.ReportUploadedAt)
					.Select(report => report.HitStatus)
					.FirstOrDefault(),
				OrderCreatedAt = x.OrderCreatedAt,
				OrderCompletedAt = x.OrderCompletedAt
			})
			.ToListAsync(cancellationToken);

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
		return categoryCounts
			.Select(category => new DashboardCategoryDTO
			{
				Name = category.Name,
				Count = category.Count,
				Percentage = CalculatePercentage(category.Count, total)
			})
			.ToArray();
	}

	private static double CalculatePercentage(int numerator, int denominator)
	{
		return denominator == 0
			? 0
			: Math.Round(numerator * 100d / denominator, 1);
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

	private sealed record DashboardReportRow
	{
		public string? ReportStatus { get; init; }
		public string? HitStatus { get; init; }
		public DateTime ReportUploadedAt { get; init; }
		public string? RushNormal { get; init; }
	}

	public async Task<PaginatedResult<ReportListDTO>> GetReportsAsync(PaginationRequest paginationRequest, AtsQueryScope scope, string? sortColumn, bool sortDescending, CancellationToken cancellationToken)
	{
		var usersQuery = ApplyQueryScope(
			_dbcontext.EmailInvitationRequests.AsNoTracking(),
			scope)
			.Select(eir => new
			{
				eir.EmailInvitationID,
				eir.FirstName,
				eir.LastName,
				eir.Requestor,
				eir.OrderStatus,
				eir.OrderCompletedAt,
				eir.SelectPackage,
				HitStatus = _dbcontext.ReportDetails
					.Where(rd => rd.EmailInvitationRequestId == eir.EmailInvitationID)
					.OrderByDescending(rd => rd.ReportUploadedAt)
					.Select(rd => rd.HitStatus)
					.FirstOrDefault()
			});

		if (string.IsNullOrWhiteSpace(sortColumn))
		{
			usersQuery = usersQuery
				.OrderBy(x =>
					x.OrderStatus == OrderStatus.Completed ? 0 :
					x.OrderStatus == OrderStatus.InProgress ? 1 :
					x.OrderStatus == OrderStatus.ApplicationWithdrawn ? 2 :
					x.OrderStatus == OrderStatus.PendingCandidateInfo ? 3 :
					4)
				.ThenByDescending(x => x.OrderCompletedAt)
				.ThenBy(x => x.EmailInvitationID);
		}
		else
		{
			usersQuery = sortColumn switch
			{
				SortColumn.SubjectName => sortDescending
					? usersQuery.OrderByDescending(x => x.FirstName).ThenByDescending(x => x.LastName)
					: usersQuery.OrderBy(x => x.FirstName).ThenBy(x => x.LastName),

				SortColumn.OrderStatus => sortDescending
					? usersQuery.OrderByDescending(x => x.OrderStatus)
					: usersQuery.OrderBy(x => x.OrderStatus),

				SortColumn.OrderCompletedAt => sortDescending
					? usersQuery.OrderByDescending(x => x.OrderCompletedAt).ThenBy(x => x.EmailInvitationID)
					: usersQuery.OrderBy(x => x.OrderCompletedAt).ThenBy(x => x.EmailInvitationID),

				_ => usersQuery.OrderByDescending(x => x.OrderCompletedAt).ThenBy(x => x.EmailInvitationID)
			};
		}

		var totalRecords = await usersQuery.LongCountAsync(cancellationToken);

		var items = await usersQuery
			.Skip((paginationRequest.PageIndex - 1) * paginationRequest.PageSize)
			.Take(paginationRequest.PageSize)
			.Select(x => new ReportListDTO
			{
				EmailInvitationRequestId = x.EmailInvitationID,
				SubjectName = $"{x.FirstName} {x.LastName}".Trim(),
				OrderStatus = x.OrderStatus,
				OrderCompletedAt = x.OrderCompletedAt,
				SelectedPackage = x.SelectPackage,
				Requestor = x.Requestor,
				HitStatus = x.HitStatus
			})
			.ToListAsync(cancellationToken);

		return new PaginatedResult<ReportListDTO>(
			paginationRequest.PageIndex,
			paginationRequest.PageSize,
			totalRecords,
			items);
	}

	public async Task<PaginatedResult<ReportListDTO>> SearchReportsAsync(PaginationRequest paginationRequest, AtsQueryScope scope, string? sortColumn, bool sortDescending, CancellationToken cancellationToken)
	{
		var usersQuery = ApplyQueryScope(
			_dbcontext.EmailInvitationRequests.AsNoTracking(),
			scope)
			.Select(eir => new
			{
				eir.EmailInvitationID,
				eir.FirstName,
				eir.LastName,
				eir.Requestor,
				eir.OrderStatus,
				eir.OrderCompletedAt,
				eir.SelectPackage,
				HitStatus = _dbcontext.ReportDetails
					.Where(rd => rd.EmailInvitationRequestId == eir.EmailInvitationID)
					.OrderByDescending(rd => rd.ReportUploadedAt)
					.Select(rd => rd.HitStatus)
					.FirstOrDefault()
			});

		if (paginationRequest.StartDate.HasValue)
		{
			var start = DateTime.SpecifyKind(
						paginationRequest.StartDate.Value.Date,
						DateTimeKind.Utc);

			usersQuery = usersQuery.Where(x =>
				x.OrderCompletedAt >= start);
		}

		if (paginationRequest.EndDate.HasValue)
		{
			var end = DateTime.SpecifyKind(
				paginationRequest.EndDate.Value.Date.AddDays(1),
				DateTimeKind.Utc);

			usersQuery = usersQuery.Where(x =>
				x.OrderCompletedAt < end);
		}

		if (!string.IsNullOrWhiteSpace(paginationRequest.SearchTerm))
		{
			var search = $"%{paginationRequest.SearchTerm}%";

			usersQuery = usersQuery.Where(x =>
				EF.Functions.ILike((x.FirstName ?? "") + " " + (x.LastName ?? ""), search) ||
				EF.Functions.ILike(x.Requestor ?? string.Empty, search) ||
				EF.Functions.ILike(x.SelectPackage ?? string.Empty, search) ||
				EF.Functions.ILike(x.HitStatus ?? string.Empty, search));
		}

		usersQuery = sortColumn switch
		{
			SortColumn.SubjectName => sortDescending
				? usersQuery.OrderByDescending(x => x.FirstName).ThenByDescending(x => x.LastName)
				: usersQuery.OrderBy(x => x.FirstName).ThenBy(x => x.LastName),
			SortColumn.OrderStatus => sortDescending
				? usersQuery.OrderByDescending(x => x.OrderStatus)
				: usersQuery.OrderBy(x => x.OrderStatus),
			SortColumn.OrderCompletedAt => sortDescending
				? usersQuery.OrderByDescending(x => x.OrderCompletedAt).ThenBy(x => x.EmailInvitationID)
				: usersQuery.OrderBy(x => x.OrderCompletedAt).ThenBy(x => x.EmailInvitationID),
			_ => usersQuery.OrderByDescending(x => x.OrderCompletedAt).ThenBy(x => x.EmailInvitationID)
		};

		var totalRecords = await usersQuery.LongCountAsync(cancellationToken);

		var items = await usersQuery
			.OrderByDescending(x => x.OrderCompletedAt)
			.Skip((paginationRequest.PageIndex - 1) * paginationRequest.PageSize)
			.Take(paginationRequest.PageSize)
			.Select(x => new ReportListDTO
			{
				EmailInvitationRequestId = x.EmailInvitationID,
				SubjectName = $"{x.FirstName} {x.LastName}".Trim(),
				OrderStatus = x.OrderStatus,
				OrderCompletedAt = x.OrderCompletedAt,
				SelectedPackage = x.SelectPackage,
				Requestor = x.Requestor,
				HitStatus = x.HitStatus
			})
			.ToListAsync(cancellationToken);

		return new PaginatedResult<ReportListDTO>(
			paginationRequest.PageIndex,
			paginationRequest.PageSize,
			totalRecords,
			items);
	}

	private static IQueryable<EmailInvitationRequest> ApplyQueryScope(
		IQueryable<EmailInvitationRequest> query,
		AtsQueryScope scope) => scope.Kind switch
		{
			AtsQueryScopeKind.All => query,
			AtsQueryScopeKind.Client => query.Where(invitation => invitation.ClientId == scope.ClientId),
			AtsQueryScopeKind.Clients => query.Where(invitation =>
				invitation.ClientId.HasValue && scope.ClientIds.Contains(invitation.ClientId.Value)),
			AtsQueryScopeKind.ClientRequestor => query.Where(invitation =>
				invitation.ClientId == scope.ClientId
				&& invitation.RequestorId == scope.RequestorId),
			AtsQueryScopeKind.Requestor => query.Where(invitation => invitation.RequestorId == scope.RequestorId),
			_ => query.Where(_ => false)
		};

	public async Task<ReportResultDTO?> GetReportResultByEmailInvitationRequestIdAsync(Guid emailInvitationRequestId, CancellationToken cancellationToken)
	{
		var result = await _dbcontext.EmailInvitationRequests
			.AsNoTracking()
			.Where(eir => eir.EmailInvitationID == emailInvitationRequestId)
			.Select(eir => new
			{
				eir.FirstName,
				eir.LastName,
				eir.OrderStatus,
				eir.SelectPackage,
				eir.FormCompletedAt,
				Personal = new
				{
					eir.PersonalDetails!.ResumeFileName,
					eir.PersonalDetails.ResumeFileKey,
					eir.PersonalDetails.BiometricFileName,
					eir.PersonalDetails.BiometricFileKey,
					eir.PersonalDetails.AdditionalGovtIDFileName,
					eir.PersonalDetails.AdditionalGovtIDFileKey
				},
				Educational = new
				{
					eir.EducationalBackground!.DoctorateDiplomaFileName,
					eir.EducationalBackground!.DoctorateDiplomaFileKey,
					eir.EducationalBackground!.MastersDiplomaFileName,
					eir.EducationalBackground!.MastersDiplomaFileKey,
					eir.EducationalBackground!.BachelorsDiplomaFileName,
					eir.EducationalBackground!.BachelorsDiplomaFileKey,
					eir.EducationalBackground!.SeniorHighSchoolDiplomaFileName,
					eir.EducationalBackground!.SeniorHighSchoolDiplomaFileKey,
					eir.EducationalBackground!.HighSchoolDiplomaFileName,
					eir.EducationalBackground!.HighSchoolDiplomaFileKey,
				},
				Professional = new
				{
					eir.ProfessionalExperiences!.Emp1COEUploadFileName,
					eir.ProfessionalExperiences!.Emp1COEUploadFileKey,
					eir.ProfessionalExperiences!.Emp2COEUploadFileName,
					eir.ProfessionalExperiences!.Emp2COEUploadFileKey,
					eir.ProfessionalExperiences!.Emp3COEUploadFileName,
					eir.ProfessionalExperiences!.Emp3COEUploadFileKey,
					eir.ProfessionalExperiences!.COEUploadFileName,
					eir.ProfessionalExperiences!.COEUploadFileKey
				},
				Signature = new
				{
					eir.SignatureDetails!.ConsentFormFileName,
					eir.SignatureDetails!.ConsentFormFileKey
				},
				LatestReport = eir.ReportDetails!
				.Where(rd =>
					rd.ReportStatus == ReportStatus.SupplementaryReport ||
					rd.ReportStatus == ReportStatus.CompleteFinalReport ||
					rd.ReportStatus == ReportStatus.ClosedFinalReport ||
					rd.ReportStatus == ReportStatus.InitialReport)
				.OrderBy(rd =>
					rd.ReportStatus == ReportStatus.SupplementaryReport ? 0 :
					(rd.ReportStatus == ReportStatus.CompleteFinalReport ||
					 rd.ReportStatus == ReportStatus.ClosedFinalReport) ? 1 : 2)
				.ThenByDescending(rd => rd.ReportUploadedAt)
				.Select(rd => new
				{
					rd.HitStatus,
					rd.ReportFileName,
					rd.ReportFileKey,
					rd.ReportUploadedAt,
					rd.ReportStatus
				})
				.FirstOrDefault()
			})
			.FirstOrDefaultAsync(cancellationToken);

		string? diplomaFileName = result!.Educational?.DoctorateDiplomaFileName
			?? result.Educational?.MastersDiplomaFileName
			?? result.Educational?.BachelorsDiplomaFileName
			?? result.Educational?.SeniorHighSchoolDiplomaFileName
			?? result.Educational?.HighSchoolDiplomaFileName;

		string? diplomaFileKey = result!.Educational?.DoctorateDiplomaFileKey
			?? result.Educational?.MastersDiplomaFileKey
			?? result.Educational?.BachelorsDiplomaFileKey
			?? result.Educational?.SeniorHighSchoolDiplomaFileKey
			?? result.Educational?.HighSchoolDiplomaFileKey;

		string? coeFileName = result.Professional?.Emp1COEUploadFileName
			?? result.Professional?.Emp2COEUploadFileName
			?? result.Professional?.Emp3COEUploadFileName
			?? result.Professional?.COEUploadFileName;

		string? coeFileKey = result.Professional?.Emp1COEUploadFileKey
			?? result.Professional?.Emp2COEUploadFileKey
			?? result.Professional?.Emp3COEUploadFileKey
			?? result.Professional?.COEUploadFileKey;

		return new ReportResultDTO
		{
			SubjectName = $"{result.FirstName} {result.LastName}".Trim(),
			OrderStatus = result.OrderStatus,
			HitStatus = result.LatestReport?.HitStatus,
			SelectedPackage = result.SelectPackage,
			ResumeFileName = result.Personal?.ResumeFileName,
			ResumeFileKey = result.Personal?.ResumeFileKey,
			IdUploadedFileName = result.Personal?.AdditionalGovtIDFileName,
			IdUploadedFileKey = result.Personal?.AdditionalGovtIDFileKey,
			CoeFileName = coeFileName,
			CoeFileKey = coeFileKey,
			DiplomaFileName = diplomaFileName,
			DiplomaFileKey = diplomaFileKey,
			BiometricPhotoFileName = result.Personal?.BiometricFileName,
			BiometricPhotoFileKey = result.Personal?.BiometricFileKey,
			ConsentFormFileName = result.Signature?.ConsentFormFileName,
			ConsentFormFileKey = result.Signature?.ConsentFormFileKey,
			UploadedReportFileName = result.LatestReport?.ReportFileName,
			UploadedReportFileKey = result.LatestReport?.ReportFileKey,
			FilledFormAt = result.FormCompletedAt?.ToString("MMMM dd, yyyy"),
			ReportUploadedAt = result.LatestReport?.ReportUploadedAt.ToString("MMMM dd, yyyy"),
			ReportStatus = result.LatestReport?.ReportStatus?.ToString() ?? "No Report"
		};
	}

	public async Task<List<DownloadDocumentDTO>> GetDownloadDocumentsAsync(
	List<Guid> emailInvitationRequestIds,
	CancellationToken cancellationToken)
	{
		var results = await _dbcontext.EmailInvitationRequests
			.AsNoTracking()
			.Where(eir => emailInvitationRequestIds.Contains(eir.EmailInvitationID))
			.Select(eir => new
			{
				eir.EmailInvitationID,
				SubjectName = (eir.FirstName + " " + eir.LastName).Trim(),

				Personal = new
				{
					eir.PersonalDetails!.ResumeFileName,
					eir.PersonalDetails.ResumeFileKey,

					eir.PersonalDetails.BiometricFileName,
					eir.PersonalDetails.BiometricFileKey,

					eir.PersonalDetails.AdditionalGovtIDFileName,
					eir.PersonalDetails.AdditionalGovtIDFileKey
				},

				Educational = new
				{
					eir.EducationalBackground!.DoctorateDiplomaFileName,
					eir.EducationalBackground.DoctorateDiplomaFileKey,

					eir.EducationalBackground.MastersDiplomaFileName,
					eir.EducationalBackground.MastersDiplomaFileKey,

					eir.EducationalBackground.BachelorsDiplomaFileName,
					eir.EducationalBackground.BachelorsDiplomaFileKey,

					eir.EducationalBackground.SeniorHighSchoolDiplomaFileName,
					eir.EducationalBackground.SeniorHighSchoolDiplomaFileKey,

					eir.EducationalBackground.HighSchoolDiplomaFileName,
					eir.EducationalBackground.HighSchoolDiplomaFileKey
				},

				Professional = new
				{
					eir.ProfessionalExperiences!.Emp1COEUploadFileName,
					eir.ProfessionalExperiences.Emp1COEUploadFileKey,

					eir.ProfessionalExperiences.Emp2COEUploadFileName,
					eir.ProfessionalExperiences.Emp2COEUploadFileKey,

					eir.ProfessionalExperiences.Emp3COEUploadFileName,
					eir.ProfessionalExperiences.Emp3COEUploadFileKey,

					eir.ProfessionalExperiences.COEUploadFileName,
					eir.ProfessionalExperiences.COEUploadFileKey
				},

				Signature = new
				{
					eir.SignatureDetails!.ConsentFormFileName,
					eir.SignatureDetails.ConsentFormFileKey
				},

				LatestReport = eir.ReportDetails!
					.Where(rd =>
						rd.ReportStatus == ReportStatus.SupplementaryReport ||
						rd.ReportStatus == ReportStatus.CompleteFinalReport ||
						rd.ReportStatus == ReportStatus.ClosedFinalReport ||
						rd.ReportStatus == ReportStatus.InitialReport)
					.OrderBy(rd =>
						rd.ReportStatus == ReportStatus.SupplementaryReport ? 0 :
						(rd.ReportStatus == ReportStatus.CompleteFinalReport ||
						 rd.ReportStatus == ReportStatus.ClosedFinalReport) ? 1 : 2)
					.ThenByDescending(rd => rd.ReportUploadedAt)
					.Select(rd => new
					{
						rd.ReportFileName,
						rd.ReportFileKey
					})
					.FirstOrDefault()
			})
			.ToListAsync(cancellationToken);

		var documents = new List<DownloadDocumentDTO>();

		foreach (var result in results)
		{
			void Add(string? fileName, string? fileKey)
			{
				if (!string.IsNullOrWhiteSpace(fileName) &&
					!string.IsNullOrWhiteSpace(fileKey))
				{
					documents.Add(new DownloadDocumentDTO
					{
						EmailInvitationRequestId = result.EmailInvitationID,
						SubjectName = result.SubjectName,
						FileName = fileName,
						FileKey = fileKey
					});
				}
			}

			Add(result.Personal?.ResumeFileName, result.Personal?.ResumeFileKey);

			Add(result.Personal?.BiometricFileName, result.Personal?.BiometricFileKey);

			Add(result.Personal?.AdditionalGovtIDFileName, result.Personal?.AdditionalGovtIDFileKey);

			Add(
				result.Educational?.DoctorateDiplomaFileName
					?? result.Educational?.MastersDiplomaFileName
					?? result.Educational?.BachelorsDiplomaFileName
					?? result.Educational?.SeniorHighSchoolDiplomaFileName
					?? result.Educational?.HighSchoolDiplomaFileName,
				result.Educational?.DoctorateDiplomaFileKey
					?? result.Educational?.MastersDiplomaFileKey
					?? result.Educational?.BachelorsDiplomaFileKey
					?? result.Educational?.SeniorHighSchoolDiplomaFileKey
					?? result.Educational?.HighSchoolDiplomaFileKey);

			Add(
				result.Professional?.Emp1COEUploadFileName
					?? result.Professional?.Emp2COEUploadFileName
					?? result.Professional?.Emp3COEUploadFileName
					?? result.Professional?.COEUploadFileName,
				result.Professional?.Emp1COEUploadFileKey
					?? result.Professional?.Emp2COEUploadFileKey
					?? result.Professional?.Emp3COEUploadFileKey
					?? result.Professional?.COEUploadFileKey);

			Add(result.Signature?.ConsentFormFileName, result.Signature?.ConsentFormFileKey);

			Add(result.LatestReport?.ReportFileName, result.LatestReport?.ReportFileKey);
		}

		return documents;
	}

	public async Task<EmailInvitationRequest> GetEmailInvitationRequestByIdAsync(Guid emailInvitationId, CancellationToken cancellationToken)
	{
		return await _dbcontext.EmailInvitationRequests
			.AsNoTracking()
			.FirstOrDefaultAsync(eir => eir.EmailInvitationID == emailInvitationId, cancellationToken) ?? new EmailInvitationRequest();
	}

	public async Task<List<EmailInvitationRequest>> GetEmailInvitationRequestsNeedingProjectionAsync(CancellationToken cancellationToken)
	{
		return await _dbcontext.EmailInvitationRequests
			.Where(x => x.NeedsProjection)
			.Include(x => x.PersonalDetails)
			.Include(x => x.AddressDetails)
			.Include(x => x.EducationalBackground)
			.Include(x => x.LicensesDetails)
			.Include(x => x.ProfessionalExperiences)
			.Include(x => x.ReferenceDetails)
			.Include(x => x.SignatureDetails)
			.ToListAsync(cancellationToken);
	}

	public async Task<ApplicantSearchProjection?> GetApplicantSearchProjectionByIdAsync(Guid emailInvitationRequestId, CancellationToken cancellationToken)
	{
		return await _dbcontext.ApplicantSearchProjections
			.FirstOrDefaultAsync(x => x.EmailInvitationRequestId == emailInvitationRequestId, cancellationToken);
	}

	public async Task<bool> AddApplicantSearchProjectionAsync(ApplicantSearchProjection projection, CancellationToken cancellationToken)
	{
		await _dbcontext.ApplicantSearchProjections.AddAsync(projection, cancellationToken);
		return true;
	}

	public async Task<bool> ResendApplicationFormAsync(Guid emailInvitationId, string hashToken, DateTime hashTokenExpiration, CancellationToken cancellationToken)
	{
		await _dbcontext.EmailInvitationRequests
			.Where(eir => eir.EmailInvitationID == emailInvitationId)
			.ExecuteUpdateAsync(setters => setters
				.SetProperty(eir => eir.HashToken, hashToken)
				.SetProperty(eir => eir.HashTokenCreatedAt, DateTime.UtcNow)
				.SetProperty(eir => eir.HashTokenExpiration, hashTokenExpiration)
				.SetProperty(eir => eir.OrderStatus, OrderStatus.PendingCandidateInfo)
				.SetProperty(eir => eir.ApplicationFormStatus, ApplicationFormStatus.Pending),
				cancellationToken);

		return true;
	}
}
