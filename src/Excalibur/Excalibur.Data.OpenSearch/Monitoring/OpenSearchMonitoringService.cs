// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using OpenSearch.Client;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.OpenSearch.Monitoring;

/// <summary>
/// Provides centralized monitoring and diagnostics orchestration for OpenSearch operations, integrating metrics collection, request
/// logging, performance diagnostics, and distributed tracing.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="OpenSearchMonitoringService" /> class.
/// </remarks>
/// <param name="metrics"> The metrics collection service. </param>
/// <param name="activitySource"> The activity source for distributed tracing. </param>
/// <param name="requestLogger"> The request/response logging service. </param>
/// <param name="performanceDiagnostics"> The performance diagnostics service. </param>
/// <param name="options"> The monitoring configuration options. </param>
/// <param name="logger"> The logger for monitoring operations. </param>
internal sealed class OpenSearchMonitoringService(
	OpenSearchMetrics metrics,
	OpenSearchActivitySource activitySource,
	OpenSearchRequestLogger requestLogger,
	OpenSearchPerformanceDiagnostics performanceDiagnostics,
	IOptions<OpenSearchMonitoringOptions> options,
	ILogger<OpenSearchMonitoringService> logger) : IDisposable
{
	private readonly OpenSearchMetrics _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));

	private readonly OpenSearchActivitySource
		_activitySource = activitySource ?? throw new ArgumentNullException(nameof(activitySource));

	private readonly OpenSearchRequestLogger _requestLogger = requestLogger ?? throw new ArgumentNullException(nameof(requestLogger));

	private readonly OpenSearchPerformanceDiagnostics _performanceDiagnostics =
		performanceDiagnostics ?? throw new ArgumentNullException(nameof(performanceDiagnostics));

	private readonly OpenSearchMonitoringOptions _settings = options?.Value ?? throw new ArgumentNullException(nameof(options));
	private readonly ILogger<OpenSearchMonitoringService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
	private volatile bool _disposed;

	/// <summary>
	/// Starts comprehensive monitoring for an OpenSearch operation.
	/// </summary>
	/// <param name="operationType"> The type of operation being monitored. </param>
	/// <param name="request"> The OpenSearch request object. </param>
	/// <param name="indexName"> The name of the index being operated on. </param>
	/// <param name="documentId"> The document ID for single-document operations. </param>
	/// <returns> A monitoring context that tracks the operation lifecycle. </returns>
	[RequiresUnreferencedCode("This method uses reflection and may not work correctly with trimming")]
	[RequiresDynamicCode("This method uses dynamic code generation and may not work correctly with AOT")]
	public OpenSearchMonitoringContext StartOperation(
		string operationType,
		object request,
		string? indexName = null,
		string? documentId = null)
	{
		if (!_settings.Enabled)
		{
			return new OpenSearchMonitoringContext(this, operationType, indexName, documentId, enableMonitoring: false);
		}

		try
		{
			// Start metrics collection -- ownership is transferred to the monitoring context
#pragma warning disable CA2000
			var metricsContext = _metrics.StartOperation(operationType, indexName);
#pragma warning restore CA2000

			// Start distributed tracing
			Activity? activity = null;
			if (_settings.Tracing.Enabled)
			{
				activity = _activitySource.StartOperation(operationType, indexName, documentId);

				if (activity != null && _settings.Tracing.RecordRequestResponse)
				{
					OpenSearchActivitySource.EnrichWithRequestDetails(activity, request, _settings.Tracing.RecordRequestResponse);
				}
			}

			// Start performance diagnostics -- ownership is transferred to the monitoring context
#pragma warning disable CA2000
			var performanceContext = _performanceDiagnostics.StartOperation(operationType, indexName);
#pragma warning restore CA2000

			// Log request details
			if (ShouldLogForLevel(_settings.RequestLogging.Enabled, _settings.Level))
			{
				_requestLogger.LogRequest(operationType, request, indexName, documentId);
			}

			return new OpenSearchMonitoringContext(
				this,
				operationType,
				indexName,
				documentId,
				enableMonitoring: true,
				metricsContext,
				activity,
				performanceContext);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to start monitoring for operation: {OperationType}", operationType);

			// Return a non-monitoring context to avoid affecting the operation
			return new OpenSearchMonitoringContext(this, operationType, indexName, documentId, enableMonitoring: false);
		}
	}

	/// <summary>
	/// Records a retry attempt for resilience monitoring.
	/// </summary>
	/// <param name="operationType"> The type of operation being retried. </param>
	/// <param name="attemptNumber"> The current attempt number. </param>
	/// <param name="maxAttempts"> The maximum number of attempts. </param>
	/// <param name="delay"> The delay before this attempt. </param>
	/// <param name="exception"> The exception that triggered the retry. </param>
	/// <param name="indexName"> The name of the index being operated on. </param>
	/// <param name="parentActivity"> The parent activity for tracing context. </param>
	public void RecordRetryAttempt(
		string operationType,
		int attemptNumber,
		int maxAttempts,
		TimeSpan delay,
		Exception exception,
		string? indexName = null,
		Activity? parentActivity = null)
	{
		if (!_settings.Enabled)
		{
			return;
		}

		try
		{
			// Record retry metrics
			_metrics.RecordRetryAttempt(operationType, attemptNumber, indexName);

			// Log retry attempt
			if (ShouldLogForLevel(_settings.RequestLogging.Enabled, _settings.Level))
			{
				_requestLogger.LogRetryAttempt(operationType, attemptNumber, maxAttempts, delay, exception, indexName);
			}

			// Create retry trace span
			if (_settings.Tracing is { Enabled: true, RecordResilienceDetails: true })
			{
				using var retryActivity = _activitySource.StartResilienceOperation(
					"retry",
					operationType,
					new Dictionary<string, object>
(StringComparer.Ordinal)
					{
						["retry.attempt"] = attemptNumber,
						["retry.max_attempts"] = maxAttempts,
						["retry.delay_ms"] = delay.TotalMilliseconds,
						["retry.exception.type"] = exception.GetType().Name,
						["retry.exception.message"] = exception.Message,
					});

				if (retryActivity != null && parentActivity?.Id != null)
				{
					_ = retryActivity.SetParentId(parentActivity.Id);
				}

				OpenSearchActivitySource.EnrichWithRetryDetails(
					retryActivity, attemptNumber, maxAttempts, delay, exception);
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to record retry attempt for operation: {OperationType}", operationType);
		}
	}

	/// <summary>
	/// Records a circuit breaker state change for resilience monitoring.
	/// </summary>
	/// <param name="fromState"> The previous circuit breaker state. </param>
	/// <param name="toState"> The new circuit breaker state. </param>
	/// <param name="operationType"> The operation type that triggered the state change. </param>
	/// <param name="reason"> The reason for the state change. </param>
	public void RecordCircuitBreakerStateChange(
		string fromState,
		string toState,
		string? operationType = null,
		string? reason = null)
	{
		if (!_settings.Enabled)
		{
			return;
		}

		try
		{
			// Record circuit breaker metrics
			_metrics.RecordCircuitBreakerStateChange(fromState, toState, operationType);

			// Log state change
			if (ShouldLogForLevel(_settings.RequestLogging.Enabled, _settings.Level))
			{
				_requestLogger.LogCircuitBreakerStateChange(fromState, toState, operationType, reason);
			}

			// Create circuit breaker trace span
			if (_settings.Tracing is { Enabled: true, RecordResilienceDetails: true })
			{
				using var circuitBreakerActivity = _activitySource.StartResilienceOperation(
					"circuit_breaker",
					operationType,
					new Dictionary<string, object>(StringComparer.Ordinal) { ["circuit_breaker.from_state"] = fromState, ["circuit_breaker.to_state"] = toState });

				OpenSearchActivitySource.EnrichWithCircuitBreakerDetails(
					circuitBreakerActivity, toState);

				if (!string.IsNullOrWhiteSpace(reason))
				{
					_ = (circuitBreakerActivity?.SetTag("circuit_breaker.reason", reason));
				}
			}

			_logger.LogInformation("Circuit breaker state changed from {FromState} to {ToState}", fromState, toState);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to record circuit breaker state change from {FromState} to {ToState}", fromState, toState);
		}
	}

	/// <summary>
	/// Records an operation that failed permanently after exhausting all resilience mechanisms.
	/// </summary>
	/// <param name="operationType"> The type of operation that failed permanently. </param>
	/// <param name="exception"> The final exception that caused the failure. </param>
	/// <param name="indexName"> The name of the index being operated on. </param>
	public void RecordPermanentFailure(string operationType, Exception exception, string? indexName = null)
	{
		if (!_settings.Enabled)
		{
			return;
		}

		try
		{
			var errorType = exception.GetType().Name;
			_metrics.RecordPermanentFailure(operationType, errorType, indexName);

			_logger.LogError(
				exception,
				"OpenSearch operation {OperationType} permanently failed after exhausting all retries and resilience mechanisms. " +
				"Index: {IndexName}, ErrorType: {ErrorType}",
				operationType, indexName ?? "unknown", errorType);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to record permanent failure for operation: {OperationType}", operationType);
		}
	}

	/// <summary>
	/// Gets the current performance metrics for all operations.
	/// </summary>
	/// <returns> A dictionary of operation types and their performance metrics. </returns>
	public IReadOnlyDictionary<string, OpenSearchPerformanceDiagnostics.PerformanceMetrics> GetPerformanceMetrics() =>
		_performanceDiagnostics.GetPerformanceMetrics();

	/// <summary>
	/// Resets all performance metrics.
	/// </summary>
	public void ResetPerformanceMetrics() => _performanceDiagnostics.ResetMetrics();

	/// <inheritdoc />
	public void Dispose()
	{
		if (!_disposed)
		{
			_metrics.Dispose();
			_activitySource.Dispose();
			_disposed = true;
		}
	}

	/// <summary>
	/// Completes monitoring for an operation with success.
	/// </summary>
	[RequiresUnreferencedCode("Calls OpenSearch activity source enrichment which uses reflection")]
	[RequiresDynamicCode("Calls OpenSearch activity source enrichment which uses dynamic code")]
	internal void CompleteOperation(
		string operationType,
		IResponse response,
		TimeSpan duration,
		string? indexName,
		string? documentId,
		OpenSearchMetrics.OpenSearchOperationContext? metricsContext,
		Activity? activity,
		OpenSearchPerformanceDiagnostics.PerformanceContext? performanceContext,
		long? documentCount = null)
	{
		try
		{
			var success = response.ApiCall?.HttpStatusCode is >= 200 and < 300;

			// Complete metrics
			if (success)
			{
				metricsContext?.RecordSuccess(documentCount);
			}
			else
			{
				var errorType = GetErrorType(response);
				metricsContext?.RecordFailure(errorType, documentCount);
			}

			// Complete performance diagnostics
			performanceContext?.Complete(response);

			// Complete tracing
			if (activity != null)
			{
				if (_settings.Tracing.RecordRequestResponse)
				{
					OpenSearchActivitySource.EnrichWithResponseDetails(activity, response,
						_settings.Tracing.RecordRequestResponse);
				}

				if (!success)
				{
					_ = activity.SetStatus(ActivityStatusCode.Error, GetErrorType(response));
				}
			}

			// Log response
			if (ShouldLogForLevel(_settings.RequestLogging.Enabled, _settings.Level))
			{
				_requestLogger.LogResponse(operationType, response, duration, indexName, documentId);
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to complete monitoring for operation: {OperationType}", operationType);
		}
	}

	/// <summary>
	/// Determines whether logging should occur based on the monitoring level.
	/// </summary>
	private static bool ShouldLogForLevel(bool featureEnabled, MonitoringLevel monitoringLevel)
	{
		if (!featureEnabled)
		{
			return false;
		}

		return monitoringLevel switch
		{
			MonitoringLevel.Minimal => false,
			MonitoringLevel.Standard => true,
			MonitoringLevel.Verbose => true,
			_ => false,
		};
	}

	/// <summary>
	/// Gets the error type from an OpenSearch response.
	/// </summary>
	private static string GetErrorType(IResponse response)
	{
		if (response.ServerError?.Error?.Type != null)
		{
			return response.ServerError.Error.Type;
		}

		return "unknown_error";
	}

	/// <summary>
	/// Represents the monitoring context for an individual OpenSearch operation.
	/// </summary>
	internal sealed class OpenSearchMonitoringContext : IDisposable
	{
		private readonly OpenSearchMonitoringService _service;
		private readonly string _operationType;
		private readonly string? _indexName;
		private readonly string? _documentId;
		private readonly bool _enableMonitoring;
		private readonly OpenSearchMetrics.OpenSearchOperationContext? _metricsContext;
		private readonly OpenSearchPerformanceDiagnostics.PerformanceContext? _performanceContext;
		private readonly DateTimeOffset _startTime;
		private volatile bool _disposed;
		private bool _completed;

		internal OpenSearchMonitoringContext(
			OpenSearchMonitoringService service,
			string operationType,
			string? indexName,
			string? documentId,
			bool enableMonitoring,
			OpenSearchMetrics.OpenSearchOperationContext? metricsContext = null,
			Activity? activity = null,
			OpenSearchPerformanceDiagnostics.PerformanceContext? performanceContext = null)
		{
			_service = service;
			_operationType = operationType;
			_indexName = indexName;
			_documentId = documentId;
			_enableMonitoring = enableMonitoring;
			_metricsContext = metricsContext;
			Activity = activity;
			_performanceContext = performanceContext;
			_startTime = DateTimeOffset.UtcNow;
		}

		/// <summary>
		/// Gets the tracing activity for this operation.
		/// </summary>
		public Activity? Activity { get; }

		/// <summary>
		/// Completes the monitoring for the operation with the given response.
		/// </summary>
		/// <param name="response"> The OpenSearch response. </param>
		/// <param name="documentCount"> The number of documents processed (for bulk operations). </param>
		public void Complete(IResponse response, long? documentCount = null)
		{
			if (_completed || _disposed || !_enableMonitoring)
			{
				return;
			}

			var duration = DateTimeOffset.UtcNow - _startTime;
			_service.CompleteOperation(
				_operationType,
				response,
				duration,
				_indexName,
				_documentId,
				_metricsContext,
				Activity,
				_performanceContext,
				documentCount);

			_completed = true;
		}

		/// <inheritdoc />
		public void Dispose()
		{
			if (!_disposed)
			{
				if (!_completed && _enableMonitoring)
				{
					// Operation was cancelled or failed without proper completion
					_metricsContext?.RecordFailure("cancelled");
					_performanceContext?.Complete();
					_ = (Activity?.SetStatus(ActivityStatusCode.Error, "Operation cancelled or failed"));
				}

				_metricsContext?.Dispose();
				Activity?.Dispose();
				_performanceContext?.Dispose();
				_disposed = true;
			}
		}
	}
}
