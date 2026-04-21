// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Snapshots;

namespace Excalibur.EventSourcing.Tests.Snapshots;

/// <summary>
/// A.5 (lbcfxp): Unit tests for <see cref="AutoSnapshotPolicy.ShouldSnapshot"/> --
/// event count, time, version, custom thresholds, disabled, combined, edge cases.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class AutoSnapshotPolicyShould
{
	private static SnapshotDecisionContext CreateContext(
		long currentVersion = 10,
		long? lastSnapshotVersion = 5,
		DateTimeOffset? lastSnapshotTimestamp = null,
		int eventsSinceSnapshot = 5,
		string aggregateId = "agg-1",
		string aggregateType = "Order") =>
		new(aggregateId, aggregateType, currentVersion, lastSnapshotVersion,
			lastSnapshotTimestamp ?? DateTimeOffset.UtcNow.AddMinutes(-10),
			eventsSinceSnapshot);

	// --- Event count threshold ---

	[Fact]
	public void TriggerWhenEventCountThresholdMet()
	{
		var options = new AutoSnapshotOptions { EventCountThreshold = 5 };
		var context = CreateContext(eventsSinceSnapshot: 5);

		AutoSnapshotPolicy.ShouldSnapshot(options, context).ShouldBeTrue();
	}

	[Fact]
	public void TriggerWhenEventCountThresholdExceeded()
	{
		var options = new AutoSnapshotOptions { EventCountThreshold = 3 };
		var context = CreateContext(eventsSinceSnapshot: 10);

		AutoSnapshotPolicy.ShouldSnapshot(options, context).ShouldBeTrue();
	}

	[Fact]
	public void NotTriggerWhenEventCountBelowThreshold()
	{
		var options = new AutoSnapshotOptions { EventCountThreshold = 100 };
		var context = CreateContext(eventsSinceSnapshot: 5);

		AutoSnapshotPolicy.ShouldSnapshot(options, context).ShouldBeFalse();
	}

	// --- Time threshold ---

	[Fact]
	public void TriggerWhenTimeThresholdMet()
	{
		var options = new AutoSnapshotOptions { TimeThreshold = TimeSpan.FromMinutes(5) };
		var context = CreateContext(
			lastSnapshotTimestamp: DateTimeOffset.UtcNow.AddMinutes(-10));

		AutoSnapshotPolicy.ShouldSnapshot(options, context).ShouldBeTrue();
	}

	[Fact]
	public void NotTriggerWhenTimeThresholdNotMet()
	{
		var options = new AutoSnapshotOptions { TimeThreshold = TimeSpan.FromHours(1) };
		var context = CreateContext(
			lastSnapshotTimestamp: DateTimeOffset.UtcNow.AddMinutes(-5));

		AutoSnapshotPolicy.ShouldSnapshot(options, context).ShouldBeFalse();
	}

	[Fact]
	public void TriggerWhenNeverSnapshottedAndTimeThresholdSet()
	{
		// Never snapshotted (null timestamp) -> always trigger if time threshold is configured
		var options = new AutoSnapshotOptions { TimeThreshold = TimeSpan.FromMinutes(5) };
		var context = new SnapshotDecisionContext(
			"agg-1", "Order", 10, null, null, 10);

		AutoSnapshotPolicy.ShouldSnapshot(options, context).ShouldBeTrue();
	}

	// --- Version threshold ---

	[Fact]
	public void TriggerWhenVersionThresholdMet()
	{
		var options = new AutoSnapshotOptions { VersionThreshold = 10 };
		var context = CreateContext(currentVersion: 10);

		AutoSnapshotPolicy.ShouldSnapshot(options, context).ShouldBeTrue();
	}

	[Fact]
	public void TriggerWhenVersionThresholdExceeded()
	{
		var options = new AutoSnapshotOptions { VersionThreshold = 5 };
		var context = CreateContext(currentVersion: 50);

		AutoSnapshotPolicy.ShouldSnapshot(options, context).ShouldBeTrue();
	}

	[Fact]
	public void NotTriggerWhenVersionBelowThreshold()
	{
		var options = new AutoSnapshotOptions { VersionThreshold = 100 };
		var context = CreateContext(currentVersion: 10);

		AutoSnapshotPolicy.ShouldSnapshot(options, context).ShouldBeFalse();
	}

	// --- Custom policy ---

	[Fact]
	public void TriggerWhenCustomPolicyReturnsTrue()
	{
		var options = new AutoSnapshotOptions
		{
			CustomPolicy = ctx => ctx.EventsSinceSnapshot > 2
		};
		var context = CreateContext(eventsSinceSnapshot: 5);

		AutoSnapshotPolicy.ShouldSnapshot(options, context).ShouldBeTrue();
	}

	[Fact]
	public void NotTriggerWhenCustomPolicyReturnsFalse()
	{
		var options = new AutoSnapshotOptions
		{
			CustomPolicy = _ => false
		};
		var context = CreateContext();

		AutoSnapshotPolicy.ShouldSnapshot(options, context).ShouldBeFalse();
	}

	// --- Disabled / all null ---

	[Fact]
	public void NotTriggerWhenAllThresholdsNull()
	{
		// Zero overhead path: all thresholds null -> never snapshot
		var options = new AutoSnapshotOptions();
		var context = CreateContext(currentVersion: 1000, eventsSinceSnapshot: 1000);

		AutoSnapshotPolicy.ShouldSnapshot(options, context).ShouldBeFalse();
	}

	// --- Version 1 guard ---

	[Fact]
	public void NeverTriggerAtVersion1()
	{
		// Version 1 = just created, no point snapshotting
		var options = new AutoSnapshotOptions { EventCountThreshold = 1 };
		var context = CreateContext(currentVersion: 1, eventsSinceSnapshot: 1);

		AutoSnapshotPolicy.ShouldSnapshot(options, context).ShouldBeFalse();
	}

	[Fact]
	public void NeverTriggerAtVersion0()
	{
		var options = new AutoSnapshotOptions { EventCountThreshold = 1 };
		var context = CreateContext(currentVersion: 0, eventsSinceSnapshot: 0);

		AutoSnapshotPolicy.ShouldSnapshot(options, context).ShouldBeFalse();
	}

	// --- Combined thresholds (any-match) ---

	[Fact]
	public void TriggerWhenAnyThresholdMatches()
	{
		// Event count not met, but version threshold met -> trigger
		var options = new AutoSnapshotOptions
		{
			EventCountThreshold = 100,
			VersionThreshold = 5
		};
		var context = CreateContext(currentVersion: 10, eventsSinceSnapshot: 3);

		AutoSnapshotPolicy.ShouldSnapshot(options, context).ShouldBeTrue();
	}

	[Fact]
	public void NotTriggerWhenNoThresholdMatches()
	{
		var options = new AutoSnapshotOptions
		{
			EventCountThreshold = 100,
			VersionThreshold = 1000,
			TimeThreshold = TimeSpan.FromHours(24)
		};
		var context = CreateContext(
			currentVersion: 10,
			eventsSinceSnapshot: 3,
			lastSnapshotTimestamp: DateTimeOffset.UtcNow.AddMinutes(-5));

		AutoSnapshotPolicy.ShouldSnapshot(options, context).ShouldBeFalse();
	}

	// --- Edge: event count = 0 ---

	[Fact]
	public void NotTriggerWhenZeroEventsSinceSnapshot()
	{
		var options = new AutoSnapshotOptions { EventCountThreshold = 1 };
		var context = CreateContext(currentVersion: 10, eventsSinceSnapshot: 0);

		AutoSnapshotPolicy.ShouldSnapshot(options, context).ShouldBeFalse();
	}

	// --- Edge: exactly at boundary ---

	[Fact]
	public void TriggerAtExactEventCountBoundary()
	{
		var options = new AutoSnapshotOptions { EventCountThreshold = 50 };
		var context = CreateContext(currentVersion: 100, eventsSinceSnapshot: 50);

		AutoSnapshotPolicy.ShouldSnapshot(options, context).ShouldBeTrue();
	}

	[Fact]
	public void TriggerAtExactVersionBoundary()
	{
		var options = new AutoSnapshotOptions { VersionThreshold = 100 };
		var context = CreateContext(currentVersion: 100);

		AutoSnapshotPolicy.ShouldSnapshot(options, context).ShouldBeTrue();
	}
}
