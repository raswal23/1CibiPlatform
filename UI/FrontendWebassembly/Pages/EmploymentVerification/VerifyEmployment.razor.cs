using FrontendWebassembly.DTO.EmploymentVerification;

namespace FrontendWebassembly.Pages.EmploymentVerification;

public partial class VerifyEmployment : IDisposable
{
	private readonly CancellationTokenSource _cancellation = new();

	private EmploymentVerificationPreviewDTO? _request;
	private bool _isLoading = true;
	private bool _isSubmitting;
	private bool _completed;
	private bool _wasRejected;
	private string _errorMessage = "";
	private VerificationLinkFailure _failure = VerificationLinkFailure.None;

	[Parameter]
	public string Token { get; set; } = "";

	/// <summary>
	/// True when the link expired before the HR contact answered it. The page then
	/// shows the expiry state instead of the confirmation actions.
	/// </summary>
	private bool IsExpired =>
		_failure == VerificationLinkFailure.Expired;

	/// <summary>
	/// True when the link was already answered. The earlier response stands.
	/// </summary>
	private bool IsAlreadyUsed =>
		_failure == VerificationLinkFailure.AlreadyUsed;

	private bool HasGenericError =>
		_failure is VerificationLinkFailure.NotFound or VerificationLinkFailure.Unknown;

	private string ResultTitle =>
		_wasRejected
			? "Response recorded"
			: "Employment confirmed";

	private string ResultMessage =>
		_wasRejected
			? "Thank you. We have recorded that these details are not accurate, and the CIBI team will follow up."
			: "Thank you. Your confirmation has been securely recorded.";

	private string EmploymentPeriod
	{
		get
		{
			if (_request?.EmploymentStartDate is null && _request?.EmploymentEndDate is null)
			{
				return "Not provided";
			}

			var start = _request?.EmploymentStartDate?.ToString("MMM yyyy") ?? "Not provided";
			var end = _request?.EmploymentEndDate?.ToString("MMM yyyy") ?? "Present";

			return $"{start} – {end}";
		}
	}

	protected override async Task OnParametersSetAsync()
	{
		await LoadPreviewAsync();
	}

	/// <summary>
	/// Validates the emailed token server side and loads the request details.
	/// Expired, already-answered, and unknown links are reported through
	/// <see cref="VerificationLinkFailure"/> so each gets its own state.
	/// </summary>
	private async Task LoadPreviewAsync()
	{
		_isLoading = true;
		_errorMessage = "";
		_failure = VerificationLinkFailure.None;
		_completed = false;

		try
		{
			var result = await VerificationService.GetPreviewAsync(
				Token,
				_cancellation.Token);

			if (result.Failure != VerificationLinkFailure.None)
			{
				_failure = result.Failure;
				_errorMessage = result.ErrorMessage;
				return;
			}

			_request = result.Data;

			if (_request is null)
			{
				_failure = VerificationLinkFailure.Unknown;
				_errorMessage = "This verification link could not be opened.";
			}
		}
		catch (OperationCanceledException)
		{
			// The page was disposed while the request was in flight.
		}
		catch (Exception exception)
		{
			_failure = VerificationLinkFailure.Unknown;
			_errorMessage = exception.Message;
		}
		finally
		{
			_isLoading = false;
		}
	}

	/// <summary>
	/// Confirms the details, which stamps VerifiedAt on the request.
	/// </summary>
	private Task VerifyAsync() =>
		CompleteAsync(
			reject: false,
			cancellationToken => VerificationService.VerifyAsync(Token, cancellationToken));

	/// <summary>
	/// Reports the details as inaccurate, which stamps RejectedAt on the request.
	/// </summary>
	private Task RejectAsync() =>
		CompleteAsync(
			reject: true,
			cancellationToken => VerificationService.RejectAsync(Token, cancellationToken));

	private async Task CompleteAsync(
		bool reject,
		Func<CancellationToken, Task<VerificationLinkResultDTO<EmploymentVerificationPreviewDTO>>> action)
	{
		_isSubmitting = true;
		_errorMessage = "";
		_failure = VerificationLinkFailure.None;

		try
		{
			var result = await action(_cancellation.Token);

			if (result.Failure != VerificationLinkFailure.None)
			{
				_failure = result.Failure;
				_errorMessage = result.ErrorMessage;
				return;
			}

			_request = result.Data ?? _request;
			_wasRejected = reject;
			_completed = true;
		}
		catch (OperationCanceledException)
		{
			// The page was disposed while the response was being submitted.
		}
		catch (Exception exception)
		{
			_failure = VerificationLinkFailure.Unknown;
			_errorMessage = exception.Message;
		}
		finally
		{
			_isSubmitting = false;
		}
	}

	public void Dispose()
	{
		_cancellation.Cancel();
		_cancellation.Dispose();
	}
}
