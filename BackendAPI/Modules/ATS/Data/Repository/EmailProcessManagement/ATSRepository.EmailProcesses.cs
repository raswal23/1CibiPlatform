namespace ATS.Data.Repository;

public partial class ATSRepository
{
	// Ordered by process name rather than by id, so the screen's order does not depend on the
	// order the rows happened to be seeded or added in.
	public Task<List<EmailProcessDetailsDTO>> GetEmailProcessesAsync(CancellationToken cancellationToken) =>
		_dbcontext.EmailProcessDetails
			.AsNoTracking()
			.OrderBy(process => process.EmailProcess)
			.Select(process => new EmailProcessDetailsDTO
			{
				Id = process.Id,
				EmailProcess = process.EmailProcess,
				CCEmail = process.CCEmail,
				CreatedDate = process.CreatedDate,
				IsActive = process.IsActive
			})
			.ToListAsync(cancellationToken);

	// Tracked: the service mutates what comes back and hands it to EditEmailProcessAsync.
	public Task<EmailProcessDetails?> GetEmailProcessAsync(int id, CancellationToken cancellationToken) =>
		_dbcontext.EmailProcessDetails
			.FirstOrDefaultAsync(process => process.Id == id, cancellationToken);

	// ILike, not ==, for the reason on the contract: the unique index is case-sensitive and the
	// send path's lookup is not, so an exact match would miss "withdrawn" against "Withdrawn"
	// and let both rows exist.
	public Task<bool> EmailProcessExistsAsync(string emailProcess, CancellationToken cancellationToken) =>
		_dbcontext.EmailProcessDetails
			.AsNoTracking()
			.AnyAsync(process => EF.Functions.ILike(process.EmailProcess, emailProcess), cancellationToken);

	public async Task<EmailProcessDetails> AddEmailProcessAsync(
		EmailProcessDetails emailProcess,
		CancellationToken cancellationToken)
	{
		await _dbcontext.EmailProcessDetails.AddAsync(emailProcess, cancellationToken);
		await _dbcontext.SaveChangesAsync(cancellationToken);

		return emailProcess;
	}

	// No Update() call: the entity arrives tracked from GetEmailProcessAsync above, so the
	// change tracker already holds the modifications and re-attaching it would throw.
	public async Task<EmailProcessDetails> EditEmailProcessAsync(
		EmailProcessDetails emailProcess,
		CancellationToken cancellationToken)
	{
		await _dbcontext.SaveChangesAsync(cancellationToken);

		return emailProcess;
	}
}
