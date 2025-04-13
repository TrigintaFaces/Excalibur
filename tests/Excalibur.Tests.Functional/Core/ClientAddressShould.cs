using Excalibur.Core;

using Shouldly;

namespace Excalibur.Tests.Functional.Core;

public class ClientAddressShould
{
	[Fact]
	public void IdentifyIPAddressTypeCorrectly()
	{
		// Arrange
		var ipv4 = new ClientAddress("192.168.1.1");
		var ipv6 = new ClientAddress("2001:db8::ff00:42:8329");

		// Act & Assert
		ipv4.IsIPv4.ShouldBeTrue();
		ipv4.IsIPv6.ShouldBeFalse();

		ipv6.IsIPv4.ShouldBeFalse();
		ipv6.IsIPv6.ShouldBeTrue();
	}

	[Fact]
	public void ReassignIPAddressFunctionally()
	{
		// Arrange
		var clientAddress = new ClientAddress("192.168.1.1");

		// Act
		clientAddress.Value = "2001:db8::ff00:42:8329";

		// Assert
		clientAddress.IsIPv4.ShouldBeFalse();
		clientAddress.IsIPv6.ShouldBeTrue();
	}

	[Fact]
	public void ReturnIPAddressInstance()
	{
		// Arrange
		var ipAddressString = "192.168.0.1";
		var clientAddress = new ClientAddress(ipAddressString);

		// Act
		var ipAddress = clientAddress.GetIPAddress();

		// Assert
		_ = ipAddress.ShouldNotBeNull();
		ipAddress.ToString().ShouldBe(ipAddressString);
	}

	[Fact]
	public void HandleNullAndEmptyAddressesFunctionally()
	{
		// Arrange & Act
		var nullAddress = new ClientAddress(null);
		var emptyAddress = new ClientAddress(string.Empty);

		// Assert
		nullAddress.GetIPAddress().ShouldBeNull();
		emptyAddress.GetIPAddress().ShouldBeNull();
	}

	[Fact]
	public void RejectInvalidIPAddressFunctionally()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() => new ClientAddress("999.999.999.999")).Message.ShouldContain("Invalid IP address format.");
	}

	[Fact]
	public void RetrieveOriginalIPAddressFunctionally()
	{
		// Arrange
		var originalIP = "192.168.1.1";
		var clientAddress = new ClientAddress(originalIP);

		// Act
		var retrievedIP = clientAddress.Value;

		// Assert
		retrievedIP.ShouldBe(originalIP);
	}
}
