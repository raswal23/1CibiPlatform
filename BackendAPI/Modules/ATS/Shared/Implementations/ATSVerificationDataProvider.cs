namespace ATS.Shared.Implementations;

public sealed class ATSVerificationDataProvider(ATSDBContext db) : IATSVerificationDataProvider
{
	public async Task<IReadOnlyList<ATSInProgressEmploymentRecord>> GetInProgressEmploymentAsync(CancellationToken cancellationToken = default)
	{
		// Left join on the requestor: RequestorId is nullable, and an inner join here
		// silently dropped every bulk-upload and public-API order. The requestor is no
		// longer part of the result - it was only ever used to supply UserEmail, which
		// was the ATS recruiter rather than anyone at the former employer - but the
		// join is kept because the candidate's name still comes off the invitation.
		// NeedsEmploymentVerification is what keeps this query proportional to the work
		// rather than to the table. Without it every pass loaded every in-progress
		// order - a year-old order whose employers had all answered was still read,
		// unpivoted and discarded every five minutes, forever.
		//
		// Projected rather than selecting the entity: ProfessionalExperiences is 39
		// columns, of which 18 are used here. The rest - COE file keys, addresses,
		// contact numbers - were being pulled across the wire and thrown away.
		var orders = await (
			from invitation in db.EmailInvitationRequests.AsNoTracking()
			join employment in db.ProfessionalExperiences.AsNoTracking()
				on invitation.EmailInvitationID equals employment.EmailInvitationID
			where invitation.OrderStatus == OrderStatus.InProgress
				&& invitation.NeedsEmploymentVerification
			select new
			{
				invitation.EmailInvitationID,
				invitation.FirstName,
				invitation.MiddleInitial,
				invitation.LastName,

				employment.Emp1CompanyName,
				employment.Emp1JobTitle,
				employment.Emp1StartDate,
				employment.Emp1EndDate,
				employment.Emp1SupervisorName,
				employment.Emp1SupervisorEmail,
				employment.Emp1PermissionToContact,

				employment.Emp2CompanyName,
				employment.Emp2JobTitle,
				employment.Emp2StartDate,
				employment.Emp2EndDate,
				employment.Emp2SupervisorName,
				employment.Emp2SupervisorEmail,
				employment.Emp2PermissionToContact,

				employment.Emp3CompanyName,
				employment.Emp3JobTitle,
				employment.Emp3StartDate,
				employment.Emp3EndDate,
				employment.Emp3SupervisorName,
				employment.Emp3SupervisorEmail,
				employment.Emp3PermissionToContact
			})
			.ToListAsync(cancellationToken);

		var records = new List<ATSInProgressEmploymentRecord>(orders.Count);

		foreach (var order in orders)
		{
			var candidateName = string.Join(
				' ',
				new[] { order.FirstName, order.MiddleInitial, order.LastName }
					.Where(part => !string.IsNullOrWhiteSpace(part)));

			// Unpivoted in memory rather than as three UNIONed queries, matching the
			// report preview's AddEmployer in ATSRepository.Reports.cs. The row is
			// already materialised and the three slots are plain columns on it, so a
			// SQL unpivot would buy nothing and read far worse.
			void AddSegment(
				short segment,
				string? companyName,
				string? jobTitle,
				DateOnly? startDate,
				DateOnly? endDate,
				string? supervisorName,
				string? supervisorEmail,
				string? permissionToContact)
			{
				// An empty slot is not an employer. The candidate may have listed one
				// or three; only the ones they filled in are verifiable.
				if (string.IsNullOrWhiteSpace(companyName))
				{
					return;
				}

				records.Add(new ATSInProgressEmploymentRecord(
					SubjectId: order.EmailInvitationID,
					EmploymentSegment: segment,
					CandidateName: candidateName,
					Employer: companyName.Trim(),
					Position: jobTitle,
					StartDate: startDate,
					EndDate: endDate,
					SupervisorName: supervisorName,
					SupervisorEmail: supervisorEmail,
					PermissionToContact: AffirmativeAnswer.IsAffirmative(permissionToContact)));
			}

			AddSegment(1, order.Emp1CompanyName, order.Emp1JobTitle,
				order.Emp1StartDate, order.Emp1EndDate,
				order.Emp1SupervisorName, order.Emp1SupervisorEmail,
				order.Emp1PermissionToContact);

			AddSegment(2, order.Emp2CompanyName, order.Emp2JobTitle,
				order.Emp2StartDate, order.Emp2EndDate,
				order.Emp2SupervisorName, order.Emp2SupervisorEmail,
				order.Emp2PermissionToContact);

			AddSegment(3, order.Emp3CompanyName, order.Emp3JobTitle,
				order.Emp3StartDate, order.Emp3EndDate,
				order.Emp3SupervisorName, order.Emp3SupervisorEmail,
				order.Emp3PermissionToContact);
		}

		return records;
	}

	public Task ReleaseOrdersAsync(
		IReadOnlyCollection<Guid> subjectIds,
		CancellationToken cancellationToken = default) =>
		SetNeedsEmploymentVerificationAsync(subjectIds, false, cancellationToken);

	public Task ReinstateOrdersAsync(
		IReadOnlyCollection<Guid> subjectIds,
		CancellationToken cancellationToken = default) =>
		SetNeedsEmploymentVerificationAsync(subjectIds, true, cancellationToken);

	// ExecuteUpdateAsync rather than loading and saving: this touches one boolean on
	// rows the caller never materialised, and a pass may release a few hundred at once.
	private async Task SetNeedsEmploymentVerificationAsync(
		IReadOnlyCollection<Guid> subjectIds,
		bool needsVerification,
		CancellationToken cancellationToken)
	{
		if (subjectIds.Count == 0)
		{
			return;
		}

		await db.EmailInvitationRequests
			.Where(invitation => subjectIds.Contains(invitation.EmailInvitationID))
			.Where(invitation => invitation.NeedsEmploymentVerification != needsVerification)
			.ExecuteUpdateAsync(
				setters => setters.SetProperty(
					invitation => invitation.NeedsEmploymentVerification,
					needsVerification),
				cancellationToken);
	}
}
