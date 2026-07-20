namespace FrontendWebassembly.Component.ATS;

public partial class ATSResultComponent
{
	private MudForm? form;
	private TicketDetails ticketDetails = new();
	private Subject subject = new();
	private FileDetails fileDetails = new();
	private bool IsLoaded = true;
	private bool showReportUploader = false;


	[Parameter]
	public Guid EmailInvitationId { get; set; }

	[Parameter]
	public ATSResultDetailsDTO? ReportResult { get; set; }

	private class Subject
	{
		public string SubjectName { get; set; } = "Antonio Aguinaldo";
		public string Score { get; set; } = "85%";
	}

	private class TicketDetails
	{
		public string TicketNumber { get; set; } = "2025 - 00123456";
		public string Status { get; set; } = "Completed";
		public string Result { get; set; } = "Clear";
		public string ReportType { get; set; } = "Basic";
		public string AptitudeTest { get; set; } = "Passed - 90%";
		public string LiveInterview { get; set; } = "Completed";
		public string Grammar { get; set; } = "Passed - 85%";
		public string Comprehension { get; set; } = "Good";
		public string Relativeness { get; set; } = "Good";
	}

	private class FileDetails
	{
		public string Resume { get; set; } = "TonCV.pdf";
		public string ID { get; set; } = "TonID.jpg";
		public string COE { get; set; } = "TonCOE.pdf";
		public string Diploma { get; set; } = "TonTOR.pd";
		public string BiometricPhoto { get; set; } = "123-456.jpg";
		public string ConsentForm { get; set; } = "Consent_Form.pdf";
		public string FinalReport { get; set; } = "2025-00123456";
		public string UploadedDate { get; set; } = "October 19, 2025";
	}

	private async Task ProcessBulkInvite()
	{
	}

	private void ShowUploadReport()
	{
		showReportUploader = true;
	}

	private void GoBackToSearchReport()
	{
		showReportUploader = false;
	}

	protected override void OnParametersSet()
	{
		if (ReportResult is null)
		{
			return;
		}

		subject.SubjectName = string.IsNullOrWhiteSpace(ReportResult.SubjectName) ? subject.SubjectName : ReportResult.SubjectName;
		ticketDetails.Status = ReportResult.OrderStatus ?? "-";
		ticketDetails.Result = ReportResult.HitStatus ?? "-";
		ticketDetails.ReportType = ReportResult.SelectedPackage ?? "-";

		fileDetails.Resume = ReportResult.ResumeFileName ?? "-";
		fileDetails.ID = ReportResult.IdUploadedFileName ?? "-";
		fileDetails.COE = ReportResult.CoeFileName ?? "-";
		fileDetails.Diploma = ReportResult.DiplomaFileName ?? "-";
		fileDetails.BiometricPhoto = ReportResult.BiometricPhotoFileName ?? "-";
		fileDetails.ConsentForm = ReportResult.ConsentFormFileName ?? "-";
		fileDetails.FinalReport = ReportResult.UploadedReportFileName ?? "-";
		fileDetails.UploadedDate = ReportResult.ReportUploadedAt?.ToString("MMMM dd, yyyy") ?? "-";
	}
}
