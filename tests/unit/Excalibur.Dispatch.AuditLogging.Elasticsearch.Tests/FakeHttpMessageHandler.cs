using System.Net;

namespace Excalibur.Dispatch.AuditLogging.Elasticsearch.Tests;

internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
	private HttpStatusCode _statusCode = HttpStatusCode.OK;
	private string _responseBody = string.Empty;
	private Exception? _exception;
	private readonly Queue<Func<HttpResponseMessage>> _sequence = new();

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

	public void EnqueueResponse(HttpStatusCode statusCode, string body = "")
	{
		_sequence.Enqueue(() => new HttpResponseMessage(statusCode)
		{
			Content = new StringContent(body)
		});
	}

	public void EnqueueException(Exception exception)
	{
		_sequence.Enqueue(() => throw exception);
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

		if (_sequence.Count > 0)
		{
			return _sequence.Dequeue()();
		}

		return new HttpResponseMessage(_statusCode)
		{
			Content = new StringContent(_responseBody)
		};
	}
}
