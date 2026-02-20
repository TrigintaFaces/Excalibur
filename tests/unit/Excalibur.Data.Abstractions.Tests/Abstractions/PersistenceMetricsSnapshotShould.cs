// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Persistence;

namespace Excalibur.Data.Tests.Abstractions;

/// <summary>
/// Unit tests for <see cref="PersistenceMetricsSnapshot"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data.Abstractions")]
[Trait("Feature", "Metrics")]
public sealed class PersistenceMetricsSnapshotShould : UnitTestBase
{
	#region Constructor Tests

	[Fact]
	public void Create_InitializesCustomMetricsDictionary()
	{
		// Act
		var snapshot = new PersistenceMetricsSnapshot();

		// Assert
		snapshot.CustomMetrics.ShouldNotBeNull();
		snapshot.CustomMetrics.ShouldBeEmpty();
	}

	[Fact]
	public void Create_SetsTimestamp_ToApproximatelyNow()
	{
		// Arrange
		var before = DateTimeOffset.UtcNow;

		// Act
		var snapshot = new PersistenceMetricsSnapshot();

		// Assert
		var after = DateTimeOffset.UtcNow;
		snapshot.Timestamp.ShouldBeGreaterThanOrEqualTo(before);
		snapshot.Timestamp.ShouldBeLessThanOrEqualTo(after);
	}

	#endregion

	#region TotalQueries Tests

	[Fact]
	public void TotalQueries_DefaultsToZero()
	{
		// Act
		var snapshot = new PersistenceMetricsSnapshot();

		// Assert
		snapshot.TotalQueries.ShouldBe(0);
	}

	[Fact]
	public void TotalQueries_CanBeSet()
	{
		// Arrange
		var snapshot = new PersistenceMetricsSnapshot();

		// Act
		snapshot.TotalQueries = 1000;

		// Assert
		snapshot.TotalQueries.ShouldBe(1000);
	}

	[Fact]
	public void TotalQueries_CanBeSetToLargeValue()
	{
		// Arrange
		var snapshot = new PersistenceMetricsSnapshot();

		// Act
		snapshot.TotalQueries = long.MaxValue;

		// Assert
		snapshot.TotalQueries.ShouldBe(long.MaxValue);
	}

	#endregion

	#region TotalCommands Tests

	[Fact]
	public void TotalCommands_DefaultsToZero()
	{
		// Act
		var snapshot = new PersistenceMetricsSnapshot();

		// Assert
		snapshot.TotalCommands.ShouldBe(0);
	}

	[Fact]
	public void TotalCommands_CanBeSet()
	{
		// Arrange
		var snapshot = new PersistenceMetricsSnapshot();

		// Act
		snapshot.TotalCommands = 500;

		// Assert
		snapshot.TotalCommands.ShouldBe(500);
	}

	#endregion

	#region TotalErrors Tests

	[Fact]
	public void TotalErrors_DefaultsToZero()
	{
		// Act
		var snapshot = new PersistenceMetricsSnapshot();

		// Assert
		snapshot.TotalErrors.ShouldBe(0);
	}

	[Fact]
	public void TotalErrors_CanBeSet()
	{
		// Arrange
		var snapshot = new PersistenceMetricsSnapshot();

		// Act
		snapshot.TotalErrors = 25;

		// Assert
		snapshot.TotalErrors.ShouldBe(25);
	}

	#endregion

	#region CacheHits Tests

	[Fact]
	public void CacheHits_DefaultsToZero()
	{
		// Act
		var snapshot = new PersistenceMetricsSnapshot();

		// Assert
		snapshot.CacheHits.ShouldBe(0);
	}

	[Fact]
	public void CacheHits_CanBeSet()
	{
		// Arrange
		var snapshot = new PersistenceMetricsSnapshot();

		// Act
		snapshot.CacheHits = 750;

		// Assert
		snapshot.CacheHits.ShouldBe(750);
	}

	#endregion

	#region CacheMisses Tests

	[Fact]
	public void CacheMisses_DefaultsToZero()
	{
		// Act
		var snapshot = new PersistenceMetricsSnapshot();

		// Assert
		snapshot.CacheMisses.ShouldBe(0);
	}

	[Fact]
	public void CacheMisses_CanBeSet()
	{
		// Arrange
		var snapshot = new PersistenceMetricsSnapshot();

		// Act
		snapshot.CacheMisses = 250;

		// Assert
		snapshot.CacheMisses.ShouldBe(250);
	}

	#endregion

	#region AverageQueryDurationMs Tests

