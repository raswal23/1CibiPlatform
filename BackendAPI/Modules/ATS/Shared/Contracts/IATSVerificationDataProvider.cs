namespace ATS.Shared.Contracts;

/// <summary>
/// One employment slot from a submitted application form, offered to the Employment
/// Verification module so it can contact that former employer.
/// </summary>
/// <param name="SubjectId">The ATS order id. Shared by all of an order's segments.</param>
/// <param name="EmploymentSegment">
/// Which of the form's three employer slots this is: 1, 2 or 3. Positional, not
/// chronological - it identifies the Emp1*/Emp2*/Emp3* column group the whole record
/// was read from, so the company, dates, position and supervisor all belong together.
/// </param>
/// <param name="SupervisorName">The supervisor the candidate named for this employer.</param>
/// <param name="SupervisorEmail">
/// The address the candidate supplied for that supervisor. Consumers may prefer a
/// vetted company mailbox over it; it is offered here as what the candidate stated.
/// </param>
/// <param name="PermissionToContact">
/// Whether the candidate agreed this employer may be contacted. False when they
/// declined, left it blank, or the stored value is not recognisably affirmative -
/// absence of consent is not consent.
/// </param>
/// <remarks>
/// One record per employment slot, not per order. The form stores all three employers
/// as Emp1*/Emp2*/Emp3* column groups on a single ats.ProfessionalExperiences row;
/// this contract unpivots them, because each employer is verified independently and
/// answers on its own schedule.
/// <para>
/// The supervisor fields used to be called HrName/HrEmail. They were renamed after
/// HrEmail was found to be returning the ATS requestor's address rather than anyone at
/// the former employer - a name that described the intended use rather than the actual
/// contents is what let that go unnoticed.
/// </para>
/// </remarks>
public sealed record ATSInProgressEmploymentRecord(
	Guid SubjectId,
	short EmploymentSegment,
	string CandidateName,
	string Employer,
	string? Position,
	DateOnly? StartDate,
	DateOnly? EndDate,
	string? SupervisorName,
	string? SupervisorEmail,
	bool PermissionToContact);

public interface IATSVerificationDataProvider
{
	/// <summary>
	/// Every employment slot of every in-progress order still awaiting hand-off to
	/// employment verification, one record per slot. Slots the candidate left empty
	/// are omitted, and orders already released with
	/// <see cref="ReleaseOrdersAsync"/> are not returned.
	/// </summary>
	Task<IReadOnlyList<ATSInProgressEmploymentRecord>> GetInProgressEmploymentAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Marks orders as no longer needing hand-off, so later passes stop reading them.
	/// </summary>
	/// <remarks>
	/// The consumer calls this once it holds a request for every slot the order offers.
	/// It records only that ATS is done handing the order over - what became of each
	/// request stays with the consumer.
	/// <para>
	/// Without it the query is proportional to the size of the order table rather than
	/// to the work outstanding: an order whose employers all answered a year ago would
	/// still be read, unpivoted and discarded on every pass, forever.
	/// </para>
	/// </remarks>
	Task ReleaseOrdersAsync(
		IReadOnlyCollection<Guid> subjectIds,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Puts orders back into the hand-off queue.
	/// </summary>
	/// <remarks>
	/// Needed because a released order can become actionable again - a verification
	/// link that lapses unanswered reopens its slot - and a released order is invisible
	/// to <see cref="GetInProgressEmploymentAsync"/> until it is reinstated.
	/// </remarks>
	Task ReinstateOrdersAsync(
		IReadOnlyCollection<Guid> subjectIds,
		CancellationToken cancellationToken = default);
}
