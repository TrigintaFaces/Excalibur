// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.Metrics;

namespace Excalibur.Data.ElasticSearch.Monitoring;

/// <summary>
/// Provides comprehensive metrics collection for Elasticsearch operations including performance, resilience, and health indicators.
/// </summary>
public sealed class ElasticsearchMetrics : IDisposable
{
	private readonly Meter _meter;
	private readonly Counter<long> _operationCounter;
	private readonly Counter<long> _retryCounter;
	private readonly Counter<long> _circuitBreakerStateChangeCounter;
	private readonly Counter<long> _permanentFailureCounter;
	private readonly Histogram<double> _operationDuration;
	private readonly Histogram<long> _documentCounter;
	private readonly UpDownCounter<long> _activeOperationsCounter;
	private readonly ObservableGauge<int> _circuitBreakerStateGauge;
	private readonly ObservableGauge<double> _healthStatusGauge;
	private volatile bool _disposed;

	/// <summary>
	/// Circuit breaker state tracking
	/// </summary>
	private volatile int _circuitBreakerState; // 0 = Closed, 1 = Open, 2 = Half-Open

	/// <summary>
	/// 1.0 = Healthy, 0.0 = Unhealthy
	/// </summary>
	private double _lastHealthStatus = 1.0;

	/// <summary>
	/// Initializes a new instance of the <see cref="ElasticsearchMetrics" /> class.
	/// </summary>
	/// <param name="meterName"> The name of the meter for metrics collection. </param>
	/// <param name="version"> The version of the meter. </param>
	/// <param name="durationHistogramBuckets"> The histogram buckets for duration measurements. </param>
	public ElasticsearchMetrics(string meterName = "Excalibur.Data.ElasticSearch", string? version = null,
		double[]? durationHistogramBuckets = null)
	{
		_meter = new Meter(meterName, version);

		// Operation counters
		_operationCounter = _meter.CreateCounter<long>(
			"elasticsearch_operations_total",
			"operation",
			"Total number of Elasticsearch operations by type and result");

		_retryCounter = _meter.CreateCounter<long>(
			"elasticsearch_retries_total",
			"retry",
			"Total number of retry attempts by operation type");

		_circuitBreakerStateChangeCounter = _meter.CreateCounter<long>(
			"elasticsearch_circuit_breaker_state_changes_total",
			"state_change",
			"Total number of circuit breaker state changes");

		_permanentFailureCounter = _meter.CreateCounter<long>(
			"elasticsearch_permanent_failures_total",
			"operation",
			"Total number of operations that failed permanently after exhausting all resilience mechanisms");

		// Duration and performance metrics
		_operationDuration = _meter.CreateHistogram(
			"elasticsearch_operation_duration_ms",
			"ms",
			"Duration of Elasticsearch operations in milliseconds",
			advice: durationHistogramBuckets != null
				? new InstrumentAdvice<double> { HistogramBucketBoundaries = durationHistogramBuckets }
				: null);

		_documentCounter = _meter.CreateHistogram<long>(
			"elasticsearch_documents_processed",
			"document",
			"Number of documents processed in bulk operations");

		// Active operations tracking
		_activeOperationsCounter = _meter.CreateUpDownCounter<long>(
			"elasticsearch_active_operations",
			"operation",
			"Number of currently active Elasticsearch operations");

		// Observable gauges for state monitoring
		_circuitBreakerStateGauge = _meter.CreateObservableGauge(
			"elasticsearch_circuit_breaker_state",
			() => _circuitBreakerState,
			"state",
			"Current circuit breaker state (0=Closed, 1=Open, 2=Half-Open)");

		_healthStatusGauge = _meter.CreateObservableGauge(
			"elasticsearch_health_status",
			() => _lastHealthStatus,
			"status",
			"Current Elasticsearch cluster health status (1.0=Healthy, 0.0=Unhealthy)");
	}

	/// <summary>
	/// Records the start of an Elasticsearch operation.
	/// </summary>
	/// <param name="operationType"> The type of operation (e.g., "search", "index", "delete"). </param>
	/// <param name="indexName"> The name of the index being operated on. </param>
	/// <returns> An operation context for tracking the operation lifecycle. </returns>
	public ElasticsearchOperationContext StartOperation(string operationType, string? indexName = null)
	{
		ThrowIfDisposed();

		var tags = CreateBaseTags(operationType, indexName);
		_activeOperationsCounter.Add(1, tags);

		return new ElasticsearchOperationContext(this, operationType, indexName, tags);
	}

