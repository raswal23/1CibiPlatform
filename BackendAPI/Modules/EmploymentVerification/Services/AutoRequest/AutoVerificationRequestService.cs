namespace EmploymentVerification.Services.AutoRequest;

public sealed class AutoVerificationRequestService(
	IEmploymentVerificationService verificationService,
	IContactDirectoryRepository contactRepository,
	IConfiguration configuration,
	ILogger<AutoVerificationRequestService> logger)
	: IAutoVerificationRequestService
{
	// Bounds one pass. The first run after deployment sees every in-progress order at
	// once, and the mailbox pool is rate limited - uncapped, that pass would spend its
	// quota on a backlog and starve the ordinary invitation traffic sharing it.
	private readonly int _maxPerPass = configuration
		.GetSection("EmailVerification")
		.GetValue("AutoSendMaxPerPass", 200);

	public async Task<AutoVerificationRequestResult> SendDueRequestsAsync(
		CancellationToken cancellationToken)
	{
		// Before reading: put back any order whose link has since lapsed. A released
		// order is invisible to the query below, so without this a Sent request that
		// expires unanswered could never be retried - the segment would reopen in this
		// module while ATS still considered the order handed over.
		await verificationService.ReinstateLapsedOrdersAsync(cancellationToken);

		// Already filtered to slots with no live request, per (subject, segment).
		var available = await verificationService.GetAvailableATSRecordsAsync(cancellationToken);

		if (available.Count == 0)
		{
			return new AutoVerificationRequestResult(0, 0, 0, 0, 0, false);
		}

		// Consent first, before any lookup: a slot the candidate did not agree to is
		// not a candidate for sending, so there is nothing to resolve for it.
		var consented = new List<ATSInProgressEmploymentRecord>(available.Count);
		var skippedNoConsent = 0;

		foreach (var record in available)
		{
			if (record.PermissionToContact)
			{
				consented.Add(record);
			}
			else
			{
				skippedNoConsent++;
			}
		}

		if (consented.Count == 0)
		{
			return new AutoVerificationRequestResult(
				Eligible: available.Count,
				Sent: 0,
				SkippedNoConsent: skippedNoConsent,
				SkippedNoRecipient: 0,
				Failed: 0,
				Truncated: false);
		}

		var truncated = consented.Count > _maxPerPass;

		if (truncated)
		{
			// Said out loud rather than silently dropped: a capped pass looks identical
			// to a complete one in the logs otherwise, and the remainder only moves on
			// the next tick.
			logger.LogInformation(
				"Employment verification auto-send capped at {Cap} of {Eligible} eligible segments; the rest follow next pass.",
				_maxPerPass,
				consented.Count);

			consented = consented.Take(_maxPerPass).ToList();
		}

		// The directory is an allow-list of addresses, checked in one batch. A supervisor
		// address it does not list is not written to.
		var knownMailboxes = await contactRepository.GetKnownActiveMailboxesAsync(
			consented
				.Select(record => record.SupervisorEmail)
				.Where(email => !string.IsNullOrWhiteSpace(email))
				.Select(email => email!)
				.ToList(),
			cancellationToken);

		var sent = 0;
		var skippedNoRecipient = 0;
		var failed = 0;

		// Orders that still have something outstanding after this pass, so they must
		// stay in the hand-off queue. A segment skipped for a missing or unlisted
		// address is exactly that: an operator may add the contact tomorrow, and the
		// order has to still be visible when they do.
		var unfinished = new HashSet<Guid>();

		foreach (var record in available)
		{
			if (!record.PermissionToContact)
			{
				// Declined consent is settled, not outstanding - it will never become
				// sendable, so it must not hold the order in the queue forever.
				continue;
			}

			if (!consented.Contains(record))
			{
				// Trimmed by the per-pass cap; still owed a send.
				unfinished.Add(record.SubjectId);
			}
		}

		foreach (var record in consented)
		{
			var recipient = ResolveRecipient(record, knownMailboxes);

			if (recipient is null)
			{
				skippedNoRecipient++;
				unfinished.Add(record.SubjectId);
				continue;
			}

			// Caught per item: this is a job, and the house rule is that jobs catch
			// where feature code throws. One unreachable mailbox must not abandon the
			// rest of the pass. CreateAndSendAsync already releases the segment on a
			// send failure, so a caught item is retried next tick rather than lost.
			try
			{
				await verificationService.CreateAndSendAsync(
					new CreateEmploymentVerificationRequest(
						CandidateName: record.CandidateName,
						PreviousEmployer: record.Employer,
						Position: string.IsNullOrWhiteSpace(record.Position)
							? "Not provided"
							: record.Position,
						HrEmail: recipient.Value.Address,
						EmploymentStartDate: ToDateTime(record.StartDate),
						EmploymentEndDate: ToDateTime(record.EndDate),
						AtsSubjectId: record.SubjectId,
						EmploymentSegment: record.EmploymentSegment,
						RecipientSource: recipient.Value.Source.ToString()),
					cancellationToken);

				sent++;

				logger.LogInformation(
					"Sent employment verification for subject {SubjectId} segment {Segment} to a {Source} address.",
					record.SubjectId,
					record.EmploymentSegment,
					recipient.Value.Source);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				throw;
			}
			catch (Exception exception)
			{
				failed++;
				unfinished.Add(record.SubjectId);

				logger.LogError(
					exception,
					"Failed to send employment verification for subject {SubjectId} segment {Segment}.",
					record.SubjectId,
					record.EmploymentSegment);
			}
		}

		// Hand the finished orders back. An order is finished when this pass left it
		// with nothing outstanding - every slot either sent, or settled as
		// un-sendable because the candidate declined consent.
		//
		// Releasing is what keeps the next pass cheap; releasing too eagerly would
		// strand real work, which is why anything merely deferred - capped, awaiting a
		// contact, or failed - holds its order in the queue.
		var finished = available
			.Select(record => record.SubjectId)
			.Distinct()
			.Where(subjectId => !unfinished.Contains(subjectId))
			.ToList();

		await verificationService.ReleaseFinishedOrdersAsync(finished, cancellationToken);

		return new AutoVerificationRequestResult(
			Eligible: available.Count,
			Sent: sent,
			SkippedNoConsent: skippedNoConsent,
			SkippedNoRecipient: skippedNoRecipient,
			Failed: failed,
			Truncated: truncated);
	}

	/// <summary>
	/// The supervisor address from the form, but only if the contact directory lists it.
	/// Returns null when there is no address or the directory does not know it.
	/// </summary>
	/// <remarks>
	/// The directory is an allow-list, not a preference: a candidate supplies the
	/// supervisor address on their own application form, so writing to an unlisted one
	/// would let them nominate who verifies their own employment history. An unknown
	/// address is left for an operator to look at rather than mailed.
	/// </remarks>
	private static (string Address, VerificationRecipientSource Source)? ResolveRecipient(
		ATSInProgressEmploymentRecord record,
		IReadOnlySet<string> knownMailboxes)
	{
		if (string.IsNullOrWhiteSpace(record.SupervisorEmail))
		{
			return null;
		}

		var address = record.SupervisorEmail.Trim();

		if (!knownMailboxes.Contains(address.ToLowerInvariant()))
		{
			return null;
		}

		return (address, VerificationRecipientSource.Directory);
	}

	private static DateTime? ToDateTime(DateOnly? value) =>
		value?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
}
