namespace ATS.Data.Repository;

public partial class ATSRepository
{
	// An invitation is retried until this many failed sends, then it stays Error for a
	// human to look at - a mistyped or dead address must not consume the daily quota
	// forever.
	private const int MaxEmailSendAttempts = 5;

	// Round-robin: each client may contribute at most this many invitations per tick, so
	// one large upload cannot block every other client behind it.
	private const int PerClientSliceSize = 50;

	public async Task<bool> AddEmailInvitationRequestAsync(EmailInvitationRequest emailInvitationRequest)
	{
		await _dbcontext.EmailInvitationRequests.AddAsync(emailInvitationRequest);
		await _dbcontext.SaveChangesAsync();
		return true;
	}

	public async Task<List<EmailInvitationRequest>> GetPendingEmailInvitationRequestsAsync()
	{
		// Claim and return in one statement. FOR UPDATE SKIP LOCKED lets a concurrent
		// worker step over rows another worker is already claiming instead of blocking,
		// and the Processing write is what keeps the claim after this transaction ends.
		// EF cannot express SKIP LOCKED, so this is raw SQL.
		//
		// "AutoChasing" IS TRUE: only manual-screening orders receive an application
		// form invitation. Data orders carry their identity fields from order entry
		// and must never be emailed; NULL (legacy rows) is deliberately excluded too,
		// because an unclassified order cannot prove it is manual.
		return await _dbcontext.EmailInvitationRequests
			.FromSqlRaw(
				"""
				WITH ranked AS (
					SELECT "EmailInvitationID",
						   ROW_NUMBER() OVER (
							   PARTITION BY "ClientId"
							   ORDER BY "OrderCreatedAt") AS rn
					FROM ats."EmailInvitationRequest"
					WHERE "AutoChasing" IS TRUE
					  AND ("EmailSentStatus" = {2}
						OR ("EmailSentStatus" = {3} AND "EmailSendAttempts" < {4}))
				)
				UPDATE ats."EmailInvitationRequest" t
				SET "EmailSentStatus" = {0},
					"EmailClaimedAt" = {1}
				WHERE t."EmailInvitationID" IN (
					SELECT e."EmailInvitationID"
					FROM ats."EmailInvitationRequest" e
					WHERE e."EmailInvitationID" IN (
						SELECT "EmailInvitationID" FROM ranked WHERE rn <= {5})
					ORDER BY e."OrderCreatedAt"
					LIMIT {6}
					FOR UPDATE SKIP LOCKED
				)
				RETURNING t.*;
				""",
				EmailStatus.Processing,
				DateTime.UtcNow,
				EmailStatus.Pending,
				EmailStatus.Error,
				MaxEmailSendAttempts,
				PerClientSliceSize,
				200)
			.AsNoTracking()
			.ToListAsync();
	}

	public async Task<bool> RequeueEmailInvitationAsync(
		Guid emailInvitationId,
		string hashToken,
		DateTime hashTokenExpiration,
		CancellationToken cancellationToken)
	{
		// Mirrors RequeueExhaustedTicketAsync. The predicate is the concurrency guard, not
		// just a lookup: matching on the current status inside the UPDATE means a row the
		// job has already claimed updates nothing, and the caller is told so. A
		// read-then-write would race and could resurrect a live claim.
		//
		// Processing is the one status excluded. That row is mid-send RIGHT NOW - a worker
		// is holding it in memory and is about to write its outcome, so re-issuing the token
		// here would both race that write and risk a second delivery.
		//
		// Pending is allowed even though the row is already queued: nothing has been sent,
		// so re-issuing the token duplicates no email, and refusing would give an operator a
		// confusing error for clicking resend twice.
		var updated = await _dbcontext.EmailInvitationRequests
			.Where(x => x.EmailInvitationID == emailInvitationId
					 && x.EmailSentStatus != EmailStatus.Processing)
			.ExecuteUpdateAsync(setters => setters
				.SetProperty(x => x.EmailSentStatus, x => EmailStatus.Pending)

				// The budget resets: whatever blocked delivery is expected to have been
				// fixed, so the job gets a full set of automatic attempts again. Without
				// this a retried row was still at the ceiling and the claim query skipped
				// it, so the retry silently did nothing.
				.SetProperty(x => x.EmailSendAttempts, x => 0)
				.SetProperty(x => x.EmailClaimedAt, x => null)
				.SetProperty(x => x.EmailSentAt, x => null)

				// The link is reissued in the same statement, so a queued row can never
				// carry a token the candidate was never told about.
				.SetProperty(x => x.HashToken, hashToken)
				.SetProperty(x => x.HashTokenCreatedAt, DateTime.UtcNow)
				.SetProperty(x => x.HashTokenExpiration, hashTokenExpiration)
				.SetProperty(x => x.OrderStatus, OrderStatus.PendingCandidateInfo)
				.SetProperty(x => x.ApplicationFormStatus, ApplicationFormStatus.Pending),
				cancellationToken);

		return updated > 0;
	}

