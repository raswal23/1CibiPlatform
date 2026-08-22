namespace ATS.Data.Repository;

public partial class ATSRepository
{
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

	// Keyset over the fixed reports ordering (Rank ASC, OrderCompletedAt DESC,
	// EmailInvitationID ASC). Pure query — the service decodes the cursor and mints
	// the next one. The seek applies when afterRank and afterId are present; a NULL
	// afterCompletedAt legitimately means the last row's sort key was NULL (the
	// NULLS FIRST branch of the DESC ordering).
	public async Task<List<ReportRowDTO>> GetReportsPageAsync(
		int? afterRank,
		DateTime? afterCompletedAt,
		Guid? afterId,
		int take,
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredRequestorId,
		CancellationToken cancellationToken)
	{
		var pageQuery = BuildReportRowsQuery(authorizedClientIds, requiredRequestorId);
		if (afterRank.HasValue && afterId.HasValue)
			pageQuery = ApplyReportsSeek(pageQuery, afterRank.Value, afterCompletedAt, afterId.Value);

		return await ApplyReportsOrder(pageQuery).Take(take).ToListAsync(cancellationToken);
	}

	public async Task<List<ReportRowDTO>> SearchReportsPageAsync(
		int? afterRank,
		DateTime? afterCompletedAt,
		Guid? afterId,
		int take,
		string? searchTerm,
		DateTime? startDate,
		DateTime? endDate,
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredRequestorId,
		CancellationToken cancellationToken)
	{
		var pageQuery = BuildSearchReportRowsQuery(searchTerm, startDate, endDate, authorizedClientIds, requiredRequestorId);
		if (afterRank.HasValue && afterId.HasValue)
			pageQuery = ApplyReportsSeek(pageQuery, afterRank.Value, afterCompletedAt, afterId.Value);

		return await ApplyReportsOrder(pageQuery).Take(take).ToListAsync(cancellationToken);
	}

	public Task<long> CountReportsAsync(
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredRequestorId,
		CancellationToken cancellationToken) =>
		BuildReportRowsQuery(authorizedClientIds, requiredRequestorId).LongCountAsync(cancellationToken);

	public Task<long> CountSearchReportsAsync(
		string? searchTerm,
		DateTime? startDate,
		DateTime? endDate,
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredRequestorId,
		CancellationToken cancellationToken) =>
		BuildSearchReportRowsQuery(searchTerm, startDate, endDate, authorizedClientIds, requiredRequestorId)
			.LongCountAsync(cancellationToken);

	private IQueryable<ReportRowDTO> BuildReportRowsQuery(
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredRequestorId)
	{
		return _dbcontext.EmailInvitationRequests
			.AsNoTracking()
			.Where(eir => (authorizedClientIds == null
					|| (eir.ClientId.HasValue && authorizedClientIds.Contains(eir.ClientId.Value)))
				&& (!requiredRequestorId.HasValue
					|| eir.RequestorId == requiredRequestorId.Value))
			.Select(eir => new ReportRowDTO
			{
				EmailInvitationID = eir.EmailInvitationID,
				FirstName = eir.FirstName,
				LastName = eir.LastName,
				Requestor = eir.Requestor,
				OrderStatus = eir.OrderStatus,
				OrderCompletedAt = eir.OrderCompletedAt,
				SelectPackage = eir.SelectPackage,
				HitStatus = _dbcontext.ReportDetails
					.Where(rd => rd.EmailInvitationRequestId == eir.EmailInvitationID)
					.OrderByDescending(rd => rd.ReportUploadedAt)
					.Select(rd => rd.HitStatus)
					.FirstOrDefault(),
				Rank = eir.OrderStatus == OrderStatus.Completed ? 0 :
					eir.OrderStatus == OrderStatus.InProgress ? 1 :
					eir.OrderStatus == OrderStatus.ApplicationWithdrawn ? 2 :
					eir.OrderStatus == OrderStatus.PendingCandidateInfo ? 3 :
					4
			});
	}

