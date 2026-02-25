// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Net;
using System.Net.Sockets;

namespace Excalibur.Domain;

/// <summary>
/// Represents a client address with validation helpers.
/// </summary>
public sealed class ClientAddress : IClientAddress
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ClientAddress" /> class with the specified value.
	/// </summary>
	/// <param name="value"> The IP address string to parse and store. </param>
	public ClientAddress(string? value) => Value = value;

	/// <summary>
	/// Initializes a new instance of the <see cref="ClientAddress" /> class.
	/// </summary>
	public ClientAddress()
	{
	}

	/// <inheritdoc />
	public string? Value
	{
		get => IpAddress?.ToString();
		set
		{
			if (string.IsNullOrWhiteSpace(value))
			{
				IpAddress = null;
			}
			else if (IPAddress.TryParse(value, out var ip))
			{
				IpAddress = ip;
			}
			else
			{
				throw new ArgumentException(Resources.ClientAddress_InvalidFormat, nameof(value));
			}
		}
	}

	/// <summary>
	/// Gets a value indicating whether indicates whether the address is IPv4.
	/// </summary>
	/// <value>
	/// A value indicating whether indicates whether the address is IPv4.
	/// </value>
	public bool IsIPv4 => IpAddress?.AddressFamily == AddressFamily.InterNetwork;

	/// <summary>
	/// Gets a value indicating whether indicates whether the address is IPv6.
	/// </summary>
	/// <value>
	/// A value indicating whether indicates whether the address is IPv6.
	/// </value>
	public bool IsIPv6 => IpAddress?.AddressFamily == AddressFamily.InterNetworkV6;

	/// <summary>
	/// Gets the underlying <see cref="IPAddress" /> instance.
	/// </summary>
	/// <value>
	/// The underlying <see cref="IPAddress" /> instance.
	/// </value>
	public IPAddress? IpAddress { get; private set; }
}
