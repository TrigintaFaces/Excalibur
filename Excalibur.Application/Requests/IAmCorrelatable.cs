namespace Excalibur.Application.Requests;

/// <summary>
///     Represents an entity that is correlatable with a unique identifier.
/// </summary>
public interface IAmCorrelatable
{
	/// <summary>
	///     Gets the correlation ID for the entity.
	/// </summary>
	public Guid CorrelationId { get; }
}
