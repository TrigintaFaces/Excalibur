namespace Excalibur.Core;

/// <summary>
///     Represents a tenant identifier.
/// </summary>
public interface ITenantId
{
	/// <summary>
	///     Gets or sets the tenant identifier value.
	/// </summary>
	string Value { get; set; }

	/// <summary>
	///     Returns a string representation of the tenant ID.
	/// </summary>
	/// <returns> A string representation of the tenant identifier. </returns>
	string ToString();
}
