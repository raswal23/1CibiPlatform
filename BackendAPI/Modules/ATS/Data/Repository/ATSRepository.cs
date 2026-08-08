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

	public async Task<PaginatedResult<PackageDetailsDTO>> GetPackagesAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
	{
		var packagesQuery = _dbcontext.PackageDetails
			.AsNoTracking()
			.OrderBy(p => p.PackageName);

		var totalRecords = await packagesQuery.CountAsync(cancellationToken);

		var items = await packagesQuery
			.Skip((paginationRequest.PageIndex - 1) * paginationRequest.PageSize)
			.Take(paginationRequest.PageSize)
			.Select(p => new PackageDetailsDTO
			{
				PackageId = p.PackageId,
				PackageName = p.PackageName,
				PackageDescription = p.PackageDescription,
				IsActive = p.IsActive,
				FollowUpEmail = p.FollowUpEmail,
				CreatedAt = p.CreatedAt,
				UpdatedAt = p.UpdatedAt
			})
			.ToListAsync(cancellationToken);

		return new PaginatedResult<PackageDetailsDTO>(
			paginationRequest.PageIndex,
			paginationRequest.PageSize,
			totalRecords,
			items);
	}

	public async Task<PaginatedResult<PackageDetailsDTO>> SearchPackagesAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
	{
		var packagesQuery = _dbcontext.PackageDetails
			.AsNoTracking()
			.Where(p =>
				EF.Functions.ILike(p.PackageName, $"%{paginationRequest.SearchTerm}%") ||
				EF.Functions.ILike(p.PackageDescription, $"%{paginationRequest.SearchTerm}%"));

		var totalRecords = await packagesQuery.CountAsync(cancellationToken);

		var items = await packagesQuery
			.OrderBy(p => p.PackageName)
			.Skip((paginationRequest.PageIndex - 1) * paginationRequest.PageSize)
			.Take(paginationRequest.PageSize)
			.Select(p => new PackageDetailsDTO
			{
				PackageId = p.PackageId,
				PackageName = p.PackageName,
				PackageDescription = p.PackageDescription,
				IsActive = p.IsActive,
				FollowUpEmail = p.FollowUpEmail,
				CreatedAt = p.CreatedAt,
				UpdatedAt = p.UpdatedAt
			})
			.ToListAsync(cancellationToken);

		return new PaginatedResult<PackageDetailsDTO>(
			paginationRequest.PageIndex,
			paginationRequest.PageSize,
			totalRecords,
			items);
	}

	public async Task<bool> AddPackageAsync(AddPackageDTO packageDTO, CancellationToken cancellationToken)
	{
		var timestamp = DateTime.UtcNow;
		var packageDetails = new PackageDetails
		{
			PackageName = packageDTO.PackageName.Trim(),
			PackageDescription = packageDTO.PackageDescription.Trim(),
			IsActive = packageDTO.IsActive,
			FollowUpEmail = packageDTO.FollowUpEmail,
			CreatedAt = timestamp,
			UpdatedAt = timestamp
		};

		await _dbcontext.PackageDetails.AddAsync(packageDetails, cancellationToken);
		await _dbcontext.SaveChangesAsync(cancellationToken);
		return true;
	}

	public async Task<PackageDetails?> GetPackageAsync(int packageId, CancellationToken cancellationToken)
	{
		return await _dbcontext.PackageDetails
			.AsNoTracking()
			.FirstOrDefaultAsync(p => p.PackageId == packageId, cancellationToken);
	}

	public async Task<PackageDetails> EditPackageAsync(PackageDetails packageDetails, CancellationToken cancellationToken)
	{
		_dbcontext.PackageDetails.Update(packageDetails);
		await _dbcontext.SaveChangesAsync(cancellationToken);
		return packageDetails;
	}

	public async Task<PaginatedResult<ClientDetailsDTO>> GetClientsAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
	{
		return await GetClientPageAsync(paginationRequest, applySearch: false, cancellationToken);
	}

	public async Task<PaginatedResult<ClientDetailsDTO>> SearchClientsAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
	{
		return await GetClientPageAsync(paginationRequest, applySearch: true, cancellationToken);
	}

	public async Task<bool> AddClientAsync(IReadOnlyCollection<AddClientDTO> clientDTOs, CancellationToken cancellationToken)
	{
		var clients = clientDTOs.ToArray();
		if (clients.Length == 0)
			throw new BadRequestException("At least one package must be selected.");

		var clientName = clients[0].ClientName.Trim();
		var alreadyExists = await _dbcontext.ClientDetails
			.AsNoTracking()
			.AnyAsync(c => EF.Functions.ILike(c.ClientName, clientName), cancellationToken);
		if (alreadyExists)
			throw new BadRequestException($"Client '{clientName}' already exists.");

		var packageIds = clients.Select(c => c.PackageId).Distinct().ToArray();
		var activePackageCount = await _dbcontext.PackageDetails
			.AsNoTracking()
			.CountAsync(p => packageIds.Contains(p.PackageId) && p.IsActive, cancellationToken);
		if (activePackageCount != packageIds.Length)
			throw new BadRequestException("One or more selected packages do not exist or are inactive.");

		var now = DateTime.UtcNow;
		await using var transaction = await _dbcontext.Database.BeginTransactionAsync(cancellationToken);

		var firstClient = new ClientDetails
		{
			ClientName = clientName,
			ClientDescription = clients[0].ClientDescription.Trim(),
			IsActive = clients[0].IsActive,
			PackageId = clients[0].PackageId,
			CreatedAt = now,
			UpdatedAt = now
		};

		await _dbcontext.ClientDetails.AddAsync(firstClient, cancellationToken);
		await _dbcontext.SaveChangesAsync(cancellationToken);

		var remainingClients = clients.Skip(1).Select(client => new ClientDetails

		{
			ClientId = firstClient.ClientId,
			ClientName = clientName,
			ClientDescription = clients[0].ClientDescription.Trim(),
			IsActive = clients[0].IsActive,
			PackageId = client.PackageId,
			CreatedAt = now,
			UpdatedAt = now
		});

		await _dbcontext.ClientDetails.AddRangeAsync(remainingClients, cancellationToken);
		await _dbcontext.SaveChangesAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);
		return true;
	}

	public async Task<IReadOnlyList<ClientDetails>> GetClientAsync(int clientId, CancellationToken cancellationToken)
	{
		return await _dbcontext.ClientDetails
			.AsNoTracking()
			.Where(c => c.ClientId == clientId)
			.OrderBy(c => c.PackageId)
			.ToListAsync(cancellationToken);
	}

	public async Task<IReadOnlyList<ClientDetails>> EditClientAsync(IReadOnlyCollection<EditClientDTO> clientDTOs, CancellationToken cancellationToken)
	{
		var clients = clientDTOs.ToArray();
		if (clients.Length == 0)
			throw new BadRequestException("At least one package must be selected.");

		var clientId = clients[0].ClientId;
		var existingClients = await _dbcontext.ClientDetails
			.Where(c => c.ClientId == clientId)
			.ToListAsync(cancellationToken);
		if (existingClients.Count == 0)
			throw new NotFoundException($"Client with ID {clientId} was not found.");

		var clientName = clients[0].ClientName.Trim();
		var duplicateName = await _dbcontext.ClientDetails
			.AsNoTracking()
			.AnyAsync(c => c.ClientId != clientId && EF.Functions.ILike(c.ClientName, clientName), cancellationToken);
		if (duplicateName)
			throw new BadRequestException($"Client '{clientName}' already exists.");

		var selectedPackageIds = clients.Select(c => c.PackageId).Distinct().ToHashSet();
		var existingPackageIds = existingClients.Select(c => c.PackageId).ToHashSet();
		var newPackageIds = selectedPackageIds.Except(existingPackageIds).ToArray();
		if (newPackageIds.Length > 0)
		{
			var activePackageCount = await _dbcontext.PackageDetails
				.AsNoTracking()
				.CountAsync(p => newPackageIds.Contains(p.PackageId) && p.IsActive, cancellationToken);
			if (activePackageCount != newPackageIds.Length)
				throw new BadRequestException("One or more newly selected packages do not exist or are inactive.");
		}

		var now = DateTime.UtcNow;
		var createdAt = existingClients.Min(c => c.CreatedAt);
		var description = clients[0].ClientDescription.Trim();
		var isActive = clients[0].IsActive;

		var removedClients = existingClients.Where(c => !selectedPackageIds.Contains(c.PackageId)).ToArray();
		_dbcontext.ClientDetails.RemoveRange(removedClients);

		foreach (var existingClient in existingClients.Where(c => selectedPackageIds.Contains(c.PackageId)))
		{
			existingClient.ClientName = clientName;
			existingClient.ClientDescription = description;
			existingClient.IsActive = isActive;
			existingClient.UpdatedAt = now;
		}

		var addedClients = newPackageIds.Select(packageId => new ClientDetails
		{
			ClientId = clientId,
			ClientName = clientName,
			ClientDescription = description,
			IsActive = isActive,
			PackageId = packageId,
			CreatedAt = createdAt,
			UpdatedAt = now
		}).ToArray();
		await _dbcontext.ClientDetails.AddRangeAsync(addedClients, cancellationToken);

		await _dbcontext.SaveChangesAsync(cancellationToken);
		return existingClients
			.Where(c => selectedPackageIds.Contains(c.PackageId))
			.Concat(addedClients)
			.OrderBy(c => c.PackageId)
			.ToArray();
	}

	private async Task<PaginatedResult<ClientDetailsDTO>> GetClientPageAsync(
		PaginationRequest paginationRequest,
		bool applySearch,
		CancellationToken cancellationToken)
	{
		var clientsQuery = _dbcontext.ClientDetails.AsNoTracking();
		if (applySearch)
		{
			var searchTerm = $"%{paginationRequest.SearchTerm}%";
			clientsQuery = clientsQuery.Where(c =>
				EF.Functions.ILike(c.ClientName, searchTerm) ||
				EF.Functions.ILike(c.ClientDescription, searchTerm));
		}

		var logicalClients = clientsQuery
			.GroupBy(c => c.ClientId)
			.Select(group => new
			{
				ClientId = group.Key,
				ClientName = group.Min(c => c.ClientName)
			});

		var totalRecords = await logicalClients.LongCountAsync(cancellationToken);
		var clientIds = await logicalClients
			.OrderBy(c => c.ClientName)
			.ThenBy(c => c.ClientId)
			.Skip((paginationRequest.PageIndex - 1) * paginationRequest.PageSize)
			.Take(paginationRequest.PageSize)
			.Select(c => c.ClientId)
			.ToListAsync(cancellationToken);

		var items = await clientsQuery
			.Where(c => clientIds.Contains(c.ClientId))
			.OrderBy(c => c.ClientName)
			.ThenBy(c => c.ClientId)
			.ThenBy(c => c.PackageId)
			.Select(c => new ClientDetailsDTO
			{
				ClientId = c.ClientId,
				ClientName = c.ClientName,
				ClientDescription = c.ClientDescription,
				IsActive = c.IsActive,
				PackageId = c.PackageId,
				CreatedAt = c.CreatedAt,
				UpdatedAt = c.UpdatedAt
			})
			.ToListAsync(cancellationToken);

		return new PaginatedResult<ClientDetailsDTO>(
			paginationRequest.PageIndex,
			paginationRequest.PageSize,
			totalRecords,
			items);
	}

	public async Task<PaginatedResult<RoleDetailsDTO>> GetRolesAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
	{
		var rolesQuery = _dbcontext.RoleDetails
			.AsNoTracking()
			.OrderBy(r => r.RoleName);

		var totalRecords = await rolesQuery.CountAsync(cancellationToken);

		var items = await rolesQuery
			.Skip((paginationRequest.PageIndex - 1) * paginationRequest.PageSize)
			.Take(paginationRequest.PageSize)
			.Select(r => new RoleDetailsDTO
			{
				RoleId = r.RoleId,
				RoleName = r.RoleName,
				RoleDescription = r.RoleDescription,
				IsActive = r.IsActive,
				CreatedAt = r.CreatedAt,
				UpdatedAt = r.UpdatedAt
			})
			.ToListAsync(cancellationToken);

		return new PaginatedResult<RoleDetailsDTO>(
			paginationRequest.PageIndex,
			paginationRequest.PageSize,
			totalRecords,
			items);
	}

	public async Task<PaginatedResult<RoleDetailsDTO>> SearchRolesAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
	{
		var rolesQuery = _dbcontext.RoleDetails
			.AsNoTracking()
			.Where(r => EF.Functions.ILike(r.RoleName, $"%{paginationRequest.SearchTerm}%") ||
				EF.Functions.ILike(r.RoleDescription, $"%{paginationRequest.SearchTerm}%"));

		var totalRecords = await rolesQuery.CountAsync(cancellationToken);

		var items = await rolesQuery
			.OrderBy(r => r.RoleName)
			.Skip((paginationRequest.PageIndex - 1) * paginationRequest.PageSize)
			.Take(paginationRequest.PageSize)
			.Select(r => new RoleDetailsDTO
			{
				RoleId = r.RoleId,
				RoleName = r.RoleName,
				RoleDescription = r.RoleDescription,
				IsActive = r.IsActive,
				CreatedAt = r.CreatedAt,
				UpdatedAt = r.UpdatedAt
			})
			.ToListAsync(cancellationToken);

		return new PaginatedResult<RoleDetailsDTO>(
			paginationRequest.PageIndex,
			paginationRequest.PageSize,
			totalRecords,
			items);
	}

	public async Task<bool> AddRoleAsync(AddRoleDTO roleDTO)
	{
		var timestamp = DateTime.UtcNow;
		var roleDetails = new RoleDetails
		{
			RoleName = roleDTO.RoleName!,
			RoleDescription = roleDTO.RoleDescription!,
			IsActive = roleDTO.IsActive,
			CreatedAt = timestamp,
			UpdatedAt = timestamp
		};

		await _dbcontext.RoleDetails.AddAsync(roleDetails);
		await _dbcontext.SaveChangesAsync();
		return true;
	}

	public async Task<RoleDetails?> GetRoleAsync(int roleId)
	{
		return await _dbcontext.RoleDetails
			.AsNoTracking()
			.FirstOrDefaultAsync(r => r.RoleId == roleId);
	}

	public async Task<RoleDetails> EditRoleAsync(RoleDetails roleDetails)
	{
		_dbcontext.RoleDetails.Update(roleDetails);
		await _dbcontext.SaveChangesAsync();
		return roleDetails;
	}

	public async Task<PaginatedResult<ModuleDetailsDTO>> GetModulesAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
	{
		var modulesQuery = _dbcontext.ModuleDetails
			.AsNoTracking()
			.OrderBy(m => m.ModuleName);

		var totalRecords = await modulesQuery.CountAsync(cancellationToken);

		var items = await modulesQuery
			.Skip((paginationRequest.PageIndex - 1) * paginationRequest.PageSize)
			.Take(paginationRequest.PageSize)
			.Select(m => new ModuleDetailsDTO
			{
				ModuleId = m.ModuleId,
				ModuleName = m.ModuleName,
				ModuleDescription = m.ModuleDescription,
				IsActive = m.IsActive,
				CreatedAt = m.CreatedAt,
				UpdatedAt = m.UpdatedAt
			})
			.ToListAsync(cancellationToken);

		return new PaginatedResult<ModuleDetailsDTO>(
			paginationRequest.PageIndex,
			paginationRequest.PageSize,
			totalRecords,
			items);
	}

	public async Task<PaginatedResult<ModuleDetailsDTO>> SearchModulesAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
	{
		var modulesQuery = _dbcontext.ModuleDetails
			.AsNoTracking()
			.Where(m => EF.Functions.ILike(m.ModuleName, $"%{paginationRequest.SearchTerm}%") ||
				EF.Functions.ILike(m.ModuleDescription, $"%{paginationRequest.SearchTerm}%"));

		var totalRecords = await modulesQuery.CountAsync(cancellationToken);

		var items = await modulesQuery
			.OrderBy(m => m.ModuleName)
			.Skip((paginationRequest.PageIndex - 1) * paginationRequest.PageSize)
			.Take(paginationRequest.PageSize)
			.Select(m => new ModuleDetailsDTO
			{
				ModuleId = m.ModuleId,
				ModuleName = m.ModuleName,
				ModuleDescription = m.ModuleDescription,
				IsActive = m.IsActive,
				CreatedAt = m.CreatedAt,
				UpdatedAt = m.UpdatedAt
			})
			.ToListAsync(cancellationToken);

		return new PaginatedResult<ModuleDetailsDTO>(
			paginationRequest.PageIndex,
			paginationRequest.PageSize,
			totalRecords,
			items);
	}

	public async Task<bool> AddModuleAsync(AddModuleDTO moduleDTO)
	{
		var timestamp = DateTime.UtcNow;
		var moduleDetails = new ModuleDetails
		{
			ModuleName = moduleDTO.ModuleName!,
			ModuleDescription = moduleDTO.ModuleDescription!,
			IsActive = moduleDTO.IsActive,
			CreatedAt = timestamp,
			UpdatedAt = timestamp
		};

		await _dbcontext.ModuleDetails.AddAsync(moduleDetails);
		await _dbcontext.SaveChangesAsync();
		return true;
	}

	public async Task<ModuleDetails?> GetModuleAsync(int moduleId)
	{
		return await _dbcontext.ModuleDetails
			.AsNoTracking()
			.FirstOrDefaultAsync(m => m.ModuleId == moduleId);
	}

	public async Task<ModuleDetails> EditModuleAsync(ModuleDetails moduleDetails)
	{
		_dbcontext.ModuleDetails.Update(moduleDetails);
		await _dbcontext.SaveChangesAsync();
		return moduleDetails;
	}

	public async Task<IReadOnlyList<UserClientDetailsDTO>> GetUserClientAssignmentsAsync(
		CancellationToken cancellationToken)
	{
		return await _dbcontext.UserClientDetails
			.AsNoTracking()
			.OrderBy(assignment => assignment.UserId)
			.Select(assignment => new UserClientDetailsDTO
			{
				UserId = assignment.UserId,
				ClientId = assignment.ClientId,
				CreatedAt = assignment.CreatedAt,
				UpdatedAt = assignment.UpdatedAt
			})
			.ToListAsync(cancellationToken);
	}

	public async Task<UserClientDetails?> GetUserClientAssignmentAsync(
		Guid userId,
		CancellationToken cancellationToken)
	{
		return await _dbcontext.UserClientDetails
			.AsNoTracking()
			.FirstOrDefaultAsync(assignment => assignment.UserId == userId, cancellationToken);
	}

	public async Task<UserClientDetails> AssignUserClientAsync(
		AssignUserClientDTO assignment,
		CancellationToken cancellationToken)
	{
		var clientExists = await _dbcontext.ClientDetails
			.AsNoTracking()
			.AnyAsync(client =>
				client.ClientId == assignment.ClientId && client.IsActive,
				cancellationToken);
		if (!clientExists)
			throw new BadRequestException("The selected client does not exist or is inactive.");

		var now = DateTime.UtcNow;
		var userClient = await _dbcontext.UserClientDetails
			.FirstOrDefaultAsync(item => item.UserId == assignment.UserId, cancellationToken);

		if (userClient is null)
		{
			userClient = new UserClientDetails
			{
				UserId = assignment.UserId,
				ClientId = assignment.ClientId,
				CreatedAt = now,
				UpdatedAt = now
			};
			await _dbcontext.UserClientDetails.AddAsync(userClient, cancellationToken);
		}
		else
		{
			userClient.ClientId = assignment.ClientId;
			userClient.UpdatedAt = now;
		}

		var existingAccessRows = await _dbcontext.UserDetails
			.Where(user => user.UserId == assignment.UserId)
			.ToListAsync(cancellationToken);
		foreach (var accessRow in existingAccessRows)
		{
			accessRow.ClientId = assignment.ClientId;
			accessRow.UpdatedAt = now;
		}

		await _dbcontext.SaveChangesAsync(cancellationToken);
		return userClient;
	}

	public async Task<PaginatedResult<UserDetailsDTO>> GetUsersAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
	{
		return await GetUserPageAsync(paginationRequest, applySearch: false, cancellationToken);
	}

	public async Task<PaginatedResult<UserDetailsDTO>> SearchUsersAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
	{
		return await GetUserPageAsync(paginationRequest, applySearch: true, cancellationToken);
	}

	public async Task<bool> AddUserAsync(IReadOnlyCollection<AddUserDTO> userDTOs, CancellationToken cancellationToken)
	{
		var users = userDTOs.ToArray();
		if (users.Length == 0)
			throw new BadRequestException("At least one module must be selected.");

		var user = users[0];
		var userEmail = user.UserEmail.Trim();
		var alreadyExists = await _dbcontext.UserDetails
			.AsNoTracking()
			.AnyAsync(existing =>
				existing.UserId == user.UserId ||
				EF.Functions.ILike(existing.UserEmail, userEmail), cancellationToken);
		if (alreadyExists)
			throw new BadRequestException("The selected Auth user already exists in ATS User Management.");

		if (user.ClientId.HasValue)
		{
			var clientExists = await _dbcontext.ClientDetails
				.AsNoTracking()
				.AnyAsync(client => client.ClientId == user.ClientId.Value && client.IsActive, cancellationToken);
			if (!clientExists)
				throw new BadRequestException("The selected client does not exist or is inactive.");
		}

		var roleExists = await _dbcontext.RoleDetails
			.AsNoTracking()
			.AnyAsync(role => role.RoleId == user.RoleId && role.IsActive, cancellationToken);
		if (!roleExists)
			throw new BadRequestException("The selected role does not exist or is inactive.");

		var moduleIds = users.Select(item => item.ModuleId).Distinct().ToArray();
		var activeModuleCount = await _dbcontext.ModuleDetails
			.AsNoTracking()
			.CountAsync(module => moduleIds.Contains(module.ModuleId) && module.IsActive, cancellationToken);
		if (activeModuleCount != moduleIds.Length)
			throw new BadRequestException("One or more selected modules do not exist or are inactive.");

		var userId = user.UserId;
		var now = DateTime.UtcNow;
		if (user.ClientId.HasValue)
		{
			var hasClientAssignment = await _dbcontext.UserClientDetails
				.AsNoTracking()
				.AnyAsync(assignment => assignment.UserId == userId, cancellationToken);
			if (!hasClientAssignment)
			{
				await _dbcontext.UserClientDetails.AddAsync(new UserClientDetails
				{
					UserId = userId,
					ClientId = user.ClientId.Value,
					CreatedAt = now,
					UpdatedAt = now
				}, cancellationToken);
			}
		}

		var userDetails = moduleIds.Select(moduleId => new UserDetails
		{
			UserId = userId,
			UserName = user.UserName.Trim(),
			UserEmail = userEmail,
			IsActive = user.IsActive,
			ClientId = user.ClientId,
			Site = user.Site.Trim(),
			RoleId = user.RoleId,
			ModuleId = moduleId,
			CreatedAt = now,
			UpdatedAt = now
		}).ToArray();

		await _dbcontext.UserDetails.AddRangeAsync(userDetails, cancellationToken);
		await _dbcontext.SaveChangesAsync(cancellationToken);
		return true;
	}

	public async Task<IReadOnlyList<UserDetails>> GetUserAsync(Guid userId, CancellationToken cancellationToken)
	{
		return await _dbcontext.UserDetails
			.AsNoTracking()
			.Where(user => user.UserId == userId)
			.OrderBy(user => user.ModuleId)
			.ToListAsync(cancellationToken);
	}

	public async Task<IReadOnlyList<int>> GetActiveUserModuleIdsAsync(
		Guid userId,
		CancellationToken cancellationToken)
	{
		return await _dbcontext.UserDetails
			.AsNoTracking()
			.Where(user => user.UserId == userId && user.IsActive && user.Module.IsActive)
			.Select(user => user.ModuleId)
			.Distinct()
			.OrderBy(moduleId => moduleId)
			.ToListAsync(cancellationToken);
	}

	public async Task<IReadOnlyList<UserDetails>> EditUserAsync(IReadOnlyCollection<EditUserDTO> userDTOs, CancellationToken cancellationToken)
	{
		var users = userDTOs.ToArray();
		if (users.Length == 0)
			throw new BadRequestException("At least one module must be selected.");

		var userId = users[0].UserId;
		var existingUsers = await _dbcontext.UserDetails
			.Where(user => user.UserId == userId)
			.ToListAsync(cancellationToken);
		if (existingUsers.Count == 0)
			throw new NotFoundException($"User with ID {userId} was not found.");

		var user = users[0];
		var userEmail = user.UserEmail.Trim();
		var duplicateEmail = await _dbcontext.UserDetails
			.AsNoTracking()
			.AnyAsync(existing => existing.UserId != userId && EF.Functions.ILike(existing.UserEmail, userEmail), cancellationToken);
		if (duplicateEmail)
			throw new BadRequestException($"User with email '{userEmail}' already exists.");

		var currentUser = existingUsers[0];
		if (currentUser.ClientId != user.ClientId)
		{
			if (user.ClientId.HasValue)
			{
				var clientExists = await _dbcontext.ClientDetails
					.AsNoTracking()
					.AnyAsync(client => client.ClientId == user.ClientId.Value && client.IsActive, cancellationToken);
				if (!clientExists)
					throw new BadRequestException("The selected client does not exist or is inactive.");
			}
		}

		if (currentUser.RoleId != user.RoleId)
		{
			var roleExists = await _dbcontext.RoleDetails
				.AsNoTracking()
				.AnyAsync(role => role.RoleId == user.RoleId && role.IsActive, cancellationToken);
			if (!roleExists)
				throw new BadRequestException("The selected role does not exist or is inactive.");
		}

		var selectedModuleIds = users.Select(item => item.ModuleId).Distinct().ToHashSet();
		var existingModuleIds = existingUsers.Select(item => item.ModuleId).ToHashSet();
		var newModuleIds = selectedModuleIds.Except(existingModuleIds).ToArray();
		if (newModuleIds.Length > 0)
		{
			var activeModuleCount = await _dbcontext.ModuleDetails
				.AsNoTracking()
				.CountAsync(module => newModuleIds.Contains(module.ModuleId) && module.IsActive, cancellationToken);
			if (activeModuleCount != newModuleIds.Length)
				throw new BadRequestException("One or more newly selected modules do not exist or are inactive.");
		}

		var now = DateTime.UtcNow;
		var createdAt = existingUsers.Min(existing => existing.CreatedAt);
		var userName = user.UserName.Trim();
		var site = user.Site.Trim();

		var removedUsers = existingUsers.Where(existing => !selectedModuleIds.Contains(existing.ModuleId)).ToArray();
		_dbcontext.UserDetails.RemoveRange(removedUsers);

		foreach (var existingUser in existingUsers.Where(existing => selectedModuleIds.Contains(existing.ModuleId)))
		{
			existingUser.UserName = userName;
			existingUser.UserEmail = userEmail;
			existingUser.IsActive = user.IsActive;
			existingUser.ClientId = user.ClientId;
			existingUser.Site = site;
			existingUser.RoleId = user.RoleId;
			existingUser.UpdatedAt = now;
		}

		var addedUsers = newModuleIds.Select(moduleId => new UserDetails
		{
			UserId = userId,
			UserName = userName,
			UserEmail = userEmail,
			IsActive = user.IsActive,
			ClientId = user.ClientId,
			Site = site,
			RoleId = user.RoleId,
			ModuleId = moduleId,
			CreatedAt = createdAt,
			UpdatedAt = now
		}).ToArray();

		await _dbcontext.UserDetails.AddRangeAsync(addedUsers, cancellationToken);
		await _dbcontext.SaveChangesAsync(cancellationToken);

		return existingUsers
			.Where(existing => selectedModuleIds.Contains(existing.ModuleId))
			.Concat(addedUsers)
			.OrderBy(existing => existing.ModuleId)
			.ToArray();
	}

	private async Task<PaginatedResult<UserDetailsDTO>> GetUserPageAsync(
		PaginationRequest paginationRequest,
		bool applySearch,
		CancellationToken cancellationToken)
	{
		var usersQuery = _dbcontext.UserDetails.AsNoTracking();
		if (applySearch)
		{
			var searchTerm = $"%{paginationRequest.SearchTerm}%";
			usersQuery = usersQuery.Where(user =>
				EF.Functions.ILike(user.UserName, searchTerm) ||
				EF.Functions.ILike(user.UserEmail, searchTerm) ||
				EF.Functions.ILike(user.Site, searchTerm));
		}

		var logicalUsers = usersQuery
			.GroupBy(user => user.UserId)
			.Select(group => new
			{
				UserId = group.Key,
				UserName = group.Min(user => user.UserName),
				UserEmail = group.Min(user => user.UserEmail)
			});

		var totalRecords = await logicalUsers.LongCountAsync(cancellationToken);
		var userIds = await logicalUsers
			.OrderBy(user => user.UserName)
			.ThenBy(user => user.UserEmail)
			.ThenBy(user => user.UserId)
			.Skip((paginationRequest.PageIndex - 1) * paginationRequest.PageSize)
			.Take(paginationRequest.PageSize)
			.Select(user => user.UserId)
			.ToListAsync(cancellationToken);

		var items = await usersQuery
			.Where(user => userIds.Contains(user.UserId))
			.OrderBy(user => user.UserName)
			.ThenBy(user => user.UserEmail)
			.ThenBy(user => user.UserId)
			.ThenBy(user => user.ModuleId)
			.Select(user => new UserDetailsDTO
			{
				UserId = user.UserId,
				UserName = user.UserName,
				UserEmail = user.UserEmail,
				IsActive = user.IsActive,
				ClientId = user.ClientId,
				Site = user.Site,
				RoleId = user.RoleId,
				ModuleId = user.ModuleId,
				CreatedAt = user.CreatedAt,
				UpdatedAt = user.UpdatedAt
			})
			.ToListAsync(cancellationToken);

		return new PaginatedResult<UserDetailsDTO>(
			paginationRequest.PageIndex,
			paginationRequest.PageSize,
			totalRecords,
			items);
	}
}
