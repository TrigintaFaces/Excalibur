using Excalibur.Core;

using Shouldly;

namespace Excalibur.Tests.Integration.Core;

public class ClientAddressShould
{
	[Fact]
	public void IntegrateWithHttpRequestHandling()
	{
		// Arrange
		var clientAddress = new ClientAddress("192.168.1.1");
		using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com");
		request.Headers.Add("X-Client-IP", clientAddress.Value);

		// Act
		var clientIp = request.Headers.GetValues("X-Client-IP").FirstOrDefault();

		// Assert
		clientIp.ShouldBe("192.168.1.1");
	}
}
