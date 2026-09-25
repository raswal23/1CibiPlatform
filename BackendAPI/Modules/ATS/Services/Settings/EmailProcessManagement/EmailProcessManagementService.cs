namespace ATS.Services.Settings.EmailProcessManagement;

public class EmailProcessManagementService : IEmailProcessManagementService
{
	private readonly IEmailProcessRepository _emailProcessRepository;
	private readonly ILogger<EmailProcessManagementService> _logger;

	public EmailProcessManagementService(
		IEmailProcessRepository emailProcessRepository,
		ILogger<EmailProcessManagementService> logger)
	{
		_emailProcessRepository = emailProcessRepository;
		_logger = logger;
	}

	public async Task<IReadOnlyList<EmailProcessDetailsDTO>> GetEmailProcessesAsync(
		CancellationToken cancellationToken)
	{
		var logContext = new
		{
			Action = "GetEmailProcesses",
			Step = "FetchingEmailProcesses",
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("Fetching email process copy lists: {@Context}", logContext);

		return await _emailProcessRepository.GetEmailProcessesAsync(cancellationToken);
	}

	/// <inheritdoc/>
	public async Task<IReadOnlyList<string>> GetCopyListAsync(
		string emailProcess,
		CancellationToken cancellationToken)
	{
		// Reads the WHOLE table rather than one row, because that read is the one the cache
		// decorator holds under a single key - a by-process query would miss the cache and put a
		// database round trip on every notice. Five rows filtered in memory is cheaper than the
		// query that avoids it.
		//
		// Through SideEffectGuard, and NOT through GetEmailProcessesAsync above: that method is the
		// console's, and its job is to fail loudly. This one is a send path's and must not fail at
		// all. The fallback is null, which the null-coalesce turns into the same empty list every
		// other failure here produces.
		var emailProcesses = await SideEffectGuard.RunAsync(
			() => _emailProcessRepository.GetEmailProcessesAsync(cancellationToken),
			_logger,
			$"read the copy list for the {emailProcess} notice (it is sent to its recipient either way)",
			fallback: null,
			cancellationToken) ?? [];

		// Case-insensitive for the same reason EmailProcessExistsAsync is: the unique index is
		// case-sensitive, so a row hand-inserted as "withdrawn" is a different row to PostgreSQL
		// and the same notice to this lookup. Matching loosely here means such a row is used
		// rather than silently ignored.
		var match = emailProcesses.FirstOrDefault(process =>
			string.Equals(process.EmailProcess, emailProcess, StringComparison.OrdinalIgnoreCase));

		if (match is null)
		{
			// Warning, not silence: every value in AtsEmailProcess.All is seeded, so a missing row
			// means the seed did not run or somebody deleted it. The notice still goes out.
			_logger.LogWarning(
				"No copy list row exists for the {EmailProcess} notice, so nobody was copied on it.",
				emailProcess);

			return [];
		}

		if (!match.IsActive)
		{
			// Information, not a warning: an inactive row is an operator's decision, not a fault.
			_logger.LogInformation(
				"The copy list for the {EmailProcess} notice is switched off, so nobody was copied on it.",
				emailProcess);

			return [];
		}

		// Split rather than read raw: the column is hand-editable and the send path is the thing
		// that pays for stray whitespace - MailboxAddress.Parse throws for the whole notice, not
		// for the one bad fragment.
		return EmailCopyList.Split(match.CCEmail);
	}

	public async Task<EmailProcessDetailsDTO> AddEmailProcessAsync(
		AddEmailProcessDTO emailProcessDTO,
		CancellationToken cancellationToken)
	{
		var emailProcess = emailProcessDTO.EmailProcess.Trim();

		var logContext = new
		{
			Action = "AddEmailProcess",
			Step = "CreatingEmailProcess",
			EmailProcess = emailProcess,
			Timestamp = DateTime.UtcNow
		};

		_logger.LogInformation("Adding email process copy list: {@Context}", logContext);

		// Check-then-write. The unique index is still the real guarantee - two simultaneous
		// adds of the same process both pass this and the loser dies on the constraint - but
		// this turns the ordinary case, an operator adding a process that is already listed,
		// into a message the screen can render instead of a 500.
		if (await _emailProcessRepository.EmailProcessExistsAsync(emailProcess, cancellationToken))
		{
			_logger.LogWarning(
				"{EmailProcess} already has a copy list: {@Context}",
				emailProcess,
				logContext);

			throw new BadRequestException($"A copy list for '{emailProcess}' already exists. Edit that one instead.");
		}

		var copyList = EmailCopyList.Normalize(emailProcessDTO.CCEmail);

		var added = await _emailProcessRepository.AddEmailProcessAsync(
			new EmailProcessDetails
			{
				EmailProcess = emailProcess,
				CCEmail = copyList,
				CreatedDate = DateTime.UtcNow,
				IsActive = emailProcessDTO.IsActive
			},
			cancellationToken);

		return added.Adapt<EmailProcessDetailsDTO>();
	}

	public async Task<EmailProcessDetailsDTO> EditEmailProcessAsync(
		EditEmailProcessDTO emailProcessDTO,
		CancellationToken cancellationToken)
	{
		var logContext = new
		{
			Action = "EditEmailProcess",
			Step = "FetchForUpdate",
			Id = emailProcessDTO.Id,
			Timestamp = DateTime.UtcNow
		};

		var existingEmailProcess = await _emailProcessRepository.GetEmailProcessAsync(
			emailProcessDTO.Id,
			cancellationToken);

		if (existingEmailProcess is null)
		{
			_logger.LogError(
				"{Id} was not found during update operation: {@Context}",
				emailProcessDTO.Id,
				logContext);

			throw new NotFoundException($"Email process with ID {emailProcessDTO.Id} was not found.");
		}

		// EmailProcess and CreatedDate are deliberately not touched. The process is what the
		// send path matches on and retyping it would silently move the list to another notice
		// - see the remark on EditEmailProcessDTO. The creation date is the row's, not the
		// caller's.
		existingEmailProcess.CCEmail = EmailCopyList.Normalize(emailProcessDTO.CCEmail);
		existingEmailProcess.IsActive = emailProcessDTO.IsActive;

		var edited = await _emailProcessRepository.EditEmailProcessAsync(
			existingEmailProcess,
			cancellationToken);

		_logger.LogInformation(
			"Updated the copy list for {EmailProcess}: {@Context}",
			edited.EmailProcess,
			logContext);

		return edited.Adapt<EmailProcessDetailsDTO>();
	}
}
