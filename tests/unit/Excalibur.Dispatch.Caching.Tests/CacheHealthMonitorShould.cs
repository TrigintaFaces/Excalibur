// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Caching;

using StackExchange.Redis;

namespace Excalibur.Dispatch.Caching.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Caching")]
public sealed class CacheHealthMonitorShould
{
	[Fact]
	public async Task ReturnUnhealthy_WhenNoRedisConnection()
	{
		// Arrange
		var monitor = new CacheHealthMonitor();

		// Act
		var status = await monitor.GetHealthStatusAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		status.IsHealthy.ShouldBeFalse();
		status.ConnectionStatus.ShouldBe("Redis not available");
		status.LastChecked.ShouldNotBe(default);
	}

	[Fact]
	public async Task ReturnUnhealthy_WhenRedisNotConnected()
	{
		// Arrange
		var multiplexer = A.Fake<IConnectionMultiplexer>();
		A.CallTo(() => multiplexer.IsConnected).Returns(false);
		var monitor = new CacheHealthMonitor(multiplexer);

		// Act
		var status = await monitor.GetHealthStatusAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		status.IsHealthy.ShouldBeFalse();
		status.ConnectionStatus.ShouldBe("Redis not available");
	}

	[Fact]
	public async Task ReturnHealthy_WhenRedisConnected()
	{
		// Arrange
		var multiplexer = A.Fake<IConnectionMultiplexer>();
		A.CallTo(() => multiplexer.IsConnected).Returns(true);
		var database = A.Fake<IDatabase>();
		A.CallTo(() => multiplexer.GetDatabase(A<int>._, A<object>._)).Returns(database);
		A.CallTo(() => database.PingAsync(A<CommandFlags>._)).Returns(TimeSpan.FromMilliseconds(5));
		var monitor = new CacheHealthMonitor(multiplexer);

		// Act
		var status = await monitor.GetHealthStatusAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		status.IsHealthy.ShouldBeTrue();
		status.ConnectionStatus.ShouldBe("Connected");
		status.ResponseTimeMs.ShouldBe(5.0);
	}

	[Fact]
	public async Task ReturnUnhealthy_WhenPingThrows()
	{
		// Arrange
		var multiplexer = A.Fake<IConnectionMultiplexer>();
		A.CallTo(() => multiplexer.IsConnected).Returns(true);
		var database = A.Fake<IDatabase>();
		A.CallTo(() => multiplexer.GetDatabase(A<int>._, A<object>._)).Returns(database);
		A.CallTo(() => database.PingAsync(A<CommandFlags>._)).ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Test error"));
		var monitor = new CacheHealthMonitor(multiplexer);

		// Act
		var status = await monitor.GetHealthStatusAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		status.IsHealthy.ShouldBeFalse();
		status.ConnectionStatus.ShouldStartWith("Error:");
	}

	[Fact]
	public void RecordCacheOperation_IncrementsTotalRequests()
	{
		// Arrange
		var monitor = new CacheHealthMonitor();

		// Act
		monitor.RecordCacheOperation(isHit: true);
		monitor.RecordCacheOperation(isHit: false);
		monitor.RecordCacheOperation(isHit: true);

		// Assert
		var snapshot = monitor.GetPerformanceSnapshot();
		snapshot.HitCount.ShouldBe(2);
		snapshot.MissCount.ShouldBe(1);
	}

	[Fact]
	public void RecordCacheOperation_TracksErrors()
	{
		// Arrange
		var monitor = new CacheHealthMonitor();

		// Act
		monitor.RecordCacheOperation(isHit: true, hasError: true);
		monitor.RecordCacheOperation(isHit: false, hasError: true);
		monitor.RecordCacheOperation(isHit: true, hasError: false);

		// Assert
		var snapshot = monitor.GetPerformanceSnapshot();
		snapshot.TotalErrors.ShouldBe(2);
	}

	[Fact]
	public void GetPerformanceSnapshot_ReturnsTimestamp()
	{
		// Arrange
		var monitor = new CacheHealthMonitor();

		// Act
		var snapshot = monitor.GetPerformanceSnapshot();

		// Assert
		snapshot.Timestamp.ShouldNotBe(default);
	}

	[Fact]
	public void GetPerformanceSnapshot_CalculatesMissCount()
	{
		// Arrange
		var monitor = new CacheHealthMonitor();
		monitor.RecordCacheOperation(isHit: true);
		monitor.RecordCacheOperation(isHit: false);
		monitor.RecordCacheOperation(isHit: false);

		// Act
		var snapshot = monitor.GetPerformanceSnapshot();

		// Assert
		snapshot.MissCount.ShouldBe(2);
		snapshot.HitCount.ShouldBe(1);
	}
}
