using System.Net;

namespace Excalibur.Dispatch.AuditLogging.Aws.Tests;

/// <summary>
/// Fake HTTP message handler for testing HTTP-based exporters.
/// </summary>
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
	private HttpStatusCode _statusCode = HttpStatusCode.OK;
	private string _responseBody = string.Empty;
	private Exception? _exception;

	public HttpRequestMessage? LastRequest { get; private set; }
	public int RequestCount { get; private set; }
	public bool CaptureContent { get; set; }
	public string? CapturedContent { get; private set; }

	public void SetResponse(HttpStatusCode statusCode, string body = "")
	{
		_statusCode = statusCode;
		_responseBody = body;
		_exception = null;
	}

	public void SetException(Exception exception)
	{
		_exception = exception;
	}

	protected override async Task<HttpResponseMessage> SendAsync(
		HttpRequestMessage request,
		CancellationToken cancellationToken)
	{
		RequestCount++;
		LastRequest = request;

		if (CaptureContent && request.Content != null)
		{
			CapturedContent = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
		}

		if (_exception != null)
		{
			throw _exception;
		}

		return new HttpResponseMessage(_statusCode)
		{
			Content = new StringContent(_responseBody)
		};
	}
}
