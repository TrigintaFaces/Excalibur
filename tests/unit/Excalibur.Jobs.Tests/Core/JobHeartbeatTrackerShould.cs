// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Jobs.Core;

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
		var tracker = new JobHeartbeatTracker();

		// Act
		tracker.RecordHeartbeat("my-job");
		var first = tracker.GetLastHeartbeat("my-job");

		global::Tests.Shared.Infrastructure.TestTiming.Sleep(10);

		tracker.RecordHeartbeat("my-job");
		var second = tracker.GetLastHeartbeat("my-job");

		// Assert
		second!.Value.ShouldBeGreaterThanOrEqualTo(first!.Value);
	}

	[Fact]
	public void TrackMultipleJobsIndependently()
	{
		// Arrange
		var tracker = new JobHeartbeatTracker();

		// Act
		tracker.RecordHeartbeat("job-a");
		global::Tests.Shared.Infrastructure.TestTiming.Sleep(10);
		tracker.RecordHeartbeat("job-b");

		// Assert
		var a = tracker.GetLastHeartbeat("job-a");
		var b = tracker.GetLastHeartbeat("job-b");

		a.ShouldNotBeNull();
		b.ShouldNotBeNull();
		b.Value.ShouldBeGreaterThanOrEqualTo(a.Value);
	}
}
