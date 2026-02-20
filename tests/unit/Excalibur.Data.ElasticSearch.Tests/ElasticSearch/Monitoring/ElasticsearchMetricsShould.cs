// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Monitoring;

namespace Excalibur.Data.Tests.ElasticSearch.Monitoring;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class ElasticsearchMetricsShould : IDisposable
{
	private readonly ElasticsearchMetrics _sut;

	public ElasticsearchMetricsShould()
	{
		_sut = new ElasticsearchMetrics();
	}

	[Fact]
	public void CreateWithDefaultParameters()
	{
		using var metrics = new ElasticsearchMetrics();
		metrics.ShouldNotBeNull();
	}

	[Fact]
	public void CreateWithCustomMeterName()
	{
		using var metrics = new ElasticsearchMetrics("custom.meter", "1.0.0");
		metrics.ShouldNotBeNull();
	}

	[Fact]
	public void CreateWithCustomHistogramBuckets()
	{
		using var metrics = new ElasticsearchMetrics(durationHistogramBuckets: [1, 5, 10, 50, 100, 500]);
		metrics.ShouldNotBeNull();
	}

	[Fact]
	public void StartOperationAndReturnContext()
	{
		using var context = _sut.StartOperation("search", "test-index");
		context.ShouldNotBeNull();
	}

	[Fact]
	public void StartOperationWithoutIndexName()
	{
		using var context = _sut.StartOperation("search");
		context.ShouldNotBeNull();
	}

	[Fact]
	public void RecordRetryAttemptWithIndex()
	{
		// Should not throw
		_sut.RecordRetryAttempt("search", 1, "test-index");
	}

	[Fact]
	public void RecordRetryAttemptWithoutIndex()
	{
		_sut.RecordRetryAttempt("search", 1);
	}

	[Fact]
	public void RecordCircuitBreakerStateChangeToOpen()
	{
		_sut.RecordCircuitBreakerStateChange("closed", "open", "search");
	}

	[Fact]
	public void RecordCircuitBreakerStateChangeToClosed()
	{
		_sut.RecordCircuitBreakerStateChange("open", "closed");
	}

	[Fact]
	public void RecordCircuitBreakerStateChangeToHalfOpen()
	{
		_sut.RecordCircuitBreakerStateChange("open", "half-open");
	}

	[Fact]
	public void RecordCircuitBreakerStateChangeWithUnknownState()
	{
		_sut.RecordCircuitBreakerStateChange("closed", "unknown");
	}

	[Fact]
	public void RecordPermanentFailureWithIndex()
	{
		_sut.RecordPermanentFailure("search", "TimeoutException", "test-index");
	}

	[Fact]
	public void RecordPermanentFailureWithoutIndex()
	{
		_sut.RecordPermanentFailure("search", "TimeoutException");
	}

	[Fact]
	public void UpdateHealthStatusToHealthy()
	{
		_sut.UpdateHealthStatus(true, "green");
	}

	[Fact]
	public void UpdateHealthStatusToUnhealthy()
	{
		_sut.UpdateHealthStatus(false, "red");
	}

	[Fact]
	public void ThrowObjectDisposedExceptionAfterDispose()
	{
		_sut.Dispose();
		Should.Throw<ObjectDisposedException>(() => _sut.StartOperation("search"));
	}

	[Fact]
	public void ThrowObjectDisposedExceptionForRecordRetryAfterDispose()
	{
		_sut.Dispose();
		Should.Throw<ObjectDisposedException>(() => _sut.RecordRetryAttempt("search", 1));
	}

	[Fact]
	public void ThrowObjectDisposedExceptionForCircuitBreakerAfterDispose()
	{
		_sut.Dispose();
		Should.Throw<ObjectDisposedException>(() => _sut.RecordCircuitBreakerStateChange("closed", "open"));
	}

	[Fact]
	public void ThrowObjectDisposedExceptionForPermanentFailureAfterDispose()
	{
		_sut.Dispose();
		Should.Throw<ObjectDisposedException>(() => _sut.RecordPermanentFailure("search", "error"));
	}

	[Fact]
	public void ThrowObjectDisposedExceptionForUpdateHealthAfterDispose()
	{
		_sut.Dispose();
		Should.Throw<ObjectDisposedException>(() => _sut.UpdateHealthStatus(true));
	}

	[Fact]
	public void HandleDoubleDispose()
	{
		_sut.Dispose();
		// Should not throw
		_sut.Dispose();
	}

	[Fact]
	public void RecordSuccessInOperationContext()
	{
		using var context = _sut.StartOperation("index", "test-index");
		// Should not throw
		context.RecordSuccess();
	}

	[Fact]
	public void RecordSuccessWithDocumentCount()
	{
		using var context = _sut.StartOperation("bulk", "test-index");
		context.RecordSuccess(documentCount: 100);
	}

	[Fact]
	public void RecordFailureInOperationContext()
	{
		using var context = _sut.StartOperation("search", "test-index");
		context.RecordFailure("TimeoutException");
	}

	[Fact]
	public void RecordFailureWithDocumentCount()
	{
		using var context = _sut.StartOperation("bulk", "test-index");
		context.RecordFailure("BulkError", documentCount: 50);
	}

	[Fact]
	public void IgnoreDuplicateRecordSuccessInContext()
	{
		using var context = _sut.StartOperation("search", "test-index");
		context.RecordSuccess();
		// Second call should be ignored silently
		context.RecordSuccess();
	}

	[Fact]
	public void IgnoreRecordFailureAfterRecordSuccess()
	{
		using var context = _sut.StartOperation("search", "test-index");
		context.RecordSuccess();
		// Should be ignored since already completed
		context.RecordFailure("error");
	}

	[Fact]
	public void RecordCancelledWhenContextDisposedWithoutCompletion()
	{
		var context = _sut.StartOperation("search", "test-index");
		// Disposing without RecordSuccess/RecordFailure should auto-record cancelled
		context.Dispose();
	}

	[Fact]
	public void HandleContextDoubleDispose()
	{
		var context = _sut.StartOperation("search");
		context.Dispose();
		context.Dispose(); // Should not throw
	}

	public void Dispose() => _sut.Dispose();
}