	private IQueryable<ReportRowDTO> BuildSearchReportRowsQuery(
		string? searchTerm,
		DateTime? startDate,
		DateTime? endDate,
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredRequestorId)
	{
		var usersQuery = BuildReportRowsQuery(authorizedClientIds, requiredRequestorId);

		if (startDate.HasValue)
		{
			var start = DateTime.SpecifyKind(startDate.Value.Date, DateTimeKind.Utc);
			usersQuery = usersQuery.Where(x => x.OrderCompletedAt >= start);
		}

		if (endDate.HasValue)
		{
			var end = DateTime.SpecifyKind(endDate.Value.Date.AddDays(1), DateTimeKind.Utc);
			usersQuery = usersQuery.Where(x => x.OrderCompletedAt < end);
		}

		if (!string.IsNullOrWhiteSpace(searchTerm))
		{
			var search = $"%{searchTerm}%";
			usersQuery = usersQuery.Where(x =>
				EF.Functions.ILike((x.FirstName ?? "") + " " + (x.LastName ?? ""), search) ||
				EF.Functions.ILike(x.Requestor ?? string.Empty, search) ||
				EF.Functions.ILike(x.SelectPackage ?? string.Empty, search) ||
				EF.Functions.ILike(x.HitStatus ?? string.Empty, search));
		}

		return usersQuery;
	}

	// The single reports ordering: status precedence first, newest completions next,
	// unique id as the tiebreaker. Postgres sorts DESC as NULLS FIRST, which the
	// seek predicate in ApplyReportsSeek mirrors exactly.
	private static IQueryable<ReportRowDTO> ApplyReportsOrder(IQueryable<ReportRowDTO> pageQuery) => pageQuery
		.OrderBy(x => x.Rank).ThenByDescending(x => x.OrderCompletedAt).ThenBy(x => x.EmailInvitationID);

	// NULL-aware seek predicate for the fixed (Rank ASC, OrderCompletedAt DESC
	// NULLS FIRST, Id ASC) ordering. A NULL afterCompletedAt means the last row's
	// sort key was NULL, selecting the NULLS FIRST branch.
	private static IQueryable<ReportRowDTO> ApplyReportsSeek(
		IQueryable<ReportRowDTO> query, int afterRank, DateTime? afterCompletedAt, Guid afterId)
	{
		if (afterCompletedAt is null)
			return query.Where(x => x.Rank > afterRank
				|| (x.Rank == afterRank && ((x.OrderCompletedAt == null && x.EmailInvitationID.CompareTo(afterId) > 0)
					|| x.OrderCompletedAt != null)));

		return query.Where(x => x.Rank > afterRank
			|| (x.Rank == afterRank && x.OrderCompletedAt != null
				&& (x.OrderCompletedAt < afterCompletedAt
					|| (x.OrderCompletedAt == afterCompletedAt && x.EmailInvitationID.CompareTo(afterId) > 0))));
	}

	public async Task<ReportResultDTO?> GetReportResultByEmailInvitationRequestIdAsync(
		Guid emailInvitationRequestId,
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredRequestorId,
		CancellationToken cancellationToken)
	{
		var result = await _dbcontext.EmailInvitationRequests
			.AsNoTracking()
			.Where(eir => eir.EmailInvitationID == emailInvitationRequestId)
			.Where(eir => (authorizedClientIds == null
					|| (eir.ClientId.HasValue && authorizedClientIds.Contains(eir.ClientId.Value)))
				&& (!requiredRequestorId.HasValue
					|| eir.RequestorId == requiredRequestorId.Value))
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

		// An unknown id - or one outside the caller's scope - returns null here. The
		// null-forgiving dereference below used to turn that into a 500 before the
		// service's own null check could run.
		if (result is null)
			return null;

		string? diplomaFileName = result.Educational?.DoctorateDiplomaFileName
			?? result.Educational?.MastersDiplomaFileName
			?? result.Educational?.BachelorsDiplomaFileName
			?? result.Educational?.SeniorHighSchoolDiplomaFileName
			?? result.Educational?.HighSchoolDiplomaFileName;

		string? diplomaFileKey = result.Educational?.DoctorateDiplomaFileKey
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

	// The scope predicates are applied inside the query rather than checked afterwards,
	// so an id outside the caller's scope simply yields no rows - the caller cannot tell
	// an unauthorized order from a non-existent one.
	public async Task<List<DownloadDocumentDTO>> GetDownloadDocumentsAsync(
	List<Guid> emailInvitationRequestIds,
	IReadOnlyCollection<int>? authorizedClientIds,
	Guid? requiredRequestorId,
	CancellationToken cancellationToken)
	{
		var results = await _dbcontext.EmailInvitationRequests
			.AsNoTracking()
			.Where(eir => emailInvitationRequestIds.Contains(eir.EmailInvitationID))
			.Where(eir => (authorizedClientIds == null
					|| (eir.ClientId.HasValue && authorizedClientIds.Contains(eir.ClientId.Value)))
				&& (!requiredRequestorId.HasValue
					|| eir.RequestorId == requiredRequestorId.Value))
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
}
