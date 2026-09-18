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

	// Both the unfiltered and searched report lists use the same fixed ordering:
	// OrderCreatedAt descending. EmailInvitationID is only a deterministic keyset
	// tiebreaker for orders created at the same instant.
	public async Task<List<ReportRowDTO>> GetReportsPageAsync(
		DateTime? afterCreatedAt,
		Guid? afterId,
		int take,
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredRequestorId,
		CancellationToken cancellationToken)
	{
		var pageQuery = BuildReportRowsQuery(authorizedClientIds, requiredRequestorId);
		if (afterId.HasValue)
			pageQuery = ApplyReportsSeek(pageQuery, afterCreatedAt, afterId.Value);

		return await ApplyReportsOrder(pageQuery).Take(take).ToListAsync(cancellationToken);
	}

	public async Task<List<ReportRowDTO>> SearchReportsPageAsync(
		DateTime? afterCreatedAt,
		Guid? afterId,
		int take,
		string? searchTerm,
		DateTime? startDate,
		DateTime? endDate,
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredRequestorId,
		CancellationToken cancellationToken)
	{
		var pageQuery = BuildSearchReportRowsQuery(
			searchTerm,
			startDate,
			endDate,
			authorizedClientIds,
			requiredRequestorId);

		if (afterId.HasValue)
			pageQuery = ApplyReportsSeek(pageQuery, afterCreatedAt, afterId.Value);

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
			.Where(eir => (authorizedClientIds == null || (eir.ClientId.HasValue && authorizedClientIds.Contains(eir.ClientId.Value)))
				&& (!requiredRequestorId.HasValue || eir.RequestorId == requiredRequestorId.Value))
			.Select(eir => new ReportRowDTO
			{
				EmailInvitationID = eir.EmailInvitationID,
				FirstName = eir.FirstName,
				MiddleInitial = eir.MiddleInitial,
				LastName = eir.LastName,
				Requestor = eir.Requestor,
				TicketNumber = eir.TicketNumber,
				OrderStatus = eir.OrderStatus,
				OrderCreatedAt = eir.OrderCreatedAt,
				OrderCompletedAt = eir.OrderCompletedAt,
				SelectPackage = eir.SelectPackage,
				RushNormal = eir.RushNormal,
				HitStatus = _dbcontext.ReportDetails
					.Where(rd => rd.EmailInvitationRequestId == eir.EmailInvitationID)
					.OrderByDescending(rd => rd.ReportUploadedAt)
					.Select(rd => rd.HitStatus)
					.FirstOrDefault(),

				// The follow-up inputs, carried so the service can work out how many
				// reminders are left. A correlated subquery rather than a join, matching how
				// HitStatus above is read: the shape of this query is one row per invitation
				// and a join to PackageDetails would risk changing that.
				PackageFollowUpEmail = _dbcontext.PackageDetails
					.Where(pd => pd.PackageId == eir.PackageId)
					.Select(pd => pd.FollowUpEmail)
					.FirstOrDefault(),
				ChasesCandidate = eir.AutoChasing == true,
				ApplicationFormStatus = eir.ApplicationFormStatus,
				FollowUpSentCount = eir.FollowUpSentCount
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
				EF.Functions.ILike(x.TicketNumber ?? string.Empty, search) ||
				EF.Functions.ILike(x.Requestor ?? string.Empty, search) ||
				EF.Functions.ILike(x.OrderStatus ?? string.Empty, search) ||
				EF.Functions.ILike(x.SelectPackage ?? string.Empty, search) ||
				EF.Functions.ILike(x.HitStatus ?? string.Empty, search));
		}

		return usersQuery;
	}

	// OrderCreatedAt is the only business sort key. The id provides stable paging
	// when two rows have the same creation timestamp.
	private static IQueryable<ReportRowDTO> ApplyReportsOrder(IQueryable<ReportRowDTO> pageQuery) => pageQuery
		.OrderByDescending(x => x.OrderCreatedAt).ThenBy(x => x.EmailInvitationID);

	// PostgreSQL places nulls first for DESC. Mirror that behavior in the seek so
	// legacy rows without a creation timestamp remain pageable.
	private static IQueryable<ReportRowDTO> ApplyReportsSeek(
		IQueryable<ReportRowDTO> query, DateTime? afterCreatedAt, Guid afterId)
	{
		if (afterCreatedAt is null)
			return query.Where(x =>
				(x.OrderCreatedAt == null && x.EmailInvitationID.CompareTo(afterId) > 0)
				|| x.OrderCreatedAt != null);

		return query.Where(x => x.OrderCreatedAt != null
			&& (x.OrderCreatedAt < afterCreatedAt
				|| (x.OrderCreatedAt == afterCreatedAt && x.EmailInvitationID.CompareTo(afterId) > 0)));
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
					eir.PersonalDetails.AdditionalGovtIDFileKey,
					eir.PersonalDetails.NBIClearanceFileName,
					eir.PersonalDetails.NBIClearanceFileKey
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
			NbiClearanceFileName = result.Personal?.NBIClearanceFileName,
			NbiClearanceFileKey = result.Personal?.NBIClearanceFileKey,
			CoeFileName = coeFileName,
			CoeFileKey = coeFileKey,
			Coe1FileName = result.Professional?.Emp1COEUploadFileName,
			Coe1FileKey = result.Professional?.Emp1COEUploadFileKey,
			Coe2FileName = result.Professional?.Emp2COEUploadFileName,
			Coe2FileKey = result.Professional?.Emp2COEUploadFileKey,
			Coe3FileName = result.Professional?.Emp3COEUploadFileName,
			Coe3FileKey = result.Professional?.Emp3COEUploadFileKey,
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
					eir.PersonalDetails.AdditionalGovtIDFileKey,

					eir.PersonalDetails.NBIClearanceFileName,
					eir.PersonalDetails.NBIClearanceFileKey
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

				License = new
				{
					eir.LicensesDetails!.LicenseUploadFileName,
					eir.LicensesDetails.LicenseUploadFileKey
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
			void Add(string? fileName, string? fileKey, string documentType)
			{
				if (!string.IsNullOrWhiteSpace(fileName) &&
					!string.IsNullOrWhiteSpace(fileKey))
				{
					documents.Add(new DownloadDocumentDTO
					{
						EmailInvitationRequestId = result.EmailInvitationID,
						SubjectName = result.SubjectName,
						FileName = fileName,
						FileKey = fileKey,
						DocumentType = documentType
					});
				}
			}

			Add(result.Personal?.ResumeFileName, result.Personal?.ResumeFileKey, AtsDocumentTypes.Resume);

			Add(result.Personal?.BiometricFileName, result.Personal?.BiometricFileKey, AtsDocumentTypes.BiometricPhoto);

			Add(result.Personal?.AdditionalGovtIDFileName, result.Personal?.AdditionalGovtIDFileKey, AtsDocumentTypes.GovernmentId);

			Add(result.Personal?.NBIClearanceFileName, result.Personal?.NBIClearanceFileKey, AtsDocumentTypes.NbiClearance);

			// Only the highest diploma on record goes into the compiled file.
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
					?? result.Educational?.HighSchoolDiplomaFileKey,
				AtsDocumentTypes.Diploma);

			// The COEs used to share that coalesce shape, which silently dropped
			// employers 2 and 3 whenever employer 1 had a COE; every COE is included.
			Add(result.Professional?.Emp1COEUploadFileName, result.Professional?.Emp1COEUploadFileKey, AtsDocumentTypes.Coe1);
			Add(result.Professional?.Emp2COEUploadFileName, result.Professional?.Emp2COEUploadFileKey, AtsDocumentTypes.Coe2);
			Add(result.Professional?.Emp3COEUploadFileName, result.Professional?.Emp3COEUploadFileKey, AtsDocumentTypes.Coe3);
			Add(result.Professional?.COEUploadFileName, result.Professional?.COEUploadFileKey, AtsDocumentTypes.Coe);

			Add(result.License?.LicenseUploadFileName, result.License?.LicenseUploadFileKey, AtsDocumentTypes.License);

			Add(result.Signature?.ConsentFormFileName, result.Signature?.ConsentFormFileKey, AtsDocumentTypes.ConsentForm);

			Add(result.LatestReport?.ReportFileName, result.LatestReport?.ReportFileKey, AtsDocumentTypes.Report);
		}

		return documents;
	}

	public async Task<ApplicationFormPreviewDTO?> GetApplicationFormPreviewAsync(
		Guid emailInvitationRequestId,
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredRequestorId,
		CancellationToken cancellationToken)
	{
		var eir = await _dbcontext.EmailInvitationRequests
			.AsNoTracking()
			.Include(x => x.PersonalDetails)
			.Include(x => x.AddressDetails)
			.Include(x => x.EducationalBackground)
			.Include(x => x.LicensesDetails)
			.Include(x => x.ProfessionalExperiences)
			.Include(x => x.ReferenceDetails)
			.Include(x => x.SignatureDetails)
			.Where(x => x.EmailInvitationID == emailInvitationRequestId)
			.Where(x => (authorizedClientIds == null
					|| (x.ClientId.HasValue && authorizedClientIds.Contains(x.ClientId.Value)))
				&& (!requiredRequestorId.HasValue
					|| x.RequestorId == requiredRequestorId.Value))
			.FirstOrDefaultAsync(cancellationToken);

		if (eir is null)
			return null;

		var preview = new ApplicationFormPreviewDTO
		{
			SubjectName = $"{eir.FirstName} {eir.LastName}".Trim(),
			FilledFormAt = eir.FormCompletedAt?.ToString("MMMM dd, yyyy")
		};

		if (eir.PersonalDetails is { } p)
		{
			// A stored biometric capture only comes from the PhilSys liveness flow.
			preview.PhilSysVerified = !string.IsNullOrWhiteSpace(p.BiometricFileKey);

			preview.Personal = new PersonalPreviewDTO
			{
				PositionAppliedFor = p.PositionAppliedFor,
				FirstName = p.FirstName,
				MiddleName = p.MiddleName,
				LastName = p.LastName,
				Suffix = p.Suffix,
				Sex = p.Sex,
				DateOfBirth = p.DOB?.ToString("MMMM dd, yyyy"),
				MaritalStatus = p.MaritalStatus,
				Nationality = p.Nationality,
				MobileNumber = p.MobileNumber,
				TelephoneNumber = p.TelephoneNumber,
				EmailAddress = p.EmailAddress,
				EmailAlternative = p.EmailAlternative,
				SSS = p.SSS,
				TIN = p.TIN,
				GovtIdFileName = p.AdditionalGovtIDFileName,
				NbiClearanceFileName = p.NBIClearanceFileName,
				ResumeFileName = p.ResumeFileName
			};
		}

		if (eir.AddressDetails is { } a)
		{
			preview.Address = new AddressPreviewDTO
			{
				CurrentAddress = a.CurrentAddress,
				CurrentCity = a.CurrentCity,
				CurrentProvince = a.CurrentProvince,
				CurrentCountry = a.CurrentCountry,
				CurrentPostalCode = a.CurrentPostalCode,
				CurrentTypeOfOwnership = a.CurrentTypeOfOwnership,
				PermanentAddress = a.PermanentAddress,
				PermanentCity = a.PermanentCity,
				PermanentProvince = a.PermanentProvince,
				PermanentCountry = a.PermanentCountry,
				PermanentPostalCode = a.PermanentPostalCode
			};
		}

		if (eir.EducationalBackground is { } e)
		{
			// Surface the highest level the applicant filled in, mirroring how the
			// form stores one level per tier.
			preview.Education = new EducationPreviewDTO
			{
				HighestEducationalAttainment = e.HighestEducationalAttainment,
				SchoolName = e.PhDSchoolName
					?? e.MastersSchoolName
					?? e.BachelorsSchoolName
					?? e.CollegeSchoolName
					?? e.SeniorHighSchoolName
					?? e.HighSchoolName,
				Degree = e.DoctorateDegree
					?? e.MastersDegree
					?? e.BachelorsDegree
					?? e.CollegeDegree,
				GraduationDate = (e.DoctorateGraduationDate
					?? e.MastersGraduationDate
					?? e.BachelorsGraduationDate
					?? e.CollegeGraduationDate
					?? e.SeniorHighSchoolGraduationDate
					?? e.HighSchoolGraduationDate)?.ToString("MMMM dd, yyyy"),
				DiplomaFileName = e.DoctorateDiplomaFileName
					?? e.MastersDiplomaFileName
					?? e.BachelorsDiplomaFileName
					?? e.CollegeDiplomaFileName
					?? e.SeniorHighSchoolDiplomaFileName
					?? e.HighSchoolDiplomaFileName
			};
		}

		if (eir.LicensesDetails is { } l && !string.IsNullOrWhiteSpace(l.LicenseName))
		{
			preview.License = new LicensePreviewDTO
			{
				LicenseName = l.LicenseName,
				LicenseNumber = l.LicenseNumber,
				LicenseExpiryDate = l.LicenseExpiryDate?.ToString("MMMM dd, yyyy"),
				LicenseFileName = l.LicenseUploadFileName
			};
		}

		if (eir.ProfessionalExperiences is { } pe)
		{
			void AddEmployer(string? company, string? jobTitle, string? address, DateOnly? start, DateOnly? end,
				string? currentlyEmployed, string? permissionToContact, string? reason, string? supName,
				string? supContact, string? supEmail, string? coeFileName)
			{
				if (string.IsNullOrWhiteSpace(company))
					return;

				preview.Employers.Add(new EmployerPreviewDTO
				{
					CompanyName = company,
					JobTitle = jobTitle,
					CompanyAddress = address,
					StartDate = start?.ToString("MMMM dd, yyyy"),
					EndDate = end?.ToString("MMMM dd, yyyy"),
					CurrentlyEmployed = currentlyEmployed,
					PermissionToContact = permissionToContact,
					ReasonForLeaving = reason,
					SupervisorName = supName,
					SupervisorContactNumber = supContact,
					SupervisorEmail = supEmail,
					CoeFileName = coeFileName
				});
			}

			AddEmployer(pe.Emp1CompanyName, pe.Emp1JobTitle, pe.Emp1CompanyAddress, pe.Emp1StartDate, pe.Emp1EndDate,
				pe.Emp1CurrentlyEmployed, pe.Emp1PermissionToContact, pe.Emp1ReasonForLeaving, pe.Emp1SupervisorName,
				pe.Emp1SupervisorContactNumber, pe.Emp1SupervisorEmail, pe.Emp1COEUploadFileName ?? pe.COEUploadFileName);
			AddEmployer(pe.Emp2CompanyName, pe.Emp2JobTitle, pe.Emp2CompanyAddress, pe.Emp2StartDate, pe.Emp2EndDate,
				pe.Emp2CurrentlyEmployed, pe.Emp2PermissionToContact, pe.Emp2ReasonForLeaving, pe.Emp2SupervisorName,
				pe.Emp2SupervisorContactNumber, pe.Emp2SupervisorEmail, pe.Emp2COEUploadFileName);
			AddEmployer(pe.Emp3CompanyName, pe.Emp3JobTitle, pe.Emp3CompanyAddress, pe.Emp3StartDate, pe.Emp3EndDate,
				pe.Emp3CurrentlyEmployed, pe.Emp3PermissionToContact, pe.Emp3ReasonForLeaving, pe.Emp3SupervisorName,
				pe.Emp3SupervisorContactNumber, pe.Emp3SupervisorEmail, pe.Emp3COEUploadFileName);
		}

		if (eir.ReferenceDetails is { } r)
		{
			void AddReference(string? fullName, string? relationship, string? company, string? email,
				string? contact, string? mode, DateTime? bestTime)
			{
				if (string.IsNullOrWhiteSpace(fullName))
					return;

				preview.References.Add(new ReferencePreviewDTO
				{
					FullName = fullName,
					ProfessionalRelationship = relationship,
					AffiliatedCompany = company,
					Email = email,
					ContactNumber = contact,
					ModeOfContact = mode,
					BestTimeToContact = bestTime?.ToString("MMMM dd, yyyy h:mm tt")
				});
			}

			AddReference(r.Ref1FullName, r.Ref1ProfessionalRelationship, r.Ref1AffiliatedCompany, r.Ref1Email,
				r.Ref1ContactNumber, r.Ref1ModeOfContact, r.Ref1BestTimeToContact);
			AddReference(r.Ref2FullName, r.Ref2ProfessionalRelationship, r.Ref2AffiliatedCompany, r.Ref2Email,
				r.Ref2ContactNumber, r.Ref2ModeOfContact, r.Ref2BestTimeToContact);
			AddReference(r.Ref3FullName, r.Ref3ProfessionalRelationship, r.Ref3AffiliatedCompany, r.Ref3Email,
				r.Ref3ContactNumber, r.Ref3ModeOfContact, r.Ref3BestTimeToContact);
		}

		if (eir.SignatureDetails is { } s)
		{
			preview.Signature = new SignaturePreviewDTO
			{
				SignerName = s.SignerName,
				SignatureDate = s.SignatureDate?.ToString("MMMM dd, yyyy"),
				ConsentFormFileName = s.ConsentFormFileName
			};
		}

		return preview;
	}
}
