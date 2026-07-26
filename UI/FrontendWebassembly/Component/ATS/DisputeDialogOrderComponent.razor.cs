namespace FrontendWebassembly.Component.ATS;

public partial class DisputeDialogOrderComponent
{
	private MudForm? disputeForm;
	private bool isMarkingAsDisputed = false;

	[CascadingParameter]
	IMudDialogInstance? SubmitDisputeOrderDialog { get; set; }
	[Parameter]
	public Guid EmailInvitationId { get; set; }

	[Parameter]
	public DateTime? OrderCreatedAt { get; set; }

	[Parameter]
	public string? SubjectName { get; set; }
	private DisputeOrderRequestDTO disputeRequest = new();
	private string? otherReason = string.Empty;

	void Cancel() => SubmitDisputeOrderDialog!.Cancel();

	async Task SendDisputeAsync()
	{
		await disputeForm!.ValidateAsync();
		if (disputeForm!.IsValid)
		{

			try
			{
				isMarkingAsDisputed = true;
				await InvokeAsync(StateHasChanged);

				if (disputeRequest.DisputeReason == "Others")
				{
					disputeRequest.DisputeReason = otherReason;
				}

				disputeRequest.EmailInvitationId = EmailInvitationId;
				disputeRequest.OrderCreatedAt = OrderCreatedAt;
				disputeRequest.SubjectName = SubjectName;

				var success = await DisputeOrderService.MarkAsDisputedAsync(disputeRequest);

				if (!success)
				{
					Snackbar.Add("Failed to mark order as disputed.", Severity.Error);
					return;
				}
			}
			catch (Exception)
			{
				Snackbar.Add("Failed to dispute order as disputed.", Severity.Error);
			}
			finally
			{
				isMarkingAsDisputed = false;
				await InvokeAsync(StateHasChanged);
			}

			SubmitDisputeOrderDialog!.Close();
		}
	}
}
