namespace examples.Excalibur.Patterns.MaterializedViews.Examples;

/// <summary>
///     Customer interaction data.
/// </summary>
public class InteractionData
{
	public string InteractionId { get; set; } = string.Empty;
	public string CustomerId { get; set; } = string.Empty;
	public DateTime Date { get; set; }
	public string Channel { get; set; } = string.Empty; // Email, Phone, Chat, etc.
	public string Type { get; set; } = string.Empty; // Inquiry, Complaint, Feedback
}
