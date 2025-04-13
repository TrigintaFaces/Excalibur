using Excalibur.Core;

using Newtonsoft.Json;

using Shouldly;

namespace Excalibur.Tests.Functional.Core;

public class CorrelationIdShould
{
	[Fact]
	public void EnsureUniqueCorrelationIdForEachInstance()
	{
		// Act
		var id1 = new CorrelationId();
		var id2 = new CorrelationId();

		// Assert
		id1.Value.ShouldNotBe(id2.Value);
	}

	[Fact]
	public void PreserveGuidAcrossSerialization()
	{
		// Arrange
		var original = new CorrelationId();
		var serialized = JsonConvert.SerializeObject(original);

		// Act
		var deserialized = JsonConvert.DeserializeObject<CorrelationId>(serialized);

		// Assert
		_ = deserialized.ShouldNotBeNull();
		deserialized!.Value.ShouldBe(original.Value);
	}

	[Fact]
	public void EnsureCorrelationIdInHttpHeadersFunctionally()
	{
		// Arrange
		var correlationId = new CorrelationId();
		using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com");
		request.Headers.Add("X-Correlation-ID", correlationId.ToString());

		// Act
		var headerValue = request.Headers.GetValues("X-Correlation-ID").FirstOrDefault();

		// Assert
		headerValue.ShouldBe(correlationId.ToString());
	}
}
