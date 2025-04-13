using System.Net;

using Excalibur.Core;

using Shouldly;

namespace Excalibur.Tests.Integration.Core;

public class ExcaliburHeaderNamesShould
{
	[Fact]
	public async Task IncludeHeadersInHttpRequest()
	{
		// Arrange
		using var handler = new HttpClientHandler();
		using var client = new HttpClient(handler);

		using var request = new HttpRequestMessage(HttpMethod.Get, "https://httpbin.org/headers");
		request.Headers.Add(ExcaliburHeaderNames.CorrelationId, "test-correlation-id");
		request.Headers.Add(ExcaliburHeaderNames.TenantId, "test-tenant-id");

		// Act
		using var response = await client.SendAsync(request).ConfigureAwait(false);
		var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

		// Assert
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		content.ShouldContain("test-correlation-id");
		content.ShouldContain("test-tenant-id");
	}

	[Fact]
	public async Task IncludeETagHeaderInHttpResponse()
	{
		// Arrange
		using var handler = new HttpClientHandler();
		using var client = new HttpClient(handler);

		var requestUri = "https://httpbin.org/response-headers?ETag=%22test-etag%22";
		using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);

		// Act
		using var response = await client.SendAsync(request).ConfigureAwait(false);

		// Assert
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		_ = response.Headers.ETag.ShouldNotBeNull();
		response.Headers.ETag.Tag.ShouldBe("\"test-etag\"");
	}

	[Fact]
	public async Task IncludeRaisedByHeaderInHttpRequest()
	{
		// Arrange
		using var handler = new HttpClientHandler();
		using var client = new HttpClient(handler);

		using var request = new HttpRequestMessage(HttpMethod.Get, "https://httpbin.org/headers");
		request.Headers.Add(ExcaliburHeaderNames.RaisedBy, "integration-test");

		// Act
		using var response = await client.SendAsync(request).ConfigureAwait(false);
		var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

		// Assert
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		content.ShouldContain("integration-test");
	}
}
