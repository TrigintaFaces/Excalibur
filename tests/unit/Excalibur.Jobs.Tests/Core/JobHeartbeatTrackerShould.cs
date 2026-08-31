// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Jobs.Core;

using Microsoft.Extensions.Time.Testing;

namespace Excalibur.Jobs.Tests.Core;

/// <summary>
/// Unit tests for <see cref="JobHeartbeatTracker"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
[Trait("Feature", "Jobs")]
public sealed class JobHeartbeatTrackerShould
{
	[Fact]
	public void ReturnNullForUnknownJob()
	{
		// Arrange
		var tracker = new JobHeartbeatTracker();

		// Act
		var result = tracker.GetLastHeartbeat("unknown-job");

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void RecordAndRetrieveHeartbeat()
	{
		// Arrange
		var tracker = new JobHeartbeatTracker();
		var before = DateTimeOffset.UtcNow;

		// Act
		tracker.RecordHeartbeat("my-job");
		var result = tracker.GetLastHeartbeat("my-job");

		// Assert
		result.ShouldNotBeNull();
		result.Value.ShouldBeGreaterThanOrEqualTo(before);
	}

	[Fact]
	public void UpdateHeartbeatOnSubsequentCalls()
	{
		// Arrange
		var clock = new FakeTimeProvider();
		var tracker = new JobHeartbeatTracker(clock);

		// Act
		tracker.RecordHeartbeat("my-job");
		var first = tracker.GetLastHeartbeat("my-job");

		clock.Advance(TimeSpan.FromSeconds(10));

		tracker.RecordHeartbeat("my-job");
		var second = tracker.GetLastHeartbeat("my-job");

		// Assert. Driving the clock rather than sleeping lets this be a strict comparison: with a real
		// sleep the two stamps could land in the same tick, so the assertion had to be >= and would have
		// passed even if the second call had not updated anything.
		second!.Value.ShouldBeGreaterThan(first!.Value);
	}

	[Fact]
	public void TrackMultipleJobsIndependently()
	{
		// Arrange
		var clock = new FakeTimeProvider();
		var tracker = new JobHeartbeatTracker(clock);

		// Act
		tracker.RecordHeartbeat("job-a");
		clock.Advance(TimeSpan.FromSeconds(10));
		tracker.RecordHeartbeat("job-b");

		// Assert
		var a = tracker.GetLastHeartbeat("job-a");
		var b = tracker.GetLastHeartbeat("job-b");

		a.ShouldNotBeNull();
		b.ShouldNotBeNull();
		b.Value.ShouldBeGreaterThan(a.Value);
	}
}
