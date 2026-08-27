namespace ATS.Services.OMSTicketing;

public interface IOMSTicketingProcessorService
{
	/// <summary>
	/// Claims a batch of un-ticketed orders, raises an OMS ticket for each, and writes
	/// the returned ticket number back onto the order. Safe to run concurrently: the
	/// claim is atomic, so no order is ticketed twice.
	/// </summary>
	Task ProcessAsync(CancellationToken cancellationToken);
}
