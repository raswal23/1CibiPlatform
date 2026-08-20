namespace ATS.Data.Repository;

public interface IATSRepository
{
	Task<bool> AddPersonalDetailsAsync(PersonalDetails personalDetails);
	Task<bool> AddAddressDetailsAsync(AddressDetails addressDetails);
	Task<bool> AddEducationalBackgroundAsync(EducationalBackground educationalBackground);
	Task<bool> AddLicensesDetailsAsync(LicensesDetails licensesDetails);
	Task<bool> AddProfessionalExperiencesAsync(ProfessionalExperiences professionalExperiences);
	Task<bool> AddReferenceDetailsAsync(ReferenceDetails referenceDetails);
	Task<EmailIdAndApplicationFormPathDTO> GetEmailIdAndApplicationFormPathAsync(string hashToken,
												 CancellationToken cancellationToken);
	Task<bool> AddSignatureDetailsAsync(SignatureDetails signatureDetails);
	Task<bool> AddEmailInvitationRequestAsync(EmailInvitationRequest emailInvitationRequest);
	Task<bool> AddBulkUploadFileDetailsAsync(BulkUploadFileDetails bulkUploadFileDetails);

	Task<List<BulkUploadFileDetails>> GetBulkUploadFileDetailsAsync();
	Task<int> ReleaseBulkFileClaimsAsync(List<BulkUploadFileDetails> bulkUploadFileDetails);
	Task<int> ReleaseStaleBulkFileClaimsAsync(TimeSpan staleAfter);
	Task<List<EmailInvitationRequest>> GetPendingEmailInvitationRequestsAsync();
	Task<int> ReleaseStaleEmailInvitationClaimsAsync(TimeSpan staleAfter);
	Task<bool> AddBulkEmailInvitationRequestAsync(List<EmailInvitationRequest> emailInvitationRequests);
	Task<bool> UpdateBulkEmailInvitationRequestForSentEmailAsync(List<EmailInvitationRequest> emailInvitationRequests);
	Task<bool> UpdateBulkEmailInvitationRequestForNotSentEmailAsync(List<EmailInvitationRequest> emailInvitationRequests);
	Task<bool> UpdateEmailInvitationRequestForFilledUpFormAsync(Guid emailInvitationRequestId);
	Task<bool> UpdateBulkFileDetailsStatusAsync(List<BulkUploadFileDetails> bulkUploadFileDetails);
	Task<bool> UpdateSingleEmailInvitationRequestStatusForSentEmailAsync(Guid emailInvitationId);
	Task<bool> UpdateSingleEmailInvitationRequestStatusForNotSentEmailAsync(Guid emailInvitationId);
	Task<bool> IsHashTokenValidAsync(string hashToken, CancellationToken cancellationToken);
	Task<int> WithdrawnApplicationForm(string hashToken, CancellationToken cancellationToken);
	Task<PaginatedResult<EmailInvitationRequestListDTO>> GetWithdrawnEmailInvitationRequestsAsync(PaginationRequest paginationRequest, AtsQueryScope scope, CancellationToken cancellationToken);
	Task<PaginatedResult<EmailInvitationRequestListDTO>> SearchWithdrawnEmailInvitationRequestsAsync(PaginationRequest paginationRequest, AtsQueryScope scope, CancellationToken cancellationToken);
	Task<PaginatedResult<DisputeOrderListDTO>> GetDisputeOrdersAsync(PaginationRequest paginationRequest, AtsQueryScope scope, CancellationToken cancellationToken);
	Task<PaginatedResult<DisputeOrderListDTO>> SearchDisputeOrdersAsync(PaginationRequest paginationRequest, AtsQueryScope scope, CancellationToken cancellationToken);
	Task<bool> MarkAsDisputedAsync(DisputeOrderRequestDTO disputeRequest, CancellationToken cancellationToken);
	Task<ReportDetails?> GetReportDetailsByStatusAsync(Guid emailInvitationRequestId, string reportStatus, CancellationToken cancellationToken);
	Task<bool> AddReportDetailsAsync(ReportDetails reportDetails, CancellationToken cancellationToken);
	Task<bool> UpdateReportDetailsAsync(ReportDetails reportDetails, CancellationToken cancellationToken);
	Task<bool> UpdateOrderStatusAsync(Guid EmailInvitationRequestId, string orderStatus, DateTime? orderCompletedAt, CancellationToken cancellationToken);
	Task<bool> AddArchiveReportAsync(ArchiveReport archiveReport, CancellationToken cancellationToken);
	Task<ATSDashboardDTO> GetDashboardAsync(
		string? requester,
		AtsQueryScope scope,
		CancellationToken cancellationToken);
	Task<PaginatedResult<ReportListDTO>> GetReportsAsync(PaginationRequest paginationRequest, AtsQueryScope scope, string? sortColumn, bool sortDescending, CancellationToken cancellationToken);
	Task<PaginatedResult<ReportListDTO>> SearchReportsAsync(PaginationRequest paginationRequest, AtsQueryScope scope, string? sortColumn, bool sortDescending, CancellationToken cancellationToken);
	Task<ReportResultDTO?> GetReportResultByEmailInvitationRequestIdAsync(Guid emailInvitationRequestId, CancellationToken cancellationToken);
	Task<List<EmailInvitationRequest>> GetEmailInvitationRequestsNeedingProjectionAsync(CancellationToken cancellationToken);
	Task<ApplicantSearchProjection?> GetApplicantSearchProjectionByIdAsync(Guid emailInvitationRequestId, CancellationToken cancellationToken);
	Task<bool> AddApplicantSearchProjectionAsync(ApplicantSearchProjection projection, CancellationToken cancellationToken);
	Task<EmailInvitationRequest> GetEmailInvitationRequestByIdAsync(Guid emailInvitationId, CancellationToken cancellationToken);
	Task<bool> ResendApplicationFormAsync(Guid emailInvitationId, string hashToken, DateTime hashTokenExpiration, CancellationToken cancellationToken);
	Task<List<DownloadDocumentDTO>> GetDownloadDocumentsAsync(List<Guid> emailInvitationRequestIds, CancellationToken cancellationToken);
}
