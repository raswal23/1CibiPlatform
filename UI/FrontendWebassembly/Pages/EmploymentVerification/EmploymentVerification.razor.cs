using FrontendWebassembly.DTO.EmploymentVerification;
using System.Diagnostics.Contracts;

namespace FrontendWebassembly.Pages.EmploymentVerification;

public partial class EmploymentVerification
{
    private readonly List<VerificationRequest> Requests = [];
    private VerificationRequest? SelectedRequest;
    private bool _isLoading = true;
    private bool _isSubmitting;

    protected override async Task OnInitializedAsync()
    {
        await LoadRequestsAsync();
    }

    private async Task LoadRequestsAsync()
    {
        _isLoading = true;

        try
        {
            var requestResult = await VerificationService.GetRequestsAsync();
            var atsResult = await VerificationService.GetInProgressATSRecordsAsync();

            if (!string.IsNullOrWhiteSpace(requestResult.ErrorMessage))
            {
                Requests.Clear();
                Snackbar.Add(requestResult.ErrorMessage, Severity.Error);
                return;
            }

            if (!string.IsNullOrWhiteSpace(atsResult.ErrorMessage))
            {
                Requests.Clear();
                Snackbar.Add(atsResult.ErrorMessage, Severity.Error);
                return;
            }

            var existingRequests = requestResult.Data ?? [];
            var atsRecords = atsResult.Data ?? [];

            Requests.Clear();
            Requests.AddRange(atsRecords.Select(record =>
            {
                var existing = existingRequests
                    .Where(request => request.SubjectId == record.SubjectId)
                    .OrderByDescending(request => request.RequestedAt)
                    .FirstOrDefault();

                var startDate = record.StartDate?.ToDateTime(TimeOnly.MinValue);
                var endDate = record.EndDate?.ToDateTime(TimeOnly.MinValue);

                return new VerificationRequest(
                    record.CandidateName,
                    record.Position ?? "Not provided",
                    record.Employer,
                    record.HrEmail ?? existing?.HrEmail ?? "",
                    record.SubjectId,
                    startDate,
                    endDate,
                    $"{startDate:MMM yyyy} – {endDate:MMM yyyy}",
                    existing?.RequestedAt ?? DateTime.UtcNow,
                    existing?.Status ?? "Pending");
            }));
        }
        catch (Exception exception)
        {
            Requests.Clear();
            Snackbar.Add(
                $"Employment Verification API error: {exception.Message}",
                Severity.Error);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task SendSelectedRequestAsync()
    {
        if (SelectedRequest is null || string.IsNullOrWhiteSpace(SelectedRequest.HrEmail))
        {
            Snackbar.Add(
                "The selected record does not have an HR email.",
                Severity.Warning);
            return;
        }

        _isSubmitting = true;

        try
        {
            var request = new CreateEmploymentVerificationRequestDTO
            {
                AtsSubjectId = SelectedRequest.AtsSubjectId,
                CandidateName = SelectedRequest.Candidate,
                PreviousEmployer = "ATEC",//SelectedRequest.Employer
                Position = string.IsNullOrWhiteSpace(SelectedRequest.Position)
                    ? "Not provided"
                    : SelectedRequest.Position,
                HrEmail = "contract.fullstackdev@cibi.com.ph", //SelectedRequest.HrEmail
                EmploymentStartDate = SelectedRequest.EmploymentStartDate
                    ?? DateTime.UtcNow.AddYears(-2),
                EmploymentEndDate = SelectedRequest.EmploymentEndDate
                    ?? DateTime.UtcNow.AddMonths(-6)
            };
            var result = await VerificationService.CreateAndSendAsync(request);

            if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
            {
                Snackbar.Add(result.ErrorMessage, Severity.Error);
                return;
            }

            Snackbar.Add(result.Detail, Severity.Success);
            CloseRequest();
            await LoadRequestsAsync();
        }
        finally
        {
            _isSubmitting = false;
        }
    }

    private void ViewRequest(VerificationRequest request) =>
        SelectedRequest = request;

    private void CloseRequest() =>
        SelectedRequest = null;

    private string GetSendButtonText(string status) =>
        _isSubmitting
            ? "Sending…"
            : status is "Sent" or "Verified"
                ? "Email already sent"
                : "Send verification email";

    private sealed class VerificationRequest(
        string candidate,
        string position,
        string employer,
        string hrEmail,
        Guid? atsSubjectId,
        DateTime? employmentStartDate,
        DateTime? employmentEndDate,
        string employmentPeriod,
        DateTime requestedOn,
        string status)
    {
        public string Candidate { get; } = candidate;
        public string Position { get; } = position;
        public string Employer { get; } = employer;
        public string HrEmail { get; } = hrEmail;
        public Guid? AtsSubjectId { get; } = atsSubjectId;
        public DateTime? EmploymentStartDate { get; } = employmentStartDate;
        public DateTime? EmploymentEndDate { get; } = employmentEndDate;
        public string EmploymentPeriod { get; } = employmentPeriod;
        public DateTime RequestedOn { get; } = requestedOn;
        public string Status { get; set; } = status;
    }
}
