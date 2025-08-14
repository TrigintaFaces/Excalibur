namespace Excalibur.Core;

/// <summary>
///     Represents a correlation ID used to track requests or operations across distributed systems.
/// </summary>
public interface ICorrelationId
{
	/// <summary>
	///     Gets or sets the GUID value representing the correlation ID.
	/// </summary>
	public Guid Value { get; set; }

	/// <summary>
	///     Returns a string representation of the correlation ID.
	/// </summary>
	/// <returns> A string representation of the GUID value. </returns>
	public string ToString();
}
