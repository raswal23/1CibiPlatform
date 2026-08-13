namespace ATS.DTO;

public record OrderStatusHistoryDTO(
	Guid OrderStatusHistoryId,
	string EventType,
	string? PreviousStatus,
	string NewStatus,
	string Source,
	DateTime OccurredAt);
