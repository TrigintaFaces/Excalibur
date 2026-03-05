// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Caching;
using FakeItEasy;
using StackExchange.Redis;
using Tests.Shared;

namespace Excalibur.Dispatch.Middleware.Tests.Caching;

/// <summary>
/// Unit tests for CacheHealthMonitor functionality.
/// </summary>
[Trait("Category", "Unit")]
public sealed class CacheHealthMonitorShould : UnitTestBase
{
	[Fact]
	public async Task GetHealthStatusAsync_WithNullConnection_ReturnsUnhealthyStatus()
	{
		// Arrange
		var monitor = new CacheHealthMonitor(connectionMultiplexer: null);
		var before = DateTimeOffset.UtcNow;

		// Act
		var status = await monitor.GetHealthStatusAsync(CancellationToken.None);
		var after = DateTimeOffset.UtcNow;

		// Assert
		status.IsHealthy.ShouldBeFalse();
		status.ConnectionStatus.ShouldBe("Redis not available");
		status.ResponseTimeMs.ShouldBe(0);
		status.LastChecked.ShouldBeInRange(before, after);
	}

	[Fact]
	public async Task GetHealthStatusAsync_WithDisconnectedMultiplexer_ReturnsUnhealthyStatus()
	{
		// Arrange
		var multiplexer = A.Fake<IConnectionMultiplexer>();
		A.CallTo(() => multiplexer.IsConnected).Returns(false);
		var monitor = new CacheHealthMonitor(multiplexer);

		// Act
		var status = await monitor.GetHealthStatusAsync(CancellationToken.None);

		// Assert
		status.IsHealthy.ShouldBeFalse();
		status.ConnectionStatus.ShouldBe("Redis not available");
		status.ResponseTimeMs.ShouldBe(0);
	}

	[Fact]
	public async Task GetHealthStatusAsync_WithConnectedMultiplexer_ReturnsHealthyStatus()
	{
		// Arrange
		var multiplexer = A.Fake<IConnectionMultiplexer>();
		var database = A.Fake<IDatabase>();
		A.CallTo(() => multiplexer.IsConnected).Returns(true);
		A.CallTo(() => multiplexer.GetDatabase(A<int>._, A<object>._)).Returns(database);
		A.CallTo(() => database.PingAsync(A<CommandFlags>._)).Returns(Task.FromResult(TimeSpan.FromMilliseconds(5.3)));
		var monitor = new CacheHealthMonitor(multiplexer);
		var before = DateTimeOffset.UtcNow;

		// Act
		var status = await monitor.GetHealthStatusAsync(CancellationToken.None);
		var after = DateTimeOffset.UtcNow;

		// Assert
		status.IsHealthy.ShouldBeTrue();
		status.ConnectionStatus.ShouldBe("Connected");
		status.ResponseTimeMs.ShouldBe(5.3);
		status.LastChecked.ShouldBeInRange(before, after);
	}

