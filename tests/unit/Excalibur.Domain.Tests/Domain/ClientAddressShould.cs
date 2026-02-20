// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Net;

namespace Excalibur.Tests.Domain;

/// <summary>
/// Unit tests for <see cref="ClientAddress"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Domain")]
public sealed class ClientAddressShould
{
	[Fact]
	public void Constructor_Default_HasNullValue()
	{
		// Arrange & Act
		var address = new ClientAddress();

		// Assert
		address.Value.ShouldBeNull();
		address.IpAddress.ShouldBeNull();
	}

	[Fact]
	public void Constructor_WithValidIPv4_SetsValue()
	{
		// Arrange & Act
		var address = new ClientAddress("192.168.1.1");

		// Assert
		address.Value.ShouldBe("192.168.1.1");
		address.IpAddress.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_WithValidIPv6_SetsValue()
	{
		// Arrange & Act
		var address = new ClientAddress("::1");

		// Assert
		address.Value.ShouldBe("::1");
		address.IpAddress.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_WithNull_SetsValueToNull()
	{
		// Arrange & Act
		var address = new ClientAddress(null);

		// Assert
		address.Value.ShouldBeNull();
		address.IpAddress.ShouldBeNull();
	}

	[Fact]
	public void Constructor_WithInvalidAddress_ThrowsArgumentException()
	{
		// Act & Assert
		var exception = Should.Throw<ArgumentException>(() =>
			new ClientAddress("not-an-ip-address"));
		exception.ParamName.ShouldBe("value");
	}

	[Fact]
	public void Value_Setter_WithValidIPv4_UpdatesAddress()
	{
		// Arrange
		var address = new ClientAddress();

		// Act
		address.Value = "10.0.0.1";

		// Assert
		address.Value.ShouldBe("10.0.0.1");
	}

	[Fact]
	public void Value_Setter_WithNull_ClearsAddress()
	{
		// Arrange
		var address = new ClientAddress("192.168.1.1");

		// Act
		address.Value = null;

		// Assert
		address.Value.ShouldBeNull();
		address.IpAddress.ShouldBeNull();
	}

	[Fact]
	public void Value_Setter_WithEmptyString_ClearsAddress()
	{
		// Arrange
		var address = new ClientAddress("192.168.1.1");

		// Act
		address.Value = string.Empty;

		// Assert
		address.Value.ShouldBeNull();
		address.IpAddress.ShouldBeNull();
	}

	[Fact]
	public void Value_Setter_WithWhitespace_ClearsAddress()
	{
		// Arrange
		var address = new ClientAddress("192.168.1.1");

		// Act
		address.Value = "   ";

		// Assert
		address.Value.ShouldBeNull();
		address.IpAddress.ShouldBeNull();
	}

	[Fact]
	public void Value_Setter_WithInvalidAddress_ThrowsArgumentException()
	{
		// Arrange
		var address = new ClientAddress();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => address.Value = "invalid");
	}

	[Fact]
	public void IsIPv4_ReturnsTrue_ForIPv4Address()
	{
		// Arrange & Act
		var address = new ClientAddress("192.168.1.1");

		// Assert
		address.IsIPv4.ShouldBeTrue();
		address.IsIPv6.ShouldBeFalse();
	}

	[Fact]
	public void IsIPv6_ReturnsTrue_ForIPv6Address()
	{
		// Arrange & Act
		var address = new ClientAddress("2001:db8::1");

		// Assert
		address.IsIPv6.ShouldBeTrue();
		address.IsIPv4.ShouldBeFalse();
	}

	[Fact]
	public void IsIPv4_ReturnsFalse_WhenNoAddress()
	{
		// Arrange & Act
		var address = new ClientAddress();

		// Assert
		address.IsIPv4.ShouldBeFalse();
	}

	[Fact]
	public void IsIPv6_ReturnsFalse_WhenNoAddress()
	{
		// Arrange & Act
		var address = new ClientAddress();

		// Assert
		address.IsIPv6.ShouldBeFalse();
	}

	[Fact]
	public void IpAddress_ReturnsCorrectIPAddressObject()
	{
		// Arrange
		var expectedIp = IPAddress.Parse("172.16.0.1");

		// Act
		var address = new ClientAddress("172.16.0.1");

		// Assert
		address.IpAddress.ShouldNotBeNull();
		address.IpAddress.Equals(expectedIp).ShouldBeTrue();
	}

	[Fact]
	public void ImplementsIClientAddress()
	{
		// Arrange & Act
		var address = new ClientAddress("192.168.1.1");

		// Assert
		_ = address.ShouldBeAssignableTo<IClientAddress>();
	}

	[Theory]
	[InlineData("0.0.0.0")]
	[InlineData("127.0.0.1")]
	[InlineData("255.255.255.255")]
	[InlineData("10.10.10.10")]
	public void Constructor_AcceptsValidIPv4Addresses(string ipAddress)
	{
		// Act
		var address = new ClientAddress(ipAddress);

		// Assert
		address.Value.ShouldBe(ipAddress);
		address.IsIPv4.ShouldBeTrue();
	}

	[Theory]
	[InlineData("::")]
	[InlineData("::1")]
	[InlineData("fe80::1")]
	[InlineData("2001:db8:85a3::8a2e:370:7334")]
	public void Constructor_AcceptsValidIPv6Addresses(string ipAddress)
	{
		// Act
		var address = new ClientAddress(ipAddress);

		// Assert
		address.Value.ShouldNotBeNull();
		address.IsIPv6.ShouldBeTrue();
	}
}
