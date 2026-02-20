// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using Elastic.Transport;
using Elastic.Transport.Products.Elasticsearch;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.ElasticSearch.Monitoring;

/// <summary>
/// Provides centralized monitoring and diagnostics orchestration for Elasticsearch operations, integrating metrics collection, request
/// logging, performance diagnostics, and distributed tracing.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ElasticsearchMonitoringService" /> class.
/// </remarks>
/// <param name="metrics"> The metrics collection service. </param>
/// <param name="activitySource"> The activity source for distributed tracing. </param>
/// <param name="requestLogger"> The request/response logging service. </param>
/// <param name="performanceDiagnostics"> The performance diagnostics service. </param>
/// <param name="options"> The monitoring configuration options. </param>
/// <param name="logger"> The logger for monitoring operations. </param>
public sealed class ElasticsearchMonitoringService(
	ElasticsearchMetrics metrics,
	ElasticsearchActivitySource activitySource,
	ElasticsearchRequestLogger requestLogger,
	ElasticsearchPerformanceDiagnostics performanceDiagnostics,
	IOptions<ElasticsearchMonitoringOptions> options,
	ILogger<ElasticsearchMonitoringService> logger) : IDisposable
{
	private readonly ElasticsearchMetrics _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));

	private readonly ElasticsearchActivitySource
		_activitySource = activitySource ?? throw new ArgumentNullException(nameof(activitySource));

	private readonly ElasticsearchRequestLogger _requestLogger = requestLogger ?? throw new ArgumentNullException(nameof(requestLogger));

	private readonly ElasticsearchPerformanceDiagnostics _performanceDiagnostics =
		performanceDiagnostics ?? throw new ArgumentNullException(nameof(performanceDiagnostics));

	private readonly ElasticsearchMonitoringOptions _settings = options?.Value ?? throw new ArgumentNullException(nameof(options));
	private readonly ILogger<ElasticsearchMonitoringService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
	private volatile bool _disposed;

	/// <summary>
	/// Starts comprehensive monitoring for an Elasticsearch operation.
	/// </summary>
	/// <param name="operationType"> The type of operation being monitored. </param>
	/// <param name="request"> The Elasticsearch request object. </param>
	/// <param name="indexName"> The name of the index being operated on. </param>
	/// <param name="documentId"> The document ID for single-document operations. </param>
	/// <returns> A monitoring context that tracks the operation lifecycle. </returns>
	[RequiresUnreferencedCode("This method uses reflection and may not work correctly with trimming")]
	[RequiresDynamicCode("This method uses dynamic code generation and may not work correctly with AOT")]
	public ElasticsearchMonitoringContext StartOperation(
		string operationType,
		object request,
		string? indexName = null,
		string? documentId = null)
	{
		if (!_settings.Enabled)
		{
			return new ElasticsearchMonitoringContext(this, operationType, indexName, documentId, enableMonitoring: false);
		}

		try
		{
			// Start metrics collection
			var metricsContext = _metrics.StartOperation(operationType, indexName);

			// Start distributed tracing
			Activity? activity = null;
			if (_settings.Tracing.Enabled)
			{
				activity = _activitySource.StartOperation(operationType, indexName, documentId);

				if (activity != null && _settings.Tracing.RecordRequestResponse)
				{
					ElasticsearchActivitySource.EnrichWithRequestDetails(activity, request, _settings.Tracing.RecordRequestResponse);
				}
			}

			// Start performance diagnostics
			var performanceContext = _performanceDiagnostics.StartOperation(operationType, indexName);

			// Log request details
			if (ShouldLogForLevel(_settings.RequestLogging.Enabled, _settings.Level))
			{
				_requestLogger.LogRequest(operationType, request, indexName, documentId);
			}

			return new ElasticsearchMonitoringContext(
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
			return new ElasticsearchMonitoringContext(this, operationType, indexName, documentId, enableMonitoring: false);
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

				ElasticsearchActivitySource.EnrichWithRetryDetails(
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

				ElasticsearchActivitySource.EnrichWithCircuitBreakerDetails(
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
	/// Records an operation that failed permanently after exhausting all resilience mechanisms. This represents a permanent failure
	/// that will be logged with full context for analysis.
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
				"Elasticsearch operation {OperationType} permanently failed after exhausting all retries and resilience mechanisms. " +
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
	public IReadOnlyDictionary<string, ElasticsearchPerformanceDiagnostics.PerformanceMetrics> GetPerformanceMetrics() =>
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
	/// <param name="operationType"> The type of operation. </param>
	/// <param name="response"> The Elasticsearch response. </param>
	/// <param name="duration"> The operation duration. </param>
	/// <param name="indexName"> The name of the index. </param>
	/// <param name="documentId"> The document ID. </param>
	/// <param name="metricsContext"> The metrics context. </param>
	/// <param name="activity"> The tracing activity. </param>
	/// <param name="performanceContext"> The performance context. </param>
	/// <param name="documentCount"> The number of documents processed. </param>
	[RequiresUnreferencedCode("Calls Excalibur.Data.ElasticSearch.Monitoring.ElasticsearchActivitySource.EnrichWithResponseDetails(Activity, ElasticsearchResponse, Boolean)")]
	[RequiresDynamicCode("Calls Excalibur.Data.ElasticSearch.Monitoring.ElasticsearchActivitySource.EnrichWithResponseDetails(Activity, ElasticsearchResponse, Boolean)")]
	internal void CompleteOperation(
		string operationType,
		TransportResponse response,
		TimeSpan duration,
		string? indexName,
		string? documentId,
		ElasticsearchMetrics.ElasticsearchOperationContext? metricsContext,
		Activity? activity,
		ElasticsearchPerformanceDiagnostics.PerformanceContext? performanceContext,
		long? documentCount = null)
	{
		try
		{
			var success = response.ApiCallDetails?.HttpStatusCode is >= 200 and < 300;

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
				if (_settings.Tracing.RecordRequestResponse && response is ElasticsearchResponse elasticResponse)
				{
					ElasticsearchActivitySource.EnrichWithResponseDetails(activity, elasticResponse,
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
	/// <param name="featureEnabled"> Whether the specific feature is enabled. </param>
	/// <param name="monitoringLevel"> The current monitoring level. </param>
	/// <returns> True if logging should occur, false otherwise. </returns>
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
	/// Gets the error type from an Elasticsearch response.
	/// </summary>
	/// <param name="response"> The Elasticsearch response. </param>
	/// <returns> The error type string. </returns>
	private static string GetErrorType(TransportResponse response)
	{
		// In v9, server error details are accessed through ElasticsearchServerError
		if (response is ElasticsearchResponse { ElasticsearchServerError.Error: not null } esResponse)
		{
			return esResponse.ElasticsearchServerError.Error.Type ?? "elasticsearch_server_error";
		}

		return "unknown_error";
	}

	/// <summary>
	/// Represents the monitoring context for an individual Elasticsearch operation.
	/// </summary>
	public sealed class ElasticsearchMonitoringContext : IDisposable
	{
		private readonly ElasticsearchMonitoringService _service;
		private readonly string _operationType;
		private readonly string? _indexName;
		private readonly string? _documentId;
		private readonly bool _enableMonitoring;
		private readonly ElasticsearchMetrics.ElasticsearchOperationContext? _metricsContext;
		private readonly ElasticsearchPerformanceDiagnostics.PerformanceContext? _performanceContext;
		private readonly DateTimeOffset _startTime;
		private volatile bool _disposed;
		private bool _completed;

		internal ElasticsearchMonitoringContext(
			ElasticsearchMonitoringService service,
			string operationType,
			string? indexName,
			string? documentId,
			bool enableMonitoring,
			ElasticsearchMetrics.ElasticsearchOperationContext? metricsContext = null,
			Activity? activity = null,
			ElasticsearchPerformanceDiagnostics.PerformanceContext? performanceContext = null)
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
		/// <value>
		/// The tracing activity for this operation.
		/// </value>
		public Activity? Activity { get; }

		/// <summary>
		/// Completes the monitoring for the operation with the given response.
		/// </summary>
		/// <param name="response"> The Elasticsearch response. </param>
		/// <param name="documentCount"> The number of documents processed (for bulk operations). </param>
		public void Complete(TransportResponse response, long? documentCount = null)
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
