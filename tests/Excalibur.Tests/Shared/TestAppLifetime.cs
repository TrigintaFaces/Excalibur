using Microsoft.Extensions.Hosting;

namespace Excalibur.Tests.Shared;

public class TestAppLifetime : IHostApplicationLifetime, IDisposable
{
	private readonly CancellationTokenSource _ctsStarted = new();
	private readonly CancellationTokenSource _ctsStopping = new();
	private readonly CancellationTokenSource _ctsStopped = new();
	private bool _disposedValue;

	public CancellationToken ApplicationStarted => _ctsStarted.Token;
	public CancellationToken ApplicationStopping => _ctsStopping.Token;
	public CancellationToken ApplicationStopped => _ctsStopped.Token;

	public void StopApplication()
	{
		_ctsStopping.Cancel();
		_ctsStopped.Cancel();
	}

	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (!_disposedValue)
		{
			if (disposing)
			{
				_ctsStarted.Dispose();
				_ctsStopping.Dispose();
				_ctsStopped.Dispose();
			}

			_disposedValue = true;
		}
	}
}
