using Excalibur.Core;

using Shouldly;

namespace Excalibur.Tests.Integration.Core;

public class CorrelationIdShould
{
	[Fact]
	public void TestIntegrationWithApiRequest()
	{
		// Arrange
		var correlationId = new CorrelationId();
		using var client = new HttpClient();
		using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/resource");
		request.Headers.Add("X-Correlation-ID", correlationId.ToString());

		// Act
		var headerExists = request.Headers.Contains("X-Correlation-ID");
		var headerValue = request.Headers.GetValues("X-Correlation-ID").FirstOrDefault();

		// Assert
		headerExists.ShouldBeTrue();
		headerValue.ShouldBe(correlationId.ToString());
	}
}
