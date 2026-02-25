using Xunit.Abstractions;

namespace Tests.Shared.Helpers;

/// <summary>
/// Logger implementation that outputs to xUnit test output.
/// </summary>
/// <typeparam name="T">Type being logged for</typeparam>
public sealed class TestLogger<T> : ILogger<T>
{
	private readonly ITestOutputHelper _output;

	public TestLogger(ITestOutputHelper output)
	{
		_output = output ?? throw new ArgumentNullException(nameof(output));
	}

	public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

	public bool IsEnabled(LogLevel logLevel) => true;

	public void Log<TState>(
		LogLevel logLevel,
		EventId eventId,
		TState state,
		Exception? exception,
		Func<TState, Exception?, string> formatter)
	{
		try
		{
			var message = formatter(state, exception);
			_output.WriteLine($"[{logLevel}] {typeof(T).Name}: {message}");

			if (exception != null)
			{
				_output.WriteLine($"Exception: {exception}");
			}
		}
		catch
		{
			// Ignore logging failures during test execution
		}
	}
}
