namespace EmploymentVerification.DTO;

/// <summary>
/// Why a preview lookup succeeded or failed. The anonymous page renders a distinct
/// message per outcome, so the reason must survive the trip out of the service.
/// </summary>
public enum PreviewTokenStatus
{
	/// <summary>Token matched a live request that is still actionable.</summary>
	Valid,

	/// <summary>No request carries this token hash.</summary>
	NotFound,

	/// <summary>The request exists but <c>TokenExpiresAt</c> has passed.</summary>
	Expired,

	/// <summary>The request was already verified or rejected; the link is single-use.</summary>
	AlreadyCompleted
}

/// <summary>
/// Outcome of a token preview lookup. <see cref="Request"/> is populated only when
/// <see cref="Status"/> is <see cref="PreviewTokenStatus.Valid"/>.
/// </summary>
public sealed record EmploymentVerificationPreviewResult(
	PreviewTokenStatus Status,
	EmploymentVerificationPreviewDTO? Request)
{
	public static EmploymentVerificationPreviewResult Valid(
		EmploymentVerificationPreviewDTO request) =>
		new(PreviewTokenStatus.Valid, request);

	public static EmploymentVerificationPreviewResult NotFound() =>
		new(PreviewTokenStatus.NotFound, null);

	public static EmploymentVerificationPreviewResult Expired() =>
		new(PreviewTokenStatus.Expired, null);

	public static EmploymentVerificationPreviewResult AlreadyCompleted() =>
		new(PreviewTokenStatus.AlreadyCompleted, null);
}