	/// <summary>
	/// Records a retry attempt for an operation.
	/// </summary>
	/// <param name="operationType"> The type of operation being retried. </param>
	/// <param name="attemptNumber"> The retry attempt number. </param>
	/// <param name="indexName"> The name of the index being operated on. </param>
	public void RecordRetryAttempt(string operationType, int attemptNumber, string? indexName = null)
	{
		ThrowIfDisposed();

		var tags = CreateRetryTags(operationType, attemptNumber, indexName);
		_retryCounter.Add(1, tags);
	}

	/// <summary>
	/// Records a circuit breaker state change.
	/// </summary>
	/// <param name="fromState"> The previous circuit breaker state. </param>
	/// <param name="toState"> The new circuit breaker state. </param>
	/// <param name="operationType"> The operation type that triggered the state change. </param>
	public void RecordCircuitBreakerStateChange(string fromState, string toState, string? operationType = null)
	{
		ThrowIfDisposed();

		// Update internal state tracking
		_circuitBreakerState = toState.ToLowerInvariant() switch
		{
			"closed" => 0,
			"open" => 1,
			"half-open" => 2,
			_ => 0,
		};

		var tags = CreateCircuitBreakerTags(fromState, toState, operationType);
		_circuitBreakerStateChangeCounter.Add(1, tags);
	}

	/// <summary>
	/// Records an operation that failed permanently after exhausting all resilience mechanisms.
	/// </summary>
	/// <param name="operationType"> The type of operation that failed permanently. </param>
	/// <param name="errorType"> The type of error that caused the failure. </param>
	/// <param name="indexName"> The name of the index being operated on. </param>
	public void RecordPermanentFailure(string operationType, string errorType, string? indexName = null)
	{
		ThrowIfDisposed();

		var tags = CreatePermanentFailureTags(operationType, errorType, indexName);
		_permanentFailureCounter.Add(1, tags);
	}

	/// <summary>
	/// Updates the cluster health status.
	/// </summary>
	/// <param name="isHealthy"> Whether the cluster is currently healthy. </param>
	/// <param name="healthStatus"> The detailed health status from Elasticsearch. </param>
	public void UpdateHealthStatus(bool isHealthy, string? healthStatus = null)
	{
		ThrowIfDisposed();
		_lastHealthStatus = isHealthy ? 1.0 : 0.0;
	}

	/// <inheritdoc />
	public void Dispose()
	{
		if (!_disposed)
		{
			_meter.Dispose();
			_disposed = true;
		}
	}

	/// <summary>
	/// Creates base tags for operation metrics.
	/// </summary>
	/// <param name="operationType"> The type of operation. </param>
	/// <param name="indexName"> The name of the index. </param>
	/// <returns> A collection of key-value pairs representing metric tags. </returns>
	private static KeyValuePair<string, object?>[] CreateBaseTags(string operationType, string? indexName)
	{
		var tags = new List<KeyValuePair<string, object?>> { new("operation.type", operationType) };

		if (!string.IsNullOrWhiteSpace(indexName))
		{
			tags.Add(new KeyValuePair<string, object?>("elasticsearch.index.name", indexName));
		}

		return [.. tags];
	}

	/// <summary>
	/// Creates tags for retry metrics.
	/// </summary>
	/// <param name="operationType"> The type of operation being retried. </param>
	/// <param name="attemptNumber"> The retry attempt number. </param>
	/// <param name="indexName"> The name of the index. </param>
	/// <returns> A collection of key-value pairs representing metric tags. </returns>
	private static KeyValuePair<string, object?>[] CreateRetryTags(string operationType, int attemptNumber, string? indexName)
	{
		var tags = new List<KeyValuePair<string, object?>> { new("operation.type", operationType), new("retry.attempt", attemptNumber) };

		if (!string.IsNullOrWhiteSpace(indexName))
		{
			tags.Add(new KeyValuePair<string, object?>("elasticsearch.index.name", indexName));
		}

		return [.. tags];
	}

	/// <summary>
	/// Creates tags for circuit breaker metrics.
	/// </summary>
	/// <param name="fromState"> The previous state. </param>
	/// <param name="toState"> The new state. </param>
	/// <param name="operationType"> The operation type. </param>
	/// <returns> A collection of key-value pairs representing metric tags. </returns>
	private static KeyValuePair<string, object?>[] CreateCircuitBreakerTags(string fromState, string toState, string? operationType)
	{
		var tags = new List<KeyValuePair<string, object?>>
		{
			new("circuit_breaker.from_state", fromState), new("circuit_breaker.to_state", toState),
		};

		if (!string.IsNullOrWhiteSpace(operationType))
		{
			tags.Add(new KeyValuePair<string, object?>("operation.type", operationType));
		}

		return [.. tags];
	}

