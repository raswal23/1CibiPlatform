namespace EmploymentVerification.DTO;

/// <summary>
/// Outcome of an HR contact answering a verification request.
/// </summary>
public enum CompletionStatus
{
	/// <summary>The response was recorded on this request.</summary>
	Completed,

	/// <summary>No request carries this token.</summary>
	NotFound,

	/// <summary>The request exists but the link expired before it was answered.</summary>
	Expired,

	/// <summary>The link was already answered; the earlier response stands.</summary>
	AlreadyCompleted
}

/// <summary>
/// Result of a verify or reject command. <see cref="Request"/> carries the request
/// in its post-response state for <see cref="CompletionStatus.Completed"/> and
/// <see cref="CompletionStatus.AlreadyCompleted"/>.
/// </summary>
public sealed record EmploymentVerificationCompletionResult(
	CompletionStatus Status,
	EmploymentVerificationPreviewDTO? Request)
{
	/// <summary>
	/// The response was recorded. Carries the request in its post-response state.
	/// </summary>
	public static EmploymentVerificationCompletionResult Completed(
		EmploymentVerificationPreviewDTO request)
	{
		return new EmploymentVerificationCompletionResult(
			CompletionStatus.Completed,
			request);
	}

	/// <summary>
	/// The link was already answered. Carries the request so the earlier
	/// response can still be shown.
	/// </summary>
	public static EmploymentVerificationCompletionResult AlreadyCompleted(
		EmploymentVerificationPreviewDTO request)
	{
		return new EmploymentVerificationCompletionResult(
			CompletionStatus.AlreadyCompleted,
			request);
	}

	/// <summary>
	/// No request carries this token. No details are exposed.
	/// </summary>
	public static EmploymentVerificationCompletionResult NotFound()
	{
		return new EmploymentVerificationCompletionResult(
			CompletionStatus.NotFound,
			null);
	}

	/// <summary>
	/// The link expired before it was answered. No details are exposed.
	/// </summary>
	public static EmploymentVerificationCompletionResult Expired()
	{
		return new EmploymentVerificationCompletionResult(
			CompletionStatus.Expired,
			null);
	}
}
