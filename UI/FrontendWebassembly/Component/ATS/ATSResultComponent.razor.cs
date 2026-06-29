namespace FrontendWebassembly.Component.ATS;

public partial class ATSResultComponent
{
	private MudForm? form;
	private TicketDetails ticketDetails = new();
	private Subject subject = new();
	private FileDetails fileDetails = new();
	private bool IsLoaded = true;

	[Parameter]
	public AddRoleDTO Role { get; set; } = new AddRoleDTO();

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
}