	/// <summary>
	/// Creates tags for permanent failure metrics.
	/// </summary>
	/// <param name="operationType"> The type of operation. </param>
	/// <param name="errorType"> The type of error. </param>
	/// <param name="indexName"> The name of the index. </param>
	/// <returns> A collection of key-value pairs representing metric tags. </returns>
	private static KeyValuePair<string, object?>[] CreatePermanentFailureTags(string operationType, string errorType, string? indexName)
	{
		var tags = new List<KeyValuePair<string, object?>> { new("operation.type", operationType), new("error.type", errorType) };

		if (!string.IsNullOrWhiteSpace(indexName))
		{
			tags.Add(new KeyValuePair<string, object?>("elasticsearch.index.name", indexName));
		}

		return [.. tags];
	}

	/// <summary>
	/// Throws an <see cref="ObjectDisposedException" /> if the metrics instance has been disposed.
	/// </summary>
	/// <exception cref="ObjectDisposedException"> Thrown when the instance has been disposed. </exception>
	private void ThrowIfDisposed()
	{
		if (_disposed)
		{
			throw new ObjectDisposedException(nameof(ElasticsearchMetrics));
		}
	}

	/// <summary>
	/// Represents the context of an Elasticsearch operation for metrics tracking.
	/// </summary>
	/// <remarks>
	/// Nested type is intentionally public as it is returned from StartOperation() method and part of the metrics API surface.
	/// </remarks>
	// R0.8: Do not nest type - Required for public API compatibility
#pragma warning disable CA1034
	public sealed class ElasticsearchOperationContext : IDisposable
#pragma warning restore CA1034
	{
		private readonly ElasticsearchMetrics _metrics;
		private readonly string _operationType;
		private readonly string? _indexName;
		private readonly KeyValuePair<string, object?>[] _baseTags;
		private readonly DateTimeOffset _startTime;
		private volatile bool _disposed;
		private bool _completed;

		internal ElasticsearchOperationContext(
			ElasticsearchMetrics metrics,
			string operationType,
			string? indexName,
			KeyValuePair<string, object?>[] baseTags)
		{
			_metrics = metrics;
			_operationType = operationType;
			_indexName = indexName;
			_baseTags = baseTags;
			_startTime = DateTimeOffset.UtcNow;
		}

		/// <summary>
		/// Records the completion of the operation with success.
		/// </summary>
		/// <param name="documentCount"> The number of documents processed (for bulk operations). </param>
		public void RecordSuccess(long? documentCount = null)
		{
			if (_completed || _disposed)
			{
				return;
			}

			var duration = (DateTimeOffset.UtcNow - _startTime).TotalMilliseconds;
			var tags = CreateCompletionTags("success");

			_metrics._operationCounter.Add(1, tags);
			_metrics._operationDuration.Record(duration, tags);

			if (documentCount is > 0)
			{
				_metrics._documentCounter.Record(documentCount.Value, tags);
			}

			_completed = true;
		}

		/// <summary>
		/// Records the completion of the operation with failure.
		/// </summary>
		/// <param name="errorType"> The type of error that occurred. </param>
		/// <param name="documentCount"> The number of documents processed before failure (for bulk operations). </param>
		public void RecordFailure(string errorType, long? documentCount = null)
		{
			if (_completed || _disposed)
			{
				return;
			}

			var duration = (DateTimeOffset.UtcNow - _startTime).TotalMilliseconds;
			var tags = CreateCompletionTags("failure", errorType);

			_metrics._operationCounter.Add(1, tags);
			_metrics._operationDuration.Record(duration, tags);

			if (documentCount is > 0)
			{
				_metrics._documentCounter.Record(documentCount.Value, tags);
			}

			_completed = true;
		}

		/// <inheritdoc />
		public void Dispose()
		{
			if (!_disposed)
			{
				// Record as cancelled if not already completed
				if (!_completed)
				{
					RecordFailure("cancelled");
				}

				// Decrement active operations counter
				_metrics._activeOperationsCounter.Add(-1, _baseTags);
				_disposed = true;
			}
		}

		/// <summary>
		/// Creates tags for operation completion metrics.
		/// </summary>
		/// <param name="result"> The operation result (success/failure). </param>
		/// <param name="errorType"> The error type if the operation failed. </param>
		/// <returns> A collection of key-value pairs representing metric tags. </returns>
		private KeyValuePair<string, object?>[] CreateCompletionTags(string result, string? errorType = null)
		{
			var tags = new List<KeyValuePair<string, object?>>(_baseTags) { new("operation.result", result) };

			if (!string.IsNullOrWhiteSpace(errorType))
			{
				tags.Add(new KeyValuePair<string, object?>("error.type", errorType));
			}

			return [.. tags];
		}
	}
}
