namespace Excalibur;

/// <summary>
///     Represents a client address abstraction that can be implemented by various address-related classes.
/// </summary>
public interface IClientAddress
{
	/// <summary>
	///     Gets or sets the value of the client address.
	/// </summary>
	string? Value { get; set; }
}