	public async Task<int> RequeueEmailInvitationsAsync(
		IReadOnlyCollection<EmailInvitationRequeueDTO> requeues,
		CancellationToken cancellationToken)
	{
		if (requeues.Count == 0)
		{
			return 0;
		}

		// One UPDATE per row rather than one for the set, because each invitation needs its
		// OWN freshly generated token - a shared token would let any candidate in the batch
		// open another candidate's form. They run inside the caller's transaction, so the
		// batch still commits or rolls back as a unit.
		//
		// The predicate matches RequeueEmailInvitationAsync: a row the job is actively
		// sending (Processing) is skipped rather than raced, and the returned count reflects
		// what actually moved.
		var requeued = 0;

		foreach (var requeue in requeues)
		{
			var updated = await _dbcontext.EmailInvitationRequests
				.Where(x => x.EmailInvitationID == requeue.EmailInvitationId
						 && x.EmailSentStatus != EmailStatus.Processing)
				.ExecuteUpdateAsync(setters => setters
					.SetProperty(x => x.EmailSentStatus, x => EmailStatus.Pending)
					.SetProperty(x => x.EmailSendAttempts, x => 0)
					.SetProperty(x => x.EmailClaimedAt, x => null)
					.SetProperty(x => x.EmailSentAt, x => null)
					.SetProperty(x => x.HashToken, requeue.HashToken)
					.SetProperty(x => x.HashTokenCreatedAt, DateTime.UtcNow)
					.SetProperty(x => x.HashTokenExpiration, requeue.HashTokenExpiration)
					.SetProperty(x => x.OrderStatus, OrderStatus.PendingCandidateInfo)
					.SetProperty(x => x.ApplicationFormStatus, ApplicationFormStatus.Pending),
					cancellationToken);

			requeued += updated;
		}

		return requeued;
	}

	public async Task<List<EmailInvitationOwnerDTO>> GetEmailInvitationOwnersAsync(
		IReadOnlyCollection<Guid> emailInvitationIds,
		CancellationToken cancellationToken)
	{
		if (emailInvitationIds.Count == 0)
		{
			return [];
		}

		var ids = emailInvitationIds.ToList();

		// Read before the update so the caller's scope can be enforced per row. A bulk
		// action must not become a way to touch another client's invitations by posting
		// their ids alongside your own.
		return await _dbcontext.EmailInvitationRequests
			.AsNoTracking()
			.Where(eir => ids.Contains(eir.EmailInvitationID))
			.Select(eir => new EmailInvitationOwnerDTO
			{
				EmailInvitationID = eir.EmailInvitationID,
				ClientId = eir.ClientId,
				RequestorId = eir.RequestorId
			})
			.ToListAsync(cancellationToken);
	}

	public async Task<int> ReleaseEmailInvitationClaimsAsync(List<EmailInvitationRequest> emailInvitationRequests)
	{
		// Deliberately does NOT touch EmailSendAttempts. These rows were claimed but never
		// offered to the SMTP server - the pass stood down because the provider was rate
		// limiting. Charging them an attempt would retire a perfectly valid address after
		// five throttles without a single real delivery failure.
		var ids = emailInvitationRequests.Select(x => x.EmailInvitationID).ToList();

		return await _dbcontext.EmailInvitationRequests
			.Where(x => ids.Contains(x.EmailInvitationID))
			.ExecuteUpdateAsync(setters => setters
				.SetProperty(x => x.EmailSentStatus, x => EmailStatus.Pending)
				.SetProperty(x => x.EmailClaimedAt, x => null));
	}

	public async Task<int> ReleaseStaleEmailInvitationClaimsAsync(TimeSpan staleAfter)
	{
		// A crash mid-send leaves rows stuck in Processing with no live worker. Anything
		// claimed longer ago than staleAfter goes back to Pending for the next tick.
		var cutoff = DateTime.UtcNow.Subtract(staleAfter);

		return await _dbcontext.EmailInvitationRequests
			.Where(x => x.EmailSentStatus == EmailStatus.Processing
					 && x.EmailClaimedAt != null
					 && x.EmailClaimedAt < cutoff)
			.ExecuteUpdateAsync(setters => setters
				.SetProperty(x => x.EmailSentStatus, x => EmailStatus.Pending)
				.SetProperty(x => x.EmailClaimedAt, x => null));
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
			.SetProperty(x => x.EmailSentStatus, x => EmailStatus.Error)
			.SetProperty(x => x.EmailClaimedAt, x => null)
			.SetProperty(x => x.EmailSendAttempts, x => x.EmailSendAttempts + 1));

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

	public async Task<EmailInvitationRequest> GetEmailInvitationRequestByIdAsync(Guid emailInvitationId, CancellationToken cancellationToken)
	{
		return await _dbcontext.EmailInvitationRequests
			.AsNoTracking()
			.FirstOrDefaultAsync(eir => eir.EmailInvitationID == emailInvitationId, cancellationToken) ?? new EmailInvitationRequest();
	}

	public async Task<EmailInvitationOwnerDTO?> GetEmailInvitationOwnerAsync(Guid emailInvitationId, CancellationToken cancellationToken)
	{
		return await _dbcontext.EmailInvitationRequests
			.AsNoTracking()
			.Where(eir => eir.EmailInvitationID == emailInvitationId)
			.Select(eir => new EmailInvitationOwnerDTO
			{
				EmailInvitationID = eir.EmailInvitationID,
				ClientId = eir.ClientId,
				RequestorId = eir.RequestorId
			})
			.FirstOrDefaultAsync(cancellationToken);
	}

	// NeedsProjection is raised so the ApplicantSearchProjectionJob refreshes the
	// denormalized search row with the corrected name on its next pass.
	public async Task<bool> UpdateSubjectNameAsync(EditSubjectNameDTO subjectName, CancellationToken cancellationToken)
	{
		var affectedRows = await _dbcontext.EmailInvitationRequests
			.Where(eir => eir.EmailInvitationID == subjectName.EmailInvitationRequestId)
			.ExecuteUpdateAsync(setters => setters
				.SetProperty(eir => eir.FirstName, subjectName.FirstName)
				.SetProperty(eir => eir.MiddleInitial, subjectName.MiddleInitial)
				.SetProperty(eir => eir.LastName, subjectName.LastName)
				.SetProperty(eir => eir.NeedsProjection, true),
				cancellationToken);

		return affectedRows > 0;
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
