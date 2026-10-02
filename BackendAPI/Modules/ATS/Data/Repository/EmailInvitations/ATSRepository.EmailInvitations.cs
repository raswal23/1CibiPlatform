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
					WHERE ("AutoChasing" IS TRUE)
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
				// carry a token the candidate was never told about. Note that rotating it
				// RETIRES the link in the candidate's earlier email - the operator-forced
				// resend intends that. The follow-up reminder does not; see
				// ReleaseDueFollowUpInvitationsAsync.
				.SetProperty(x => x.HashToken, hashToken)
				.SetProperty(x => x.HashTokenCreatedAt, DateTime.UtcNow)
				.SetProperty(x => x.OrderStatus, OrderStatus.PendingCandidateInfo)
				.SetProperty(x => x.ApplicationFormStatus, ApplicationFormStatus.Pending),
				cancellationToken);

		return updated > 0;
	}

	// Each pass releases at most this many reminders, the same ceiling the claim query
	// uses. The unit is days, so a backlog draining over a few hourly passes is fine -
	// and it keeps one enormous client from filling the send queue in a single tick.
	private const int MaxFollowUpReleasePerPass = 200;

	// Every date and time-of-day comparison in the release below resolves in Manila, not UTC.
	//
	// This is a correctness requirement, not a preference. Reminders are anchored to the
	// order's time of day, so a UTC-dated dedupe double-sends for any order created between
	// midnight and 8am Manila: an order at Sep 16 06:00 +08 (= Sep 15 22:00Z) is released at
	// Sep 16 22:00Z, stamped with the UTC date Sep 16, and then released AGAIN two hours
	// later at Sep 17 00:00Z because the UTC date rolled over in the middle of the Manila
	// day. Dating in Manila has no such window - consecutive releases are always a full local
	// day apart.
	//
	// Postgres ships its own tzdata, so the name resolves regardless of the container's
	// locale. FollowUpSchedule.Id is shared with ReportService, which counts the reminders
	// still to come and has to measure days the same way this does.
	private const string FollowUpTimeZone = FollowUpSchedule.Id;

	// How long past its nominal schedule an order may still be caught up.
	//
	// The stop condition is FollowUpSentCount, not elapsed time, so this is no longer what ends
	// the schedule - it is the backstop that keeps it bounded. Without an upper limit an order
	// that was never chased stays eligible forever, and the daily-reminder migration ships no
	// backfill precisely because the window was what protected the existing backlog (see
	// docs/ats-package-follow-up-email.md). Anything older than its package's count plus this
	// grace is permanently ineligible, whatever its count says.
	//
	// Seven days is wide enough to absorb a realistic outage - a send queue backed up over a
	// long weekend - without letting a months-old order suddenly start chasing.
	private const int FollowUpCatchUpGraceDays = 7;

	public async Task<List<EmailInvitationRequest>> ReleaseDueFollowUpInvitationsAsync(CancellationToken cancellationToken)
	{
		// Puts an already-sent invitation back on the email queue as the package's
		// follow-up reminder, and stamps LastFollowUpSentDate in the SAME statement.
		//
		// The reminder repeats DAILY: PackageDetails.FollowUpEmail is the number of daily
		// reminders, not a one-off delay. A package set to 2 chases the candidate on day 1
		// and again on day 2, then stops. Each send is anchored to the order's own time of
		// day, so an order placed at 8am is chased at ~8am, once every 24 hours.
		//
		// Unlike RequeueEmailInvitationAsync this leaves "HashToken" and
		// "HashTokenCreatedAt" ALONE, deliberately. The reminder points the candidate at
		// the link they were already sent; rotating the token would silently kill the URL
		// sitting in their inbox, which is the opposite of what a chaser is for.
		//
		// Raw SQL because the join to PackageDetails (for FollowUpEmail, which is the
		// per-package reminder count) and FOR UPDATE SKIP LOCKED are both outside what
		// ExecuteUpdateAsync can express.
		//
		// The predicate, clause by clause:
		//   AutoChasing IS TRUE  - the same rule the claim query applies. Data orders are
		//                          never emailed at all, and NULL cannot prove it is manual.
		//   FollowUpEmail > 0    - 0 is the package's "off" switch, per the form's own hint.
		//   ApplicationFormStatus Pending - never chase a form already submitted or withdrawn.
		//   EmailSentStatus Done - only chase someone who actually received the first email;
		//                          a row still queued or erroring is the sender's problem.
		//   LastFollowUpSentDate <> today - the once-per-day guard. The job runs hourly, so
		//                          without it a due row would be released on all 24 passes.
		//                          IS DISTINCT FROM, not <>: a NULL (never chased) must pass.
		//   day 1 has arrived    - now() is at or past the order's time of day, one day on.
		//   FollowUpSentCount < FollowUpEmail - the stop condition: N reminders SENT.
		//   within the grace window - the backstop that keeps the schedule bounded.
		//
		// The count, not the calendar, is what ends the schedule. Reminders are only released
		// for rows meeting every rule above, so a day can pass with nothing sent - the first
		// invitation still queued behind the send quota, or failed. A purely time-bounded stop
		// therefore cut the schedule short for exactly the candidates who had received the
		// least, and left the board's "Follow-ups Left" promising sends that could no longer
		// happen. ReportService.CalculateFollowUpEmailsRemaining subtracts this same count.
		//
		// The window remains, widened by FollowUpCatchUpGraceDays, because it is still the only
		// thing bounding the backlog - see that constant. The day-1 lower bound and the
		// time-of-day compare are what make this "every 24 hours" rather than "at midnight".
		// All of it runs in Manila (see FollowUpTimeZone) so the calendar and the clock agree
		// with the candidate's day.
		//
		// FromSqlRaw with RETURNING t.*, matching GetPendingEmailInvitationRequestsAsync:
		// the caller needs the released rows' OrderStatus to write order history, and
		// reading them back separately would race the very rows this just moved.
		return await _dbcontext.EmailInvitationRequests
			.FromSqlRaw(
				"""
				WITH due AS (
					SELECT eir."EmailInvitationID"
					FROM ats."EmailInvitationRequest" eir
					JOIN ats."PackageDetails" pd ON pd."PackageId" = eir."PackageId"
					WHERE eir."AutoChasing" IS TRUE
					  AND pd."FollowUpEmail" > 0
					  AND eir."ApplicationFormStatus" = {0}
					  AND eir."EmailSentStatus" = {1}
					  AND eir."HashToken" IS NOT NULL
					  AND eir."OrderCreatedAt" IS NOT NULL
					  AND eir."LastFollowUpSentDate"
						  IS DISTINCT FROM (now() AT TIME ZONE {4})::date
					  AND (now() AT TIME ZONE {4})
						  >= (eir."OrderCreatedAt" AT TIME ZONE {4}) + interval '1 day'
					  AND eir."FollowUpSentCount" < pd."FollowUpEmail"
					  AND (now() AT TIME ZONE {4})
						  <  (eir."OrderCreatedAt" AT TIME ZONE {4})
							 + make_interval(days => pd."FollowUpEmail" + {5})
					ORDER BY eir."OrderCreatedAt"
					LIMIT {2}
					FOR UPDATE OF eir SKIP LOCKED
				)
				UPDATE ats."EmailInvitationRequest" t
				SET "EmailSentStatus" = {3},
					"EmailSendAttempts" = 0,
					"EmailClaimedAt" = NULL,
					"EmailSentAt" = NULL,
					"LastFollowUpSentDate" = (now() AT TIME ZONE {4})::date,
					"FollowUpSentCount" = t."FollowUpSentCount" + 1
				WHERE t."EmailInvitationID" IN (SELECT "EmailInvitationID" FROM due)
				RETURNING t.*;
				""",
				ApplicationFormStatus.Pending,
				EmailStatus.Done,
				MaxFollowUpReleasePerPass,
				EmailStatus.Pending,
				FollowUpTimeZone,
				FollowUpCatchUpGraceDays + 1)
			.AsNoTracking()
			.ToListAsync(cancellationToken);
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

}
