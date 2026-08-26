namespace ATS.Data.Cache;

public partial class ATSCacheRepository
{
	public async Task<bool> AddEmailInvitationRequestAsync(EmailInvitationRequest emailInvitationRequest)
	{
		var result = await _atsRepository.AddEmailInvitationRequestAsync(emailInvitationRequest);

		if (result)
			await _hybridCache.RemoveByTagAsync(CacheTags.Report);

		return result;
	}

	public async Task<List<EmailInvitationRequest>> GetPendingEmailInvitationRequestsAsync()
	{
		return await _atsRepository.GetPendingEmailInvitationRequestsAsync();
	}

	public async Task<int> ReleaseStaleEmailInvitationClaimsAsync(TimeSpan staleAfter)
	{
		return await _atsRepository.ReleaseStaleEmailInvitationClaimsAsync(staleAfter);
	}

	public async Task<bool> AddBulkEmailInvitationRequestAsync(List<EmailInvitationRequest> emailInvitationRequests)
	{
		var result = await _atsRepository.AddBulkEmailInvitationRequestAsync(emailInvitationRequests);

		if (result)
			await _hybridCache.RemoveByTagAsync(CacheTags.Report);

		return result;
	}

	public async Task<bool> UpdateBulkEmailInvitationRequestForSentEmailAsync(List<EmailInvitationRequest> emailInvitationRequests)
	{
		return await _atsRepository.UpdateBulkEmailInvitationRequestForSentEmailAsync(emailInvitationRequests);
	}

	public async Task<bool> UpdateSingleEmailInvitationRequestStatusForSentEmailAsync(Guid emailInvitationId)
	{
		return await _atsRepository.UpdateSingleEmailInvitationRequestStatusForSentEmailAsync(emailInvitationId);
	}

	public async Task<bool> UpdateBulkEmailInvitationRequestForNotSentEmailAsync(List<EmailInvitationRequest> emailInvitationRequests)
	{
		return await _atsRepository.UpdateBulkEmailInvitationRequestForNotSentEmailAsync(emailInvitationRequests);
	}

	public async Task<bool> UpdateSingleEmailInvitationRequestStatusForNotSentEmailAsync(Guid emailInvitationId)
	{
		return await _atsRepository.UpdateSingleEmailInvitationRequestStatusForNotSentEmailAsync(emailInvitationId);
	}

	public async Task<EmailInvitationRequest> GetEmailInvitationRequestByIdAsync(Guid emailInvitationId, CancellationToken cancellationToken)
	{
		return await _atsRepository.GetEmailInvitationRequestByIdAsync(emailInvitationId, cancellationToken);
	}

	// Single-row lookup used only for an access check on the way into a write — not
	// worth a cache entry, and stale scope data here would be a correctness problem.
	public async Task<EmailInvitationOwnerDTO?> GetEmailInvitationOwnerAsync(Guid emailInvitationId, CancellationToken cancellationToken)
	{
		return await _atsRepository.GetEmailInvitationOwnerAsync(emailInvitationId, cancellationToken);
	}

	// The subject name is rendered by the cached reports pages and by the dispute
	// and withdrawn lists, so every one of those tags is invalidated.
	public async Task<bool> UpdateSubjectNameAsync(EditSubjectNameDTO subjectName, CancellationToken cancellationToken)
	{
		var result = await _atsRepository.UpdateSubjectNameAsync(subjectName, cancellationToken);

		if (result)
		{
			await _hybridCache.RemoveByTagAsync(CacheTags.Report, cancellationToken);
			await _hybridCache.RemoveByTagAsync(CacheTags.DisputeOrder, cancellationToken);
			await _hybridCache.RemoveByTagAsync(CacheTags.WithdrawnApplication, cancellationToken);
		}

		return result;
	}

	public async Task<bool> ResendApplicationFormAsync(Guid emailInvitationId, string hashToken, DateTime hashTokenExpiration, CancellationToken cancellationToken)
	{
		var result = await _atsRepository.ResendApplicationFormAsync(emailInvitationId, hashToken, hashTokenExpiration, cancellationToken);

		if (result)
			await _hybridCache.RemoveByTagAsync(CacheTags.WithdrawnApplication);

		return result;
	}
}
