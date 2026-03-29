// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Caching;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Tests.Shared.Helpers;

namespace Excalibur.Dispatch.Caching.Tests;

/// <summary>
/// Unit tests for InMemoryCacheTagTracker bounded capacity (Sprint 723 T.4 nhxilc).
/// Verifies skip-when-full behavior and one-time warning log.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InMemoryCacheTagTrackerBoundedCapacityShould
{
	private static InMemoryCacheTagTracker CreateBoundedTracker(int capacity)
	{
		return new InMemoryCacheTagTracker(
			new TestMeterFactory(),
			Microsoft.Extensions.Options.Options.Create(new CacheOptions { TagTrackerCapacity = capacity }),
			NullLogger<InMemoryCacheTagTracker>.Instance);
	}

	[Fact]
	public async Task AcceptRegistrationsUnderCapacity()
	{
		// Arrange
		var tracker = CreateBoundedTracker(capacity: 5);

		// Act
		await tracker.RegisterKeyAsync("key1", ["tag1"], CancellationToken.None);
		await tracker.RegisterKeyAsync("key2", ["tag1"], CancellationToken.None);

		// Assert
		var keys = await tracker.GetKeysByTagsAsync(["tag1"], CancellationToken.None);
		keys.Count.ShouldBe(2);
	}

	[Fact]
	public async Task SkipRegistration_WhenCapacityReached()
	{
		// Arrange -- capacity of 3
		var tracker = CreateBoundedTracker(capacity: 3);

		// Fill to capacity
		await tracker.RegisterKeyAsync("key1", ["tag1"], CancellationToken.None);
		await tracker.RegisterKeyAsync("key2", ["tag1"], CancellationToken.None);
		await tracker.RegisterKeyAsync("key3", ["tag1"], CancellationToken.None);

		// Act -- this should be silently skipped
		await tracker.RegisterKeyAsync("key4", ["tag1"], CancellationToken.None);

		// Assert -- key4 should NOT be tracked
		var keys = await tracker.GetKeysByTagsAsync(["tag1"], CancellationToken.None);
		keys.ShouldContain("key1");
		keys.ShouldContain("key2");
		keys.ShouldContain("key3");
		keys.ShouldNotContain("key4");
	}

	[Fact]
	public async Task NotThrow_WhenCapacityExceeded()
	{
		// Arrange
		var tracker = CreateBoundedTracker(capacity: 1);
		await tracker.RegisterKeyAsync("key1", ["tag1"], CancellationToken.None);

		// Act & Assert -- should not throw
		await tracker.RegisterKeyAsync("key2", ["tag1"], CancellationToken.None);
		await tracker.RegisterKeyAsync("key3", ["tag1"], CancellationToken.None);
	}

	[Fact]
	public async Task AllowRegistration_AfterUnregisterFreesCapacity()
	{
		// Arrange -- capacity of 2
		var tracker = CreateBoundedTracker(capacity: 2);
		await tracker.RegisterKeyAsync("key1", ["tag1"], CancellationToken.None);
		await tracker.RegisterKeyAsync("key2", ["tag1"], CancellationToken.None);

		// Act -- free one slot and register new key
		await tracker.UnregisterKeyAsync("key1", CancellationToken.None);
		await tracker.RegisterKeyAsync("key3", ["tag1"], CancellationToken.None);

		// Assert
		var keys = await tracker.GetKeysByTagsAsync(["tag1"], CancellationToken.None);
		keys.ShouldNotContain("key1");
		keys.ShouldContain("key2");
		keys.ShouldContain("key3");
	}

	[Fact]
	public async Task UseDefaultCapacity_WhenZeroOrNegative()
	{
		// Arrange -- capacity 0 should use default 10K
		var tracker = CreateBoundedTracker(capacity: 0);

		// Act -- should accept many registrations
		for (var i = 0; i < 100; i++)
		{
			await tracker.RegisterKeyAsync($"key{i}", ["tag1"], CancellationToken.None);
		}

		// Assert
		var keys = await tracker.GetKeysByTagsAsync(["tag1"], CancellationToken.None);
		keys.Count.ShouldBe(100);
	}
}
