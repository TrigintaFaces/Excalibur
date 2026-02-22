// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Abstractions.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Resilience.Polly;

/// <summary>
/// Decorator that emits OpenTelemetry metrics for resilience operations including
/// retries, circuit breaker state transitions, and timeouts.
/// </summary>
/// <remarks>
/// <para>
/// Wraps a resilience execution delegate and records metrics using the
/// <see cref="ResilienceTelemetryConstants"/> meter. All metrics follow
/// OpenTelemetry semantic conventions for resilience operations.
/// </para>
/// </remarks>
public sealed partial class TelemetryResiliencePipeline : IDisposable
{
	private readonly string _pipelineName;
	private readonly ILogger _logger;
	private readonly bool _ownsMeter;
	private readonly Meter _meter;
	private readonly ActivitySource _activitySource;
	private readonly Counter<long> _retryAttempts;
	private readonly Counter<long> _circuitBreakerTransitions;
	private readonly Histogram<double> _operationDuration;
	private readonly Counter<long> _timeouts;
	private readonly Counter<long> _operationsExecuted;

	/// <summary>
	/// Initializes a new instance of the <see cref="TelemetryResiliencePipeline"/> class.
	/// </summary>
	/// <param name="pipelineName">The name of the resilience pipeline for metric tagging.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="meterFactory">Optional meter factory for DI-managed meter lifecycle.</param>
	public TelemetryResiliencePipeline(
		string pipelineName,
		ILogger<TelemetryResiliencePipeline> logger,
		IMeterFactory? meterFactory = null)
	{
		_pipelineName = pipelineName ?? throw new ArgumentNullException(nameof(pipelineName));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		if (meterFactory is not null)
		{
			_meter = meterFactory.Create(ResilienceTelemetryConstants.MeterName);
		}
		else
		{
			_meter = new Meter(ResilienceTelemetryConstants.MeterName);
			_ownsMeter = true;
		}

		_activitySource = new ActivitySource(
			ResilienceTelemetryConstants.ActivitySourceName,
			ResilienceTelemetryConstants.Version);

		_retryAttempts = _meter.CreateCounter<long>(
			ResilienceTelemetryConstants.Instruments.RetryAttempts,
			"count",
			"Total number of retry attempts");

		_circuitBreakerTransitions = _meter.CreateCounter<long>(
			ResilienceTelemetryConstants.Instruments.CircuitBreakerTransitions,
			"count",
			"Circuit breaker state transitions");

		_operationDuration = _meter.CreateHistogram<double>(
			ResilienceTelemetryConstants.Instruments.OperationDuration,
			"ms",
			"Resilience operation duration in milliseconds");

		_timeouts = _meter.CreateCounter<long>(
			ResilienceTelemetryConstants.Instruments.Timeouts,
			"count",
			"Total number of timeout occurrences");

		_operationsExecuted = _meter.CreateCounter<long>(
			ResilienceTelemetryConstants.Instruments.OperationsExecuted,
			"count",
			"Total number of operations executed through the resilience pipeline");
	}

	/// <summary>
	/// Executes an operation with telemetry instrumentation.
	/// </summary>
	/// <typeparam name="TResult">The type of result returned by the operation.</typeparam>
	/// <param name="operation">The operation to execute.</param>
	/// <param name="cancellationToken">The cancellation token to observe.</param>
	/// <returns>The result of the operation.</returns>
	public async Task<TResult> ExecuteAsync<TResult>(
		Func<CancellationToken, Task<TResult>> operation,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(operation);

		using var activity = _activitySource.StartActivity(
			$"resilience.{_pipelineName}",
			ActivityKind.Internal);

		activity?.SetTag(ResilienceTelemetryConstants.Tags.PipelineName, _pipelineName);

		var stopwatch = ValueStopwatch.StartNew();
		var outcome = "success";

		try
		{
			var result = await operation(cancellationToken).ConfigureAwait(false);
			activity?.SetStatus(ActivityStatusCode.Ok);
			return result;
		}
		catch (TimeoutException)
		{
			outcome = "timeout";
			_timeouts.Add(1, new TagList
			{
				{ ResilienceTelemetryConstants.Tags.PipelineName, _pipelineName },
			});
			activity?.SetStatus(ActivityStatusCode.Error, "Timeout");
			throw;
		}
		catch (Exception ex)
		{
			outcome = "failure";
			activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
			throw;
		}
		finally
		{
			_operationDuration.Record(stopwatch.Elapsed.TotalMilliseconds, new TagList
			{
				{ ResilienceTelemetryConstants.Tags.PipelineName, _pipelineName },
				{ ResilienceTelemetryConstants.Tags.Outcome, outcome },
			});
			_operationsExecuted.Add(1, new TagList
			{
				{ ResilienceTelemetryConstants.Tags.PipelineName, _pipelineName },
				{ ResilienceTelemetryConstants.Tags.Outcome, outcome },
			});
		}
	}

	/// <summary>
	/// Records a retry attempt metric.
	/// </summary>
	/// <param name="attemptNumber">The retry attempt number.</param>
	/// <param name="strategyType">The type of retry strategy.</param>
	public void RecordRetryAttempt(int attemptNumber, string strategyType)
	{
		_retryAttempts.Add(1, new TagList
		{
			{ ResilienceTelemetryConstants.Tags.PipelineName, _pipelineName },
			{ ResilienceTelemetryConstants.Tags.StrategyType, strategyType },
			{ ResilienceTelemetryConstants.Tags.RetryAttempt, attemptNumber },
		});

		LogRetryAttemptRecorded(_pipelineName, attemptNumber, strategyType);
	}

	/// <summary>
	/// Records a circuit breaker state transition metric.
	/// </summary>
	/// <param name="fromState">The previous circuit breaker state.</param>
	/// <param name="toState">The new circuit breaker state.</param>
	public void RecordCircuitBreakerTransition(string fromState, string toState)
	{
		_circuitBreakerTransitions.Add(1, new TagList
		{
			{ ResilienceTelemetryConstants.Tags.PipelineName, _pipelineName },
			{ ResilienceTelemetryConstants.Tags.CircuitState, toState },
		});

		LogCircuitBreakerTransition(_pipelineName, fromState, toState);
	}

	/// <inheritdoc />
	public void Dispose()
	{
		_activitySource.Dispose();
		if (_ownsMeter)
		{
			_meter.Dispose();
		}
	}

	[LoggerMessage(ResilienceEventId.TelemetryRetryAttemptRecorded, LogLevel.Debug,
		"Resilience pipeline '{PipelineName}' recorded retry attempt {AttemptNumber} with strategy '{StrategyType}'")]
	private partial void LogRetryAttemptRecorded(string pipelineName, int attemptNumber, string strategyType);

	[LoggerMessage(ResilienceEventId.TelemetryCircuitBreakerTransition, LogLevel.Information,
		"Resilience pipeline '{PipelineName}' circuit breaker transition from '{FromState}' to '{ToState}'")]
	private partial void LogCircuitBreakerTransition(string pipelineName, string fromState, string toState);
}
