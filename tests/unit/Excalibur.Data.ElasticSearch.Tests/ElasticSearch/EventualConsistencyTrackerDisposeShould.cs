// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Projections;

using Microsoft.Extensions.Options;

using Excalibur.Data.ElasticSearch;
namespace Excalibur.Data.Tests.ElasticSearch.Projections;

/// <summary>
/// Unit tests for <see cref="EventualConsistencyTracker"/> Dispose functionality.
/// Verifies Sprint 389 fix: Dispose method properly releases resources.
/// </summary>
[Trait("Category", "Unit")]
public sealed class EventualConsistencyTrackerDisposeShould : UnitTestBase
{
	[Fact]
	public void Dispose_DoesNotThrow()
	{
		// Arrange
		var tracker = CreateTracker();

		// Act & Assert - Should not throw
		var exception = Record.Exception(() => tracker.Dispose());
		exception.ShouldBeNull();
	}

	[Fact]
	public void Dispose_CanBeCalledMultipleTimes()
	{
		// Arrange
		var tracker = CreateTracker();

		// Act - Call Dispose multiple times (should be idempotent)
		var exception = Record.Exception(() =>
		{
			tracker.Dispose();
			tracker.Dispose();
			tracker.Dispose();
		});

		// Assert
		exception.ShouldBeNull();
	}

	[Fact]
	public void Constructor_ThrowsOnNullClient()
	{
		// Arrange
		var options = Options.Create(new ProjectionOptions { IndexPrefix = "test" });
		var logger = A.Fake<ILogger<EventualConsistencyTracker>>();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new EventualConsistencyTracker(null!, options, logger));
	}

	[Fact]
	public void Constructor_ThrowsOnNullOptions()
	{
		// Arrange
		var client = A.Fake<ElasticsearchClient>();
		var logger = A.Fake<ILogger<EventualConsistencyTracker>>();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new EventualConsistencyTracker(client, null!, logger));
	}

	[Fact]
	public void Constructor_ThrowsOnNullLogger()
	{
		// Arrange
		var client = A.Fake<ElasticsearchClient>();
		var options = Options.Create(new ProjectionOptions { IndexPrefix = "test" });

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new EventualConsistencyTracker(client, options, null!));
	}

	private static EventualConsistencyTracker CreateTracker()
	{
		var client = A.Fake<ElasticsearchClient>();
		var options = Options.Create(new ProjectionOptions
		{
			IndexPrefix = "test",
			ConsistencyTracking = new ConsistencyTrackingOptions
			{
				Enabled = false,
				ExpectedMaxLag = TimeSpan.FromSeconds(30)
			}
		});
		var logger = A.Fake<ILogger<EventualConsistencyTracker>>();

		return new EventualConsistencyTracker(client, options, logger);
	}
}
