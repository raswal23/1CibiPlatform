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

	public async Task<bool> ResendApplicationFormAsync(Guid emailInvitationId, string hashToken, DateTime hashTokenExpiration, CancellationToken cancellationToken)
	{
		var result = await _atsRepository.ResendApplicationFormAsync(emailInvitationId, hashToken, hashTokenExpiration, cancellationToken);

		if (result)
			await _hybridCache.RemoveByTagAsync(CacheTags.WithdrawnApplication);

		return result;
	}
}
