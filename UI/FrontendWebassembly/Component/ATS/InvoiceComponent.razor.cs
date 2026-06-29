namespace FrontendWebassembly.Component.ATS;

public partial class InvoiceComponent
{
	private Bill bill = new();
	private DateTime? _date;

	protected override void OnInitialized()
	{
		_date = DateTime.Today;
	}

	private class Bill
	{
		public string BillingStatement { get; set; } = string.Empty;
		public string DueDate { get; set; } = string.Empty;
		public string AmountDue { get; set; } = string.Empty;
		public string PreviousBalance { get; set; } = string.Empty;
		public string TotalAmountDue { get; set; } = string.Empty;
	}

	private void ProcessBulkInvite()
	{
	}
}
