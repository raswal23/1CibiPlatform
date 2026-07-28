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
			.SetProperty(x => x.EmailSentAt, x => DateTime.UtcNow));

		return true;
	}

	public async Task<bool> UpdateBulkEmailInvitationRequestForNotSentEmailAsync(List<EmailInvitationRequest> emailInvitationRequests)
	{
		var ids = emailInvitationRequests.Select(x => x.EmailInvitationID).ToList();

		await _dbcontext.EmailInvitationRequests
			.Where(x => ids.Contains(x.EmailInvitationID))
			.ExecuteUpdateAsync(setters => setters
			.SetProperty(x => x.EmailSentStatus, x => EmailStatus.Error));

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

	public async Task<PaginatedResult<EmailInvitationRequestListDTO>> GetWithdrawnEmailInvitationRequestsAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
	{
		var usersQuery = _dbcontext.EmailInvitationRequests
			.AsNoTracking()
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
						OrderStatus = eir.OrderStatus,
					})
					.ToListAsync(cancellationToken);

		return new PaginatedResult<EmailInvitationRequestListDTO>(
			paginationRequest.PageIndex,
			paginationRequest.PageSize,
			totalRecords,
			items);
	}

	public async Task<PaginatedResult<EmailInvitationRequestListDTO>> SearchWithdrawnEmailInvitationRequestsAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
	{
		var usersQuery = _dbcontext.EmailInvitationRequests
							.AsNoTracking()
							.Where(eir => eir.OrderStatus == OrderStatus.ApplicationWithdrawn)
							.Where(eir =>
								EF.Functions.ILike(eir.FirstName!, $"%{paginationRequest.SearchTerm}%") ||
								EF.Functions.ILike(eir.MiddleInitial ?? string.Empty, $"%{paginationRequest.SearchTerm}%") ||
								EF.Functions.ILike(eir.LastName!, $"%{paginationRequest.SearchTerm}%") ||
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

	public async Task<PaginatedResult<DisputeOrderListDTO>> GetDisputeOrdersAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
	{
		var disputeWindowStart = DateTime.UtcNow.AddDays(-30);

		var usersQuery =  _dbcontext.EmailInvitationRequests
			.AsNoTracking()
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

	public async Task<PaginatedResult<DisputeOrderListDTO>> SearchDisputeOrdersAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
	{
		var disputeWindowStart = DateTime.UtcNow.AddDays(-30);

		var usersQuery = _dbcontext.EmailInvitationRequests
			.AsNoTracking()
			.Where(eir =>
				(eir.OrderStatus == OrderStatus.Completed && eir.OrderCreatedAt.HasValue && eir.OrderCompletedAt!.Value >= disputeWindowStart) &&
			   (EF.Functions.ILike(eir.FirstName!, $"%{paginationRequest.SearchTerm}%") ||
				EF.Functions.ILike(eir.LastName!, $"%{paginationRequest.SearchTerm}%") ||
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

   public async Task<PaginatedResult<ReportListDTO>> GetReportsAsync(PaginationRequest paginationRequest, string? sortColumn, bool sortDescending, CancellationToken cancellationToken)
	{
		var usersQuery = _dbcontext.EmailInvitationRequests
			.AsNoTracking()
			.Select(eir => new
			{
				eir.EmailInvitationID,
				eir.FirstName,
				eir.LastName,
				eir.OrderStatus,
				eir.OrderCompletedAt,
				eir.SelectPackage,
				HitStatus = _dbcontext.ReportDetails
					.Where(rd => rd.EmailInvitationRequestId == eir.EmailInvitationID)
					.OrderByDescending(rd => rd.ReportUploadedAt)
					.Select(rd => rd.HitStatus)
					.FirstOrDefault()
			});

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
			.Skip((paginationRequest.PageIndex - 1) * paginationRequest.PageSize)
			.Take(paginationRequest.PageSize)
			.Select(x => new ReportListDTO
			{
				EmailInvitationRequestId = x.EmailInvitationID,
				SubjectName = $"{x.FirstName} {x.LastName}".Trim(),
				OrderStatus = x.OrderStatus,
				OrderCompletedAt = x.OrderCompletedAt,
				SelectedPackage = x.SelectPackage,
				HitStatus = x.HitStatus
			})
			.ToListAsync(cancellationToken);

		return new PaginatedResult<ReportListDTO>(
			paginationRequest.PageIndex,
			paginationRequest.PageSize,
			totalRecords,
			items);
	}

   public async Task<PaginatedResult<ReportListDTO>> SearchReportsAsync(PaginationRequest paginationRequest, string? sortColumn, bool sortDescending, CancellationToken cancellationToken)
	{
		var usersQuery = _dbcontext.EmailInvitationRequests
			.AsNoTracking()
			.Select(eir => new
			{
				eir.EmailInvitationID,
				eir.FirstName,
				eir.LastName,
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
			.Skip((paginationRequest.PageIndex - 1) * paginationRequest.PageSize)
			.Take(paginationRequest.PageSize)
			.Select(x => new ReportListDTO
			{
				EmailInvitationRequestId = x.EmailInvitationID,
				SubjectName = $"{x.FirstName} {x.LastName}".Trim(),
				OrderStatus = x.OrderStatus,
				OrderCompletedAt = x.OrderCompletedAt,
				SelectedPackage = x.SelectPackage,
				HitStatus = x.HitStatus
			})
			.ToListAsync(cancellationToken);

		return new PaginatedResult<ReportListDTO>(
			paginationRequest.PageIndex,
			paginationRequest.PageSize,
			totalRecords,
			items);
	}

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
					rd.ReportUploadedAt
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
			ReportUploadedAt = result.LatestReport?.ReportUploadedAt.ToString("MMMM dd, yyyy")
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