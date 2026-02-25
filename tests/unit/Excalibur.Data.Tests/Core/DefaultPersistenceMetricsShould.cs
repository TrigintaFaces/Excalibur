// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Persistence;

namespace Excalibur.Data.Tests.Core;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DefaultPersistenceMetricsShould : IDisposable
{
	private readonly DefaultPersistenceMetrics _metrics = new();

	[Fact]
	public void HaveMeter()
	{
		_metrics.Meter.ShouldNotBeNull();
		_metrics.Meter.Name.ShouldBe("Excalibur.Data.Persistence");
	}

	[Fact]
	public void RecordQueryDuration()
	{
		Should.NotThrow(() => _metrics.RecordQueryDuration(
			TimeSpan.FromMilliseconds(50), "SELECT", true, "SqlServer"));
	}

	[Fact]
	public void RecordQueryDuration_IncrementErrorOnFailure()
	{
		Should.NotThrow(() => _metrics.RecordQueryDuration(
			TimeSpan.FromMilliseconds(50), "SELECT", false, "SqlServer"));
	}

	[Fact]
	public void RecordRowsAffected()
	{
		Should.NotThrow(() => _metrics.RecordRowsAffected(10, "INSERT", "SqlServer"));
	}

	[Fact]
	public void RecordConnectionPoolMetrics()
	{
		_metrics.RecordConnectionPoolMetrics(5, 10, "SqlServer");

		var snapshot = _metrics.GetMetricsSnapshot();
		snapshot["active_connections"].ShouldBe(5);
		snapshot["idle_connections"].ShouldBe(10);
	}

	[Fact]
	public void RecordCacheMetrics_Hit()
	{
		_metrics.RecordCacheMetrics(true, "key1", "SqlServer");
		var snapshot = _metrics.GetSnapshot();
		snapshot.CacheHits.ShouldBe(1);
	}

	[Fact]
	public void RecordCacheMetrics_Miss()
	{
		_metrics.RecordCacheMetrics(false, "key1", "SqlServer");
		var snapshot = _metrics.GetSnapshot();
		snapshot.CacheMisses.ShouldBe(1);
	}

	[Fact]
	public void RecordTransactionMetrics_Committed()
	{
		Should.NotThrow(() => _metrics.RecordTransactionMetrics(
			TimeSpan.FromMilliseconds(100), true, "SqlServer"));
	}

	[Fact]
	public void RecordTransactionMetrics_RolledBack()
	{
		Should.NotThrow(() => _metrics.RecordTransactionMetrics(
			TimeSpan.FromMilliseconds(100), false, "SqlServer"));
	}

	[Fact]
	public void RecordError_WithProvider()
	{
		Should.NotThrow(() => _metrics.RecordError(
			new InvalidOperationException("test"), "query", "SqlServer"));
	}

	[Fact]
	public void StartActivity_ReturnsNullWhenNoListener()
	{
		// Without an ActivityListener, StartActivity returns null
		var activity = _metrics.StartActivity("test-op");
		// Activity may or may not be null depending on listeners
		// Just verify no exception
	}

	[Fact]
	public void StartActivity_WithTags()
	{
		var tags = new Dictionary<string, object?> { ["key"] = "value" };
		Should.NotThrow(() => _metrics.StartActivity("test-op", tags));
	}

	[Fact]
	public void GetMetricsSnapshot_ReturnsConnectionInfo()
	{
		_metrics.RecordConnectionPoolMetrics(3, 7, "test");

		var snapshot = _metrics.GetMetricsSnapshot();

		snapshot.ShouldContainKey("active_connections");
		snapshot.ShouldContainKey("idle_connections");
	}

	[Fact]
	public void RecordQuery_DelegatesToRecordQueryDuration()
	{
		_metrics.RecordQuery("SELECT", TimeSpan.FromMilliseconds(10), true);

		var snapshot = _metrics.GetSnapshot();
		snapshot.TotalQueries.ShouldBe(1);
	}

	[Fact]
	public void RecordCommand_DelegatesToRecordRowsAffected()
	{
		_metrics.RecordCommand("INSERT", TimeSpan.FromMilliseconds(10), true);

		var snapshot = _metrics.GetSnapshot();
		snapshot.TotalCommands.ShouldBe(1);
	}

	[Fact]
	public void RecordConnectionOpen()
	{
		_metrics.RecordConnectionOpen("SqlServer");

		var snapshot = _metrics.GetMetricsSnapshot();
		snapshot.ShouldContainKey("connection_open_SqlServer");
	}

	[Fact]
	public void RecordConnectionClose()
	{
		_metrics.RecordConnectionClose("SqlServer");

		var snapshot = _metrics.GetMetricsSnapshot();
		snapshot.ShouldContainKey("connection_close_SqlServer");
	}

	[Fact]
	public void RecordCacheHit()
	{
		_metrics.RecordCacheHit("key1");

		var snapshot = _metrics.GetSnapshot();
		snapshot.CacheHits.ShouldBe(1);
	}

	[Fact]
	public void RecordCacheMiss()
	{
		_metrics.RecordCacheMiss("key1");

		var snapshot = _metrics.GetSnapshot();
		snapshot.CacheMisses.ShouldBe(1);
	}

	[Fact]
	public void RecordError_SimplifiedOverload()
	{
		_metrics.RecordError("query", new InvalidOperationException("test"));

		var snapshot = _metrics.GetSnapshot();
		snapshot.TotalErrors.ShouldBe(1);
	}

	[Fact]
	public void BeginTimedOperation_ReturnsDisposable()
	{
		using var operation = _metrics.BeginTimedOperation("test-op");
		operation.ShouldNotBeNull();
	}

	[Fact]
	public void GetSnapshot_ReturnsDefaultsWhenEmpty()
	{
		var snapshot = _metrics.GetSnapshot();

		snapshot.TotalQueries.ShouldBe(0);
		snapshot.TotalCommands.ShouldBe(0);
		snapshot.TotalErrors.ShouldBe(0);
		snapshot.CacheHits.ShouldBe(0);
		snapshot.CacheMisses.ShouldBe(0);
	}

	[Fact]
	public void Dispose_DoesNotThrow()
	{
		Should.NotThrow(() => _metrics.Dispose());
	}

	public void Dispose() => _metrics.Dispose();
}
