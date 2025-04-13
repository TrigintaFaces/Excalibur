using System.Net;

using Excalibur.Core.Concurrency;

using FakeItEasy;
using FakeItEasy.Core;
using FakeItEasy.Creation;

using Shouldly;

namespace Excalibur.Tests.Functional.Core.Concurrency;

public class ETagShould
{
	[Fact]
	public void PreserveETagValuesAcrossServiceLayers()
	{
		// Arrange
		var etag = new ETag { IncomingValue = "\"in-001\"", OutgoingValue = "\"out-002\"" };

		// Act
		var serviceEtag = ProcessETagInServiceLayer(etag);

		// Assert
		serviceEtag.IncomingValue.ShouldBe("\"in-001\"");
		serviceEtag.OutgoingValue.ShouldBe("\"out-002\"");
	}

	[Fact]
	public void OverrideOutgoingValueInServiceLayer()
	{
		// Arrange
		var etag = new ETag { IncomingValue = "\"in-001\"", OutgoingValue = "\"out-002\"" };

		// Act
		var serviceEtag = OverrideETagInServiceLayer(etag);

		// Assert
		serviceEtag.IncomingValue.ShouldBe("\"in-001\"");
		serviceEtag.OutgoingValue.ShouldBe("\"out-999\"");
	}

	[Fact]
	public async Task PassETagThroughHttpRequestCorrectly()
	{
		// Arrange
		var etag = new ETag { IncomingValue = "\"in-req-001\"", OutgoingValue = "\"out-resp-002\"" };

		var handler = A.Fake<HttpMessageHandler>((IFakeOptions<HttpMessageHandler> options) => options.Strict());

		// Allow the Dispose method to be called without throwing an exception
		_ = A.CallTo(handler).Where((IFakeObjectCall call) => call.Method.Name == "Dispose").DoesNothing();

		var requestUri = "https://api.example.com/resource";

		// Setup fake handler to intercept request
		_ = A.CallTo(handler).Where((IFakeObjectCall call) => call.Method.Name == "SendAsync").WithReturnType<Task<HttpResponseMessage>>()
			.ReturnsLazily(
				(HttpRequestMessage request, CancellationToken cancellationToken) =>
				{
					_ = request.Headers.TryGetValues("If-Match", out var incomingValues);
					incomingValues.ShouldContain(etag.IncomingValue);

					// Create a fake response
					var response = new HttpResponseMessage(HttpStatusCode.OK);
					response.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue(etag.OutgoingValue);
					return Task.FromResult(response);
				});

		using var client = new HttpClient(handler);

		using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
		request.Headers.Add("If-Match", etag.IncomingValue);

		// Act
		var response = await client.SendAsync(request).ConfigureAwait(false);
		var outgoingEtag = response.Headers.ETag?.Tag;

		// Assert
		outgoingEtag.ShouldBe(etag.OutgoingValue);
	}

	[Fact]
	public async Task ReceiveETagFromHttpResponseCorrectly()
	{
		// Arrange
		var handler = A.Fake<HttpMessageHandler>((IFakeOptions<HttpMessageHandler> options) => options.Strict());

		// Allow the Dispose method to be called without throwing an exception
		_ = A.CallTo(handler).Where((IFakeObjectCall call) => call.Method.Name == "Dispose").DoesNothing();

		var requestUri = "https://api.example.com/resource";
		var expectedEtag = "\"out-resp-002\"";

		// Setup fake handler to simulate an HTTP response with an ETag
		_ = A.CallTo(handler).Where((IFakeObjectCall call) => call.Method.Name == "SendAsync").WithReturnType<Task<HttpResponseMessage>>()
			.ReturnsLazily(
				(HttpRequestMessage request, CancellationToken _) =>
				{
					var response = new HttpResponseMessage(HttpStatusCode.OK);
					response.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue(expectedEtag);
					return Task.FromResult(response);
				});

		using var client = new HttpClient(handler);
		var etag = new ETag();

		// Act
		using var response = await client.GetAsync(requestUri).ConfigureAwait(false);
		etag.OutgoingValue = response.Headers.ETag?.Tag;

		// Assert
		etag.OutgoingValue.ShouldBe(expectedEtag);
	}

	[Fact]
	public async Task ReturnNullIfETagIsMissingInResponse()
	{
		var handler = A.Fake<HttpMessageHandler>((IFakeOptions<HttpMessageHandler> options) => options.Strict());

		_ = A.CallTo(handler).Where((IFakeObjectCall call) => call.Method.Name == "Dispose").DoesNothing();

		_ = A.CallTo(handler).Where((IFakeObjectCall call) => call.Method.Name == "SendAsync").WithReturnType<Task<HttpResponseMessage>>()
			.ReturnsLazily(
				(HttpRequestMessage request, CancellationToken _) =>
				{
					var response = new HttpResponseMessage(HttpStatusCode.OK);
					return Task.FromResult(response);
				});

		using var client = new HttpClient(handler);
		var etag = new ETag();

		using var response = await client.GetAsync("https://api.example.com/resource").ConfigureAwait(false);
		etag.OutgoingValue = response.Headers.ETag?.Tag;

		etag.OutgoingValue.ShouldBeNull();
	}

	[Fact]
	public async Task HandleMalformedETagGracefully()
	{
		// Arrange
		using var handler = new FakeHttpMessageHandler
		{
			SendAsyncFunc = async (HttpRequestMessage request, CancellationToken cancellationToken) =>
			{
				var response = new HttpResponseMessage(HttpStatusCode.OK);
				_ = response.Headers.TryAddWithoutValidation("ETag", "malformed-etag");
				return await Task.FromResult(response).ConfigureAwait(false);
			}
		};

		using var client = new HttpClient(handler);
		var etag = new ETag();

		// Act
		using var response = await client.GetAsync("https://api.example.com/resource").ConfigureAwait(false);
		etag.OutgoingValue = response.Headers.ETag?.Tag;

		// Assert
		etag.OutgoingValue.ShouldBeNull(); // Should gracefully handle malformed ETag
	}

	private IETag ProcessETagInServiceLayer(IETag etag)
	{
		// Simulate passing ETag through service layers
		return etag;
	}

	private IETag OverrideETagInServiceLayer(IETag etag)
	{
		// Simulate modifying the ETag in a service layer
		etag.OutgoingValue = "\"out-999\"";
		return etag;
	}
}

public class FakeHttpMessageHandler : HttpMessageHandler
{
	public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> SendAsyncFunc { get; set; }

	protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		var response = SendAsyncFunc != null ? await SendAsyncFunc(request, cancellationToken).ConfigureAwait(false) : null;

		// Ensure response is disposed of when it's no longer needed
		return response;
	}
}
