namespace examples.Excalibur.Patterns.MaterializedViews.Examples;

/// <summary>
///     Support ticket data.
/// </summary>
public class SupportTicketData
{
	public string TicketId { get; set; } = string.Empty;
	public string CustomerId { get; set; } = string.Empty;
	public DateTime CreatedDate { get; set; }
	public DateTime? ResolvedDate { get; set; }
	public string Status { get; set; } = string.Empty; // Open, InProgress, Closed
	public string Priority { get; set; } = string.Empty; // Low, Medium, High
}
