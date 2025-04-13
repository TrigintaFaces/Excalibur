using System.Net;

namespace Excalibur.Core;

/// <summary>
///     Represents a client address with a specific value.
/// </summary>
public class ClientAddress : IClientAddress
{
	private IPAddress? _ipAddress;

	/// <summary>
	///     Initializes a new instance of the <see cref="ClientAddress" /> class with the specified address value.
	/// </summary>
	/// <param name="value"> The address value to initialize the instance with. </param>
	public ClientAddress(string? value) => Value = value;

	/// <summary>
	///     Initializes a new instance of the <see cref="ClientAddress" /> class with no value.
	/// </summary>
	public ClientAddress()
	{
	}

	/// <summary>
	///     Gets or sets the IP address as a string. Automatically validates and converts to an IPAddress object.
	/// </summary>
	/// <exception cref="ArgumentException"> Thrown if the provided IP address is invalid. </exception>
	public string? Value
	{
		get => _ipAddress?.ToString();
		set
		{
			if (string.IsNullOrWhiteSpace(value))
			{
				_ipAddress = null;
			}
			else if (IPAddress.TryParse(value, out var ip))
			{
				_ipAddress = ip;
			}
			else
			{
				throw new ArgumentException("Invalid IP address format.", nameof(value));
			}
		}
	}

	/// <summary>
	///     Indicates whether the IP address is IPv4.
	/// </summary>
	public bool IsIPv4 => _ipAddress?.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork;

	/// <summary>
	///     Indicates whether the IP address is IPv6.
	/// </summary>
	public bool IsIPv6 => _ipAddress?.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6;

	/// <summary>
	///     Gets the underlying <see cref="IPAddress" /> instance.
	/// </summary>
	public IPAddress? GetIPAddress() => _ipAddress;
}
