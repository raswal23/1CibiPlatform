namespace FrontendWebassembly.Component.ATS;

public partial class DisputeOrderComponent
{
	private TableComponent<OrderRow>? ordersTable;

	private readonly List<OrderRow> _dummyOrders = new()
{
	new OrderRow("2025 - 00123456", "Antonio Aguinaldo", "Completed", "Clear", "October 25, 2025", false),
	new OrderRow("2025 - 00129876", "Teodoro Mabini", "Completed", "Clear", "October 21, 2025", false),
	new OrderRow("2025 - 00124356", "Maria Quezon", "Completed", "Not Clear", "October 20, 2025", false),
	new OrderRow("2025 - 00198765", "Leonardo Roxas", "Completed", "Not Clear", "October 18, 2025", false),
	new OrderRow("2025 - 00198765", "Joseph Escoda", "Completed", "Not Clear", "October 18, 2025", false),
	new OrderRow("2025 - 00198765", "Francis Macapagal", "Completed", "Clear", "October 10, 2025", false),
	new OrderRow("2025 - 00198765", "Kris Aquino","Completed", "Not Clear", "October 9, 2025", false)
};

	private class OrderRow
	{
		public string TicketNumber { get; set; }
		public string SubjectName { get; set; }
		public string Status { get; set; }
		public string Result { get; set; }
		public string DateCompleted { get; set; }
		public bool Dispute { get; set; }

		public OrderRow(string ticketNumber, string subjectName, string status, string result, string dateCompleted, bool dispute)
		{
			TicketNumber = ticketNumber;
			SubjectName = subjectName;
			Status = status;
			Result = result;
			DateCompleted = dateCompleted;
			Dispute = dispute;
		}
	}

	private Task<TableData<OrderRow>> LoadOrderData(TableState state, CancellationToken cancellationToken)
	{
		// For now returns static results filtered by name query (simple contains)
		var filtered = _dummyOrders.ToList();

		return Task.FromResult(new TableData<OrderRow>
		{
			Items = filtered,
			TotalItems = filtered.Count
		});
	}
}
