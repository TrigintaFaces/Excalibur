namespace Excalibur.Core;

/// <summary>
///     Represents a client address with a specific value.
/// </summary>
public class ClientAddress : IClientAddress
{
	/// <summary>
	///     Initializes a new instance of the <see cref="ClientAddress" /> class with the specified address value.
	/// </summary>
	/// <param name="value"> The address value to initialize the instance with. </param>
	public ClientAddress(string? value)
	{
		Value = value;
	}

	/// <summary>
	///     Initializes a new instance of the <see cref="ClientAddress" /> class with no value.
	/// </summary>
	public ClientAddress()
	{
	}

	/// <summary>
	///     Gets or sets the value of the client address.
	/// </summary>
	public string? Value { get; set; }
}
