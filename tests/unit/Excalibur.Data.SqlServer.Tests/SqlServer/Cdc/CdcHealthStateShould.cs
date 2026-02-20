// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.SqlServer.Cdc;

namespace Excalibur.Data.Tests.SqlServer.Cdc;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CdcHealthStateShould
{
	[Fact]
	public void HaveZeroTotalProcessedByDefault()
	{
		var state = new CdcHealthState();

		state.TotalProcessed.ShouldBe(0);
	}

	[Fact]
	public void HaveZeroTotalFailedByDefault()
	{
		var state = new CdcHealthState();

		state.TotalFailed.ShouldBe(0);
	}

	[Fact]
	public void HaveZeroTotalCyclesByDefault()
	{
		var state = new CdcHealthState();

		state.TotalCycles.ShouldBe(0);
	}

	[Fact]
	public void HaveNullLastActivityTimeByDefault()
	{
		var state = new CdcHealthState();

		state.LastActivityTime.ShouldBeNull();
	}

	[Fact]
	public void NotBeRunningByDefault()
	{
		var state = new CdcHealthState();

		state.IsRunning.ShouldBeFalse();
	}

	[Fact]
	public void TrackProcessedEvents()
	{
		var state = new CdcHealthState();

		state.RecordCycle(10, 0);

		state.TotalProcessed.ShouldBe(10);
	}

	[Fact]
	public void TrackFailedEvents()
	{
		var state = new CdcHealthState();

		state.RecordCycle(0, 5);

		state.TotalFailed.ShouldBe(5);
	}

	[Fact]
	public void IncrementCycleCount()
	{
		var state = new CdcHealthState();

		state.RecordCycle(1, 0);
		state.RecordCycle(2, 1);

		state.TotalCycles.ShouldBe(2);
	}

	[Fact]
	public void AccumulateProcessedAndFailedAcrossMultipleCycles()
	{
		var state = new CdcHealthState();

		state.RecordCycle(10, 2);
		state.RecordCycle(20, 3);

		state.TotalProcessed.ShouldBe(30);
		state.TotalFailed.ShouldBe(5);
		state.TotalCycles.ShouldBe(2);
	}

	[Fact]
	public void NotIncrementProcessedWhenZero()
	{
		var state = new CdcHealthState();

		state.RecordCycle(0, 0);

		state.TotalProcessed.ShouldBe(0);
		state.TotalFailed.ShouldBe(0);
		state.TotalCycles.ShouldBe(1);
	}

	[Fact]
	public void SetLastActivityTimeOnRecordCycle()
	{
		var state = new CdcHealthState();
		var before = DateTimeOffset.UtcNow;

		state.RecordCycle(1, 0);

		state.LastActivityTime.ShouldNotBeNull();
		state.LastActivityTime!.Value.ShouldBeGreaterThanOrEqualTo(before);
	}

	[Fact]
	public void MarkAsStarted()
	{
		var state = new CdcHealthState();

		state.MarkStarted();

		state.IsRunning.ShouldBeTrue();
		state.LastActivityTime.ShouldNotBeNull();
	}

	[Fact]
	public void MarkAsStopped()
	{
		var state = new CdcHealthState();
		state.MarkStarted();

		state.MarkStopped();

		state.IsRunning.ShouldBeFalse();
	}

	[Fact]
	public void HandleStartStopStartCycles()
	{
		var state = new CdcHealthState();

		state.MarkStarted();
		state.IsRunning.ShouldBeTrue();

		state.MarkStopped();
		state.IsRunning.ShouldBeFalse();

		state.MarkStarted();
		state.IsRunning.ShouldBeTrue();
	}
}