	[Fact]
	public async Task GetHealthStatusAsync_WhenPingThrows_ReturnsUnhealthyWithError()
	{
		// Arrange
		var multiplexer = A.Fake<IConnectionMultiplexer>();
		var database = A.Fake<IDatabase>();
		A.CallTo(() => multiplexer.IsConnected).Returns(true);
		A.CallTo(() => multiplexer.GetDatabase(A<int>._, A<object>._)).Returns(database);
		A.CallTo(() => database.PingAsync(A<CommandFlags>._))
			.Throws(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Connection timeout"));
		var monitor = new CacheHealthMonitor(multiplexer);

		// Act
		var status = await monitor.GetHealthStatusAsync(CancellationToken.None);

		// Assert
		status.IsHealthy.ShouldBeFalse();
		status.ConnectionStatus.ShouldContain("Error:");
		status.ConnectionStatus.ShouldContain("Connection timeout");
	}

	[Fact]
	public void GetPerformanceSnapshot_Initially_ReturnsZeroCounts()
	{
		// Arrange
		var monitor = new CacheHealthMonitor();
		var before = DateTimeOffset.UtcNow;

		// Act
		var snapshot = monitor.GetPerformanceSnapshot();
		var after = DateTimeOffset.UtcNow;

		// Assert
		snapshot.HitCount.ShouldBe(0);
		snapshot.MissCount.ShouldBe(0);
		snapshot.TotalErrors.ShouldBe(0);
		snapshot.Timestamp.ShouldBeInRange(before, after);
	}

	[Fact]
	public void RecordCacheOperation_WithHit_IncrementsHitCount()
	{
		// Arrange
		var monitor = new CacheHealthMonitor();

		// Act
		monitor.RecordCacheOperation(isHit: true, hasError: false);
		var snapshot = monitor.GetPerformanceSnapshot();

		// Assert
		snapshot.HitCount.ShouldBe(1);
		snapshot.MissCount.ShouldBe(0);
		snapshot.TotalErrors.ShouldBe(0);
	}

	[Fact]
	public void RecordCacheOperation_WithMiss_IncrementsMissCount()
	{
		// Arrange
		var monitor = new CacheHealthMonitor();

		// Act
		monitor.RecordCacheOperation(isHit: false, hasError: false);
		var snapshot = monitor.GetPerformanceSnapshot();

		// Assert
		snapshot.HitCount.ShouldBe(0);
		snapshot.MissCount.ShouldBe(1);
		snapshot.TotalErrors.ShouldBe(0);
	}

	[Fact]
	public void RecordCacheOperation_WithError_IncrementsErrorCount()
	{
		// Arrange
		var monitor = new CacheHealthMonitor();

		// Act
		monitor.RecordCacheOperation(isHit: false, hasError: true);
		var snapshot = monitor.GetPerformanceSnapshot();

		// Assert
		snapshot.HitCount.ShouldBe(0);
		snapshot.MissCount.ShouldBe(1);
		snapshot.TotalErrors.ShouldBe(1);
	}

	[Fact]
	public void RecordCacheOperation_WithHitAndError_IncrementsBothCounts()
	{
		// Arrange
		var monitor = new CacheHealthMonitor();

		// Act
		monitor.RecordCacheOperation(isHit: true, hasError: true);
		var snapshot = monitor.GetPerformanceSnapshot();

		// Assert
		snapshot.HitCount.ShouldBe(1);
		snapshot.MissCount.ShouldBe(0);
		snapshot.TotalErrors.ShouldBe(1);
	}

	[Fact]
	public void RecordCacheOperation_MultipleOperations_AccumulatesCounts()
	{
		// Arrange
		var monitor = new CacheHealthMonitor();

		// Act
		monitor.RecordCacheOperation(isHit: true, hasError: false);  // hit=1, miss=0, error=0
		monitor.RecordCacheOperation(isHit: true, hasError: false);  // hit=2, miss=0, error=0
		monitor.RecordCacheOperation(isHit: false, hasError: false); // hit=2, miss=1, error=0
		monitor.RecordCacheOperation(isHit: false, hasError: true);  // hit=2, miss=2, error=1
		monitor.RecordCacheOperation(isHit: true, hasError: true);   // hit=3, miss=2, error=2
		var snapshot = monitor.GetPerformanceSnapshot();

		// Assert
		snapshot.HitCount.ShouldBe(3);
		snapshot.MissCount.ShouldBe(2);
		snapshot.TotalErrors.ShouldBe(2);
	}

	[Fact]
	public async Task GetHealthStatusAsync_WithCancellationToken_DoesNotThrow()
	{
		// Arrange
		var monitor = new CacheHealthMonitor();
		using var cts = new CancellationTokenSource();

		// Act
		var status = await monitor.GetHealthStatusAsync(cts.Token);

		// Assert
		status.ShouldNotBeNull();
	}
}
