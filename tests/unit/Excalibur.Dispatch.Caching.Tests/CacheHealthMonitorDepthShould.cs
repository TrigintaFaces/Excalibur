// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Caching;

namespace Excalibur.Dispatch.Caching.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Caching")]
public sealed class CacheHealthMonitorDepthShould
{
	[Fact]
	public void ReturnZeroCounts_WhenNoOperationsRecorded()
	{
		// Arrange
		var monitor = new CacheHealthMonitor();

		// Act
		var snapshot = monitor.GetPerformanceSnapshot();

		// Assert
		snapshot.HitCount.ShouldBe(0);
		snapshot.MissCount.ShouldBe(0);
		snapshot.TotalErrors.ShouldBe(0);
	}

	[Fact]
	public void TrackHitsAndMissesSeparately()
	{
		// Arrange
		var monitor = new CacheHealthMonitor();

		// Act
		monitor.RecordCacheOperation(isHit: true);
		monitor.RecordCacheOperation(isHit: true);
		monitor.RecordCacheOperation(isHit: false);
		monitor.RecordCacheOperation(isHit: false);
		monitor.RecordCacheOperation(isHit: false);

		// Assert
		var snapshot = monitor.GetPerformanceSnapshot();
		snapshot.HitCount.ShouldBe(2);
		snapshot.MissCount.ShouldBe(3);
	}

	[Fact]
	public void TrackErrorsIndependentlyOfHitsAndMisses()
	{
		// Arrange
		var monitor = new CacheHealthMonitor();

		// Act -- a hit with error and a miss without error
		monitor.RecordCacheOperation(isHit: true, hasError: true);
		monitor.RecordCacheOperation(isHit: false, hasError: false);

		// Assert
		var snapshot = monitor.GetPerformanceSnapshot();
		snapshot.HitCount.ShouldBe(1);
		snapshot.MissCount.ShouldBe(1);
		snapshot.TotalErrors.ShouldBe(1);
	}

	[Fact]
	public void BeThreadSafe_WhenRecordingConcurrently()
	{
		// Arrange
		var monitor = new CacheHealthMonitor();
		const int operationsPerThread = 1000;
		const int threadCount = 4;

		// Act
		Parallel.For(0, threadCount, _ =>
		{
			for (var i = 0; i < operationsPerThread; i++)
			{
				monitor.RecordCacheOperation(isHit: i % 2 == 0, hasError: i % 10 == 0);
			}
		});

		// Assert -- total requests should equal threadCount * operationsPerThread
		var snapshot = monitor.GetPerformanceSnapshot();
		var totalRequests = snapshot.HitCount + snapshot.MissCount;
		totalRequests.ShouldBe(threadCount * operationsPerThread);
	}

	[Fact]
	public void RecordMissWithError()
	{
		// Arrange
		var monitor = new CacheHealthMonitor();

		// Act
		monitor.RecordCacheOperation(isHit: false, hasError: true);

		// Assert
		var snapshot = monitor.GetPerformanceSnapshot();
		snapshot.HitCount.ShouldBe(0);
		snapshot.MissCount.ShouldBe(1);
		snapshot.TotalErrors.ShouldBe(1);
	}

	[Fact]
	public void ReturnConsistentTimestamp()
	{
		// Arrange
		var monitor = new CacheHealthMonitor();
		var before = DateTimeOffset.UtcNow;

		// Act
		var snapshot = monitor.GetPerformanceSnapshot();

		// Assert
		var after = DateTimeOffset.UtcNow;
		snapshot.Timestamp.ShouldBeGreaterThanOrEqualTo(before);
		snapshot.Timestamp.ShouldBeLessThanOrEqualTo(after);
	}

	[Fact]
	public void AllowMultipleSnapshotsWithIncreasingCounts()
	{
		// Arrange
		var monitor = new CacheHealthMonitor();

		// Act
		monitor.RecordCacheOperation(isHit: true);
		var snapshot1 = monitor.GetPerformanceSnapshot();

		monitor.RecordCacheOperation(isHit: true);
		monitor.RecordCacheOperation(isHit: false);
		var snapshot2 = monitor.GetPerformanceSnapshot();

		// Assert
		snapshot1.HitCount.ShouldBe(1);
		snapshot1.MissCount.ShouldBe(0);

		snapshot2.HitCount.ShouldBe(2);
		snapshot2.MissCount.ShouldBe(1);
	}
}
