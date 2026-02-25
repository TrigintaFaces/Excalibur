namespace Excalibur.Dispatch.Tests.Conformance.TransportProvider;

internal sealed class FakeChannel : IChannel<object>
{
	private object? _lastMessage;

	public Task SendAsync<T>(T message, CancellationToken cancellationToken = default)
	{
		_lastMessage = message;
		return Task.CompletedTask;
	}

	public Task<T?> ReceiveAsync<T>(CancellationToken cancellationToken = default) =>
		Task.FromResult((T?)_lastMessage);
}

