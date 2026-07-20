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
			.Where(bf => bf.Status == "Pending")
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
			.SetProperty(x => x.EmailSentStatus, x => "Done")
			.SetProperty(x => x.EmailSentAt, x => DateTime.UtcNow));

		return true;
	}

	public async Task<bool> UpdateBulkEmailInvitationRequestForNotSentEmailAsync(List<EmailInvitationRequest> emailInvitationRequests)
	{
		var ids = emailInvitationRequests.Select(x => x.EmailInvitationID).ToList();

		await _dbcontext.EmailInvitationRequests
			.Where(x => ids.Contains(x.EmailInvitationID))
			.ExecuteUpdateAsync(setters => setters
			.SetProperty(x => x.EmailSentStatus, x => "Error"));

		return true;
	}

	public async Task<bool> UpdateEmailInvitationRequestForFilledUpFormAsync(Guid emailInvitationRequestId)
	{

		await _dbcontext.EmailInvitationRequests
			.Where(x => x.EmailInvitationID == emailInvitationRequestId)
			.ExecuteUpdateAsync(setters => setters
			.SetProperty(x => x.ApplicationFormStatus, x => "Done")
			.SetProperty(x => x.FormCompletedAt, x => DateTime.UtcNow)
			.SetProperty(
				x => x.OrderStatus,
				x => x.OrderStatus == "Completed"
					? x.OrderStatus
					: "In Progress")
			.SetProperty(x => x.NeedsProjection, x => true));

		return true;
	}

	public async Task<bool> UpdateBulkFileDetailsStatusAsync(List<BulkUploadFileDetails> bulkUploadFileDetails)
	{
		var fileIds = bulkUploadFileDetails.Select(x => x.FileID).ToList();

		await _dbcontext.BulkUploadFileDetails
				.Where(x => fileIds.Contains(x.FileID))
				.ExecuteUpdateAsync(setters => setters
				.SetProperty(x => x.Status, x => "Done"));

		return true;
	}

	public async Task<bool> UpdateSingleEmailInvitationRequestStatusForSentEmailAsync(Guid emailInvitationId)
	{
		await _dbcontext.EmailInvitationRequests.Where(x => x.EmailInvitationID == emailInvitationId)
				.ExecuteUpdateAsync(setters => setters
				.SetProperty(x => x.EmailSentStatus, x => "Done")
				.SetProperty(x => x.EmailSentAt, x => DateTime.UtcNow));

		return true;
	}

	public async Task<bool> UpdateSingleEmailInvitationRequestStatusForNotSentEmailAsync(Guid emailInvitationId)
	{
		await _dbcontext.EmailInvitationRequests.Where(x => x.EmailInvitationID == emailInvitationId)
				.ExecuteUpdateAsync(setters => setters
				.SetProperty(x => x.EmailSentStatus, x => "Error"));

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
				.SetProperty(x => x.ApplicationFormStatus, x => "Withdrawn")
				.SetProperty(x => x.OrderStatus, x => "Application Withdrawn"));
	}

	public async Task<PaginatedResult<EmailInvitationRequestListDTO>> GetWithdrawnEmailInvitationRequestsAsync(PaginationRequest paginationRequest, CancellationToken cancellationToken)
	{
		var usersQuery = _dbcontext.EmailInvitationRequests
			.AsNoTracking()
			.Where(eir => eir.OrderStatus == "Application Withdrawn");

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
							.Where(eir => eir.OrderStatus == "Application Withdrawn")
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
			.Where(eir => eir.OrderStatus == "Completed" && eir.OrderCreatedAt.HasValue && eir.OrderCompletedAt!.Value >= disputeWindowStart);

		var totalRecords = await usersQuery.LongCountAsync(cancellationToken);

		var items = await usersQuery
			.OrderByDescending(eir => eir.IsDisputed)
	        .ThenByDescending(eir => eir.OrderCreatedAt)
			.ThenBy(eir => eir.EmailInvitationID)
			.Skip((paginationRequest.PageIndex - 1) * paginationRequest.PageSize)
			.Take(paginationRequest.PageSize)
			.Select(eir => new DisputeOrderListDTO
			{
				EmailInvitationID = eir.EmailInvitationID,
				FirstName = eir.FirstName,
				LastName = eir.LastName,
				OrderStatus = eir.OrderStatus,
			    OrderCompletedAt = eir.OrderCompletedAt,
				IsDisputed = eir.IsDisputed,
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
				(eir.OrderStatus == "Completed" && eir.OrderCreatedAt.HasValue && eir.OrderCompletedAt!.Value >= disputeWindowStart) &&
			   (EF.Functions.ILike(eir.FirstName!, $"%{paginationRequest.SearchTerm}%") ||
				EF.Functions.ILike(eir.LastName!, $"%{paginationRequest.SearchTerm}%") ||
				EF.Functions.ILike(eir.EmailAddress!, $"%{paginationRequest.SearchTerm}%")));

		var totalRecords = await usersQuery.LongCountAsync(cancellationToken);

		var items = await usersQuery
			.OrderByDescending(eir => eir.IsDisputed)
			.ThenByDescending(eir => eir.OrderCreatedAt)
			.ThenBy(eir => eir.EmailInvitationID)
			.Skip((paginationRequest.PageIndex - 1) * paginationRequest.PageSize)
			.Take(paginationRequest.PageSize)
			.Select(eir => new DisputeOrderListDTO
			{
				EmailInvitationID = eir.EmailInvitationID,
				FirstName = eir.FirstName,
				LastName = eir.LastName,
				OrderStatus = eir.OrderStatus,
				OrderCompletedAt = eir.OrderCompletedAt,
				IsDisputed = eir.IsDisputed,
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

	public async Task<bool> MarkAsDisputedAsync(Guid emailInvitationId, CancellationToken cancellationToken)
	{
		var affectedRows = await _dbcontext.EmailInvitationRequests
			.Where(eir => eir.EmailInvitationID == emailInvitationId)
			.ExecuteUpdateAsync(setters => setters
				.SetProperty(eir => eir.IsDisputed, true)
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
				.SetProperty(x => x.OrderStatus, orderStatus)
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
			"SubjectName" => sortDescending
				? usersQuery.OrderByDescending(x => x.FirstName).ThenByDescending(x => x.LastName)
				: usersQuery.OrderBy(x => x.FirstName).ThenBy(x => x.LastName),
			"OrderStatus" => sortDescending
				? usersQuery.OrderByDescending(x => x.OrderStatus)
				: usersQuery.OrderBy(x => x.OrderStatus),
			"OrderCompletedAt" => sortDescending
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
			})
			.Where(x =>
				EF.Functions.ILike($"{x.FirstName} {x.LastName}", $"%{paginationRequest.SearchTerm}%") ||
				EF.Functions.ILike(x.OrderStatus ?? string.Empty, $"%{paginationRequest.SearchTerm}%") ||
				EF.Functions.ILike(x.SelectPackage ?? string.Empty, $"%{paginationRequest.SearchTerm}%") ||
				EF.Functions.ILike(x.HitStatus ?? string.Empty, $"%{paginationRequest.SearchTerm}%"));

		usersQuery = sortColumn switch
		{
			"SubjectName" => sortDescending
				? usersQuery.OrderByDescending(x => x.FirstName).ThenByDescending(x => x.LastName)
				: usersQuery.OrderBy(x => x.FirstName).ThenBy(x => x.LastName),
			"OrderStatus" => sortDescending
				? usersQuery.OrderByDescending(x => x.OrderStatus)
				: usersQuery.OrderBy(x => x.OrderStatus),
			"OrderCompletedAt" => sortDescending
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
				Personal = new
				{
					eir.PersonalDetails!.ResumeFileName,
					eir.PersonalDetails.BiometricFileName,
					eir.PersonalDetails.AdditionalGovtIDFileName
				},
				Educational = new
				{
					eir.EducationalBackground!.DoctorateDiplomaFileName,
					eir.EducationalBackground!.MastersDiplomaFileName,
					eir.EducationalBackground!.BachelorsDiplomaFileName,
					eir.EducationalBackground!.SeniorHighSchoolDiplomaFileName,
					eir.EducationalBackground!.HighSchoolDiplomaFileName,
				},
				Professional = new
				{
					eir.ProfessionalExperiences!.Emp1COEUploadFileName,
					eir.ProfessionalExperiences!.Emp2COEUploadFileName,
					eir.ProfessionalExperiences!.Emp3COEUploadFileName,
					eir.ProfessionalExperiences!.COEUploadFileName,
				},
				Signature = new
				{ 
					eir.SignatureDetails!.SignatureFileName
				},
				LatestReport = eir.ReportDetails!
					.OrderByDescending(rd => rd.ReportUploadedAt)
					.Select(rd => new
					{
						rd.HitStatus,
						rd.ReportFileName,
						rd.ReportUploadedAt
					})
				   .FirstOrDefault()
			})
			.FirstOrDefaultAsync(cancellationToken);

		string? diplomaFileKey = result!.Educational?.DoctorateDiplomaFileName
			?? result.Educational?.MastersDiplomaFileName
			?? result.Educational?.BachelorsDiplomaFileName
			?? result.Educational?.SeniorHighSchoolDiplomaFileName
			?? result.Educational?.HighSchoolDiplomaFileName;

		string? coeFileKey = result.Professional?.Emp1COEUploadFileName
			?? result.Professional?.Emp2COEUploadFileName
			?? result.Professional?.Emp3COEUploadFileName
			?? result.Professional?.COEUploadFileName;

		return new ReportResultDTO
		{
			SubjectName = $"{result.FirstName} {result.LastName}".Trim(),
			OrderStatus = result.OrderStatus,
			HitStatus = result.LatestReport?.HitStatus,
			SelectedPackage = result.SelectPackage,
			ResumeFileName = result.Personal?.ResumeFileName,
			IdUploadedFileName = result.Personal?.AdditionalGovtIDFileName,
			CoeFileName = coeFileKey,
			DiplomaFileName = diplomaFileKey,
			BiometricPhotoFileName = result.Personal?.BiometricFileName,
			ConsentFormFileName = result.Signature?.SignatureFileName,
			UploadedReportFileName = result.LatestReport?.ReportFileName,
			ReportUploadedAt = result.LatestReport?.ReportUploadedAt
		};
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
				.SetProperty(eir => eir.OrderStatus, "Pending Candidate Info")
				.SetProperty(eir => eir.ApplicationFormStatus, "Pending"),
				cancellationToken);

		return true;
	}
}