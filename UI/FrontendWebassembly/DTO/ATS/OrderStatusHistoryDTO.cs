namespace FrontendWebassembly.DTO.ATS;

public record OrderStatusHistoryDTO
{
    public Guid OrderStatusHistoryId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? PreviousStatus { get; set; }
    public string NewStatus { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
}

public record GetOrderStatusHistoryResponseDTO
{
    public List<OrderStatusHistoryDTO> History { get; set; } = [];
}
