using System.Net;

using Excalibur.Core;

using Shouldly;

namespace Excalibur.Tests.Unit.Core;

public class ClientAddressShould
{
	[Fact]
	public void InitializeWithNullValue()
	{
		// Act
		var clientAddress = new ClientAddress();

		// Assert
		clientAddress.Value.ShouldBeNull();
	}

	[Fact]
	public void InitializeWithValidIPv4Address()
	{
		// Arrange
		var ipAddress = "192.168.1.1";

		// Act
		var clientAddress = new ClientAddress(ipAddress);

		// Assert
		clientAddress.Value.ShouldBe(ipAddress);
		clientAddress.IsIPv4.ShouldBeTrue();
		clientAddress.IsIPv6.ShouldBeFalse();
		_ = clientAddress.GetIPAddress().ShouldNotBeNull();
	}

	[Fact]
	public void InitializeWithValidIPv6Address()
	{
		// Arrange
		var ipAddress = "2001:0db8:85a3:0000:0000:8a2e:0370:7334";

		// Act
		var clientAddress = new ClientAddress(ipAddress);

		// Assert
		clientAddress.GetIPAddress().ToString().ShouldBe(IPAddress.Parse(ipAddress).ToString());
		clientAddress.IsIPv4.ShouldBeFalse();
		clientAddress.IsIPv6.ShouldBeTrue();
		_ = clientAddress.GetIPAddress().ShouldNotBeNull();
	}

	[Fact]
	public void ThrowExceptionForInvalidIPAddress()
	{
		// Arrange
		var invalidIP = "999.999.999.999";

		// Act & Assert
		Should.Throw<ArgumentException>(() => new ClientAddress(invalidIP)).Message.ShouldContain("Invalid IP address format.");
	}

	[Fact]
	public void AllowEmptyStringAsValue()
	{
		// Act
		var clientAddress = new ClientAddress(string.Empty);

		// Assert
		clientAddress.Value.ShouldBeNull();
		clientAddress.IsIPv4.ShouldBeFalse();
		clientAddress.IsIPv6.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingNullValue()
	{
		// Arrange
		var clientAddress = new ClientAddress("192.168.1.1");

		// Act
		clientAddress.Value = null;

		// Assert
		clientAddress.Value.ShouldBeNull();
		clientAddress.IsIPv4.ShouldBeFalse();
		clientAddress.IsIPv6.ShouldBeFalse();
	}

	[Fact]
	public void ReturnValidIPAddressInstance()
	{
		// Arrange
		var ip = "127.0.0.1";
		var clientAddress = new ClientAddress(ip);

		// Act
		var ipAddress = clientAddress.GetIPAddress();

		// Assert
		_ = ipAddress.ShouldNotBeNull();
		ipAddress.ToString().ShouldBe(ip);
	}

	[Fact]
	public void AllowSettingNullAndEmptyAfterInitialization()
	{
		// Arrange
		var clientAddress = new ClientAddress("192.168.0.1");

		// Act
		clientAddress.Value = null;
		clientAddress.GetIPAddress().ShouldBeNull();

		clientAddress.Value = string.Empty;
		clientAddress.GetIPAddress().ShouldBeNull();
	}

	[Fact]
	public void AllowReassigningValidIPAddresses()
	{
		// Arrange
		var clientAddress = new ClientAddress("192.168.0.1");

		// Act
		clientAddress.Value = "2001:db8::ff00:42:8329";

		// Assert
		clientAddress.IsIPv4.ShouldBeFalse();
		clientAddress.IsIPv6.ShouldBeTrue();
	}
}
