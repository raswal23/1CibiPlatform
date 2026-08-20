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
	Task<bool> UpdateBulkFileDetailsStatusAsync(List<Guid> bulkUploadFileDetailIds, string orderStatus);
	Task<bool> UpdateSingleEmailInvitationRequestStatusForSentEmailAsync(Guid emailInvitationId);
	Task<bool> UpdateSingleEmailInvitationRequestStatusForNotSentEmailAsync(Guid emailInvitationId);
	Task<bool> IsHashTokenValidAsync(string hashToken, CancellationToken cancellationToken);
	Task<int> WithdrawnApplicationForm(string hashToken, CancellationToken cancellationToken);
	Task<List<EmailInvitationRequestListDTO>> GetWithdrawnPageAsync(
		string? searchTerm,
		Guid? afterId,
		int take,
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredRequestorId,
		CancellationToken cancellationToken);
	Task<long> CountWithdrawnAsync(
		string? searchTerm,
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredRequestorId,
		CancellationToken cancellationToken);
	Task<List<DisputeOrderListDTO>> GetDisputeOrdersPageAsync(
		string? searchTerm,
		bool? afterHasDispute,
		DateTime? afterCreatedAt,
		Guid? afterId,
		int take,
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredRequestorId,
		CancellationToken cancellationToken);
	Task<long> CountDisputeOrdersAsync(
		string? searchTerm,
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredRequestorId,
		CancellationToken cancellationToken);
    Task<bool> MarkAsDisputedAsync(DisputeOrderRequestDTO disputeRequest, CancellationToken cancellationToken);
    Task<ReportDetails?> GetReportDetailsByStatusAsync(Guid emailInvitationRequestId, string reportStatus, CancellationToken cancellationToken);

	Task<bool> AddReportDetailsAsync(ReportDetails reportDetails, CancellationToken cancellationToken);
	Task<bool> UpdateReportDetailsAsync(ReportDetails reportDetails, CancellationToken cancellationToken);
	Task<bool> UpdateOrderStatusAsync(Guid EmailInvitationRequestId, string orderStatus, DateTime? orderCompletedAt, CancellationToken cancellationToken);
	Task<bool> AddArchiveReportAsync(ArchiveReport archiveReport, CancellationToken cancellationToken);
	Task<IReadOnlyList<EmailInvitationRequest>> GetDashboardDataAsync(
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredRequestorId,
		CancellationToken cancellationToken);
	Task<List<ReportRowDTO>> GetReportsPageAsync(
		int? afterRank,
		DateTime? afterCompletedAt,
		Guid? afterId,
		int take,
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredRequestorId,
		CancellationToken cancellationToken);
	Task<List<ReportRowDTO>> SearchReportsPageAsync(
		int? afterRank,
		DateTime? afterCompletedAt,
		Guid? afterId,
		int take,
		string? searchTerm,
		DateTime? startDate,
		DateTime? endDate,
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredRequestorId,
		CancellationToken cancellationToken);
	Task<long> CountReportsAsync(
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredRequestorId,
		CancellationToken cancellationToken);
	Task<long> CountSearchReportsAsync(
		string? searchTerm,
		DateTime? startDate,
		DateTime? endDate,
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredRequestorId,
		CancellationToken cancellationToken);
    Task<ReportResultDTO?> GetReportResultByEmailInvitationRequestIdAsync(Guid emailInvitationRequestId, CancellationToken cancellationToken);
    Task<List<EmailInvitationRequest>> GetEmailInvitationRequestsNeedingProjectionAsync(CancellationToken cancellationToken);
	Task<ApplicantSearchProjection?> GetApplicantSearchProjectionByIdAsync(Guid emailInvitationRequestId, CancellationToken cancellationToken);
	Task<bool> AddApplicantSearchProjectionAsync(ApplicantSearchProjection projection, CancellationToken cancellationToken);
	Task<EmailInvitationRequest> GetEmailInvitationRequestByIdAsync(Guid emailInvitationId, CancellationToken cancellationToken);
	Task<bool> ResendApplicationFormAsync(Guid emailInvitationId, string hashToken, DateTime hashTokenExpiration, CancellationToken cancellationToken);
	Task<List<DownloadDocumentDTO>> GetDownloadDocumentsAsync(List<Guid> emailInvitationRequestIds, CancellationToken cancellationToken);
}
