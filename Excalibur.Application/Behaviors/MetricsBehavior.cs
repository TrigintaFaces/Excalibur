using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.Application.Requests;
using Excalibur.Core;

using MediatR;

namespace Excalibur.Application.Behaviors;

/// <summary>
///     Implements a pipeline behavior for tracking metrics such as request count, error count, and request duration.
/// </summary>
/// <typeparam name="TRequest"> The type of the request. </typeparam>
/// <typeparam name="TResponse"> The type of the response. </typeparam>
public sealed class MetricsBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>, IDisposable where TRequest : notnull
{
	private readonly Meter _meter;
	private readonly Counter<long> _requestsTotal;
	private readonly Counter<long> _requestErrorsTotal;
	private readonly Histogram<double> _requestDurationSeconds;
	private bool _disposed;

	/// <summary>
	///     Initializes a new instance of the <see cref="MetricsBehavior{TRequest, TResponse}" /> class.
	/// </summary>
	/// <param name="meterFactory"> The meter factory used to create metrics for tracking. </param>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="meterFactory" /> is null. </exception>
	public MetricsBehavior(IMeterFactory meterFactory)
	{
		ArgumentNullException.ThrowIfNull(meterFactory);

		var meterName = $"{ApplicationContext.ApplicationSystemName.Replace('-', '_')}_Metrics";
		_meter = meterFactory.Create(meterName);

		_requestsTotal = _meter.CreateCounter<long>(
			"requests_total",
			description: "Total count of requests processed.");
		_requestErrorsTotal = _meter.CreateCounter<long>(
			"request_errors_total",
			description: "Total count of request errors.");
		_requestDurationSeconds = _meter.CreateHistogram<double>(
			"request_duration_seconds",
			unit: "seconds",
			description: "Duration of requests in seconds.");
	}

	/// <inheritdoc />
	public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);
		ArgumentNullException.ThrowIfNull(next);

		// Extract request type and name
		var requestType = (request as IActivity)?.ActivityType.ToString() ?? "Unknown";
		var requestName = TypeNameHelper.GetTypeDisplayName(typeof(TRequest), false);

		// Define tags for metrics
		var tags = new KeyValuePair<string, object>[] { new("request_type", requestType), new("request_name", requestName) };

		// Track request duration
		using var duration = MeasureDuration(tags);
		try
		{
			_requestsTotal.Add(1, tags!);

			var response = await next().ConfigureAwait(false);

			return response;
		}
		catch
		{
			// Increment error counter on exception
			_requestErrorsTotal.Add(1, tags!);
			throw;
		}
	}

	/// <summary>
	///     Releases the resources used by the <see cref="MetricsBehavior{TRequest, TResponse}" />.
	/// </summary>
	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	///     Disposes of resources held by the object.
	/// </summary>
	/// <param name="disposing"> A value indicating whether the method is being called from <see cref="Dispose()" /> or the finalizer. </param>
	public void Dispose(bool disposing)
	{
		if (_disposed)
		{
			return;
		}

		if (disposing)
		{
			// Dispose managed resources
			_meter.Dispose();
		}

		_disposed = true;
	}

	/// <summary>
	///     Helper method to measure the duration of a request.
	/// </summary>
	private DisposableAction MeasureDuration(KeyValuePair<string, object>[] tags)
	{
		var startTime = DateTime.UtcNow;

		return new DisposableAction(() =>
		{
			var duration = (DateTime.UtcNow - startTime).TotalSeconds;
			_requestDurationSeconds.Record(duration, tags!);
		});
	}

	/// <summary>
	///     Helper class to execute an action on dispose.
	/// </summary>
	private sealed class DisposableAction(Action action) : IDisposable
	{
		private readonly Action _action = action ?? throw new ArgumentNullException(nameof(action));

		/// <inheritdoc />
		public void Dispose() => _action.Invoke();
	}
}