	[Fact]
	public void AverageQueryDurationMs_DefaultsToZero()
	{
		// Act
		var snapshot = new PersistenceMetricsSnapshot();

		// Assert
		snapshot.AverageQueryDurationMs.ShouldBe(0);
	}

	[Fact]
	public void AverageQueryDurationMs_CanBeSet()
	{
		// Arrange
		var snapshot = new PersistenceMetricsSnapshot();

		// Act
		snapshot.AverageQueryDurationMs = 15.5;

		// Assert
		snapshot.AverageQueryDurationMs.ShouldBe(15.5);
	}

	[Fact]
	public void AverageQueryDurationMs_CanStorePreciseValue()
	{
		// Arrange
		var snapshot = new PersistenceMetricsSnapshot();

		// Act
		snapshot.AverageQueryDurationMs = 25.123456789;

		// Assert
		snapshot.AverageQueryDurationMs.ShouldBe(25.123456789);
	}

	#endregion

	#region AverageCommandDurationMs Tests

	[Fact]
	public void AverageCommandDurationMs_DefaultsToZero()
	{
		// Act
		var snapshot = new PersistenceMetricsSnapshot();

		// Assert
		snapshot.AverageCommandDurationMs.ShouldBe(0);
	}

	[Fact]
	public void AverageCommandDurationMs_CanBeSet()
	{
		// Arrange
		var snapshot = new PersistenceMetricsSnapshot();

		// Act
		snapshot.AverageCommandDurationMs = 45.75;

		// Assert
		snapshot.AverageCommandDurationMs.ShouldBe(45.75);
	}

	#endregion

	#region Timestamp Tests

	[Fact]
	public void Timestamp_CanBeOverwritten()
	{
		// Arrange
		var snapshot = new PersistenceMetricsSnapshot();
		var customTimestamp = DateTimeOffset.Parse("2025-01-01T12:00:00Z");

		// Act
		snapshot.Timestamp = customTimestamp;

		// Assert
		snapshot.Timestamp.ShouldBe(customTimestamp);
	}

	#endregion

	#region CustomMetrics Tests

	[Fact]
	public void CustomMetrics_CanAddEntries()
	{
		// Arrange
		var snapshot = new PersistenceMetricsSnapshot();

		// Act
		snapshot.CustomMetrics["connection_pool_size"] = 100;
		snapshot.CustomMetrics["active_connections"] = 45;

		// Assert
		snapshot.CustomMetrics.Count.ShouldBe(2);
		snapshot.CustomMetrics["connection_pool_size"].ShouldBe(100);
		snapshot.CustomMetrics["active_connections"].ShouldBe(45);
	}

	[Fact]
	public void CustomMetrics_CanUpdateEntries()
	{
		// Arrange
		var snapshot = new PersistenceMetricsSnapshot();
		snapshot.CustomMetrics["counter"] = 10;

		// Act
		snapshot.CustomMetrics["counter"] = 20;

		// Assert
		snapshot.CustomMetrics["counter"].ShouldBe(20);
	}

	[Fact]
	public void CustomMetrics_CanRemoveEntries()
	{
		// Arrange
		var snapshot = new PersistenceMetricsSnapshot();
		snapshot.CustomMetrics["temp_metric"] = 100;

		// Act
		snapshot.CustomMetrics.Remove("temp_metric");

		// Assert
		snapshot.CustomMetrics.ContainsKey("temp_metric").ShouldBeFalse();
	}

	#endregion

	#region Full Scenario Tests

	[Fact]
	public void FullSnapshot_CanBePopulated()
	{
		// Arrange & Act
		var snapshot = new PersistenceMetricsSnapshot
		{
			TotalQueries = 10000,
			TotalCommands = 5000,
			TotalErrors = 50,
			CacheHits = 8000,
			CacheMisses = 2000,
			AverageQueryDurationMs = 12.5,
			AverageCommandDurationMs = 35.2,
			Timestamp = DateTimeOffset.UtcNow
		};
		snapshot.CustomMetrics["connections"] = 25;
		snapshot.CustomMetrics["transactions"] = 1000;

		// Assert
		snapshot.TotalQueries.ShouldBe(10000);
		snapshot.TotalCommands.ShouldBe(5000);
		snapshot.TotalErrors.ShouldBe(50);
		snapshot.CacheHits.ShouldBe(8000);
		snapshot.CacheMisses.ShouldBe(2000);
		snapshot.AverageQueryDurationMs.ShouldBe(12.5);
		snapshot.AverageCommandDurationMs.ShouldBe(35.2);
		snapshot.CustomMetrics.Count.ShouldBe(2);
	}

	#endregion
}
