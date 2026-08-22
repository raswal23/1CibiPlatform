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
	/// <summary>
	/// Resolves the invitation a hash token refers to. Never cached: this is the
	/// authorization decision for the anonymous application-form endpoints, so it must
	/// always observe the current expiry and form status.
	/// </summary>
	Task<ApplicationFormClaimDTO?> GetApplicationFormClaimAsync(string hashToken,
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
	/// <param name="windowStart">
	/// Earliest OrderCreatedAt to load. Rows with no order date are always included -
	/// the candidate-response tiles count them by email status.
	/// </param>
	Task<IReadOnlyList<EmailInvitationRequest>> GetDashboardDataAsync(
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredRequestorId,
		DateTime windowStart,
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
	/// <summary>
	/// Returns null when the order does not exist <em>or</em> falls outside the caller's
	/// scope - the two cases are deliberately indistinguishable.
	/// </summary>
	Task<ReportResultDTO?> GetReportResultByEmailInvitationRequestIdAsync(
		Guid emailInvitationRequestId,
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredRequestorId,
		CancellationToken cancellationToken);
	Task<List<EmailInvitationRequest>> GetEmailInvitationRequestsNeedingProjectionAsync(CancellationToken cancellationToken);
	Task<ApplicantSearchProjection?> GetApplicantSearchProjectionByIdAsync(Guid emailInvitationRequestId, CancellationToken cancellationToken);
	Task<bool> AddApplicantSearchProjectionAsync(ApplicantSearchProjection projection, CancellationToken cancellationToken);
	Task<EmailInvitationRequest> GetEmailInvitationRequestByIdAsync(Guid emailInvitationId, CancellationToken cancellationToken);
	Task<bool> ResendApplicationFormAsync(Guid emailInvitationId, string hashToken, DateTime hashTokenExpiration, CancellationToken cancellationToken);
	/// <summary>
	/// Resolves the object storage keys for the given orders, filtered to the caller's
	/// scope. Ids outside the scope contribute no rows.
	/// </summary>
	Task<List<DownloadDocumentDTO>> GetDownloadDocumentsAsync(
		List<Guid> emailInvitationRequestIds,
		IReadOnlyCollection<int>? authorizedClientIds,
		Guid? requiredRequestorId,
		CancellationToken cancellationToken);
}
