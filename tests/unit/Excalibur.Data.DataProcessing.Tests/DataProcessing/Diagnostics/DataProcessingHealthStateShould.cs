// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DataProcessing.Diagnostics;

namespace Excalibur.Data.Tests.DataProcessing.Diagnostics;

/// <summary>
/// Unit tests for <see cref="DataProcessingHealthState"/>.
/// </summary>
[UnitTest]
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class DataProcessingHealthStateShould : UnitTestBase
{
	[Fact]
	public void HaveZeroCounters_WhenCreated()
	{
		// Arrange & Act
		var state = new DataProcessingHealthState();

		// Assert
		state.TotalProcessed.ShouldBe(0);
		state.TotalFailed.ShouldBe(0);
		state.TotalCycles.ShouldBe(0);
		state.IsRunning.ShouldBeFalse();
		state.LastActivityTime.ShouldBeNull();
	}

	[Fact]
	public void MarkStarted_SetsIsRunningTrue()
	{
		// Arrange
		var state = new DataProcessingHealthState();

		// Act
		state.MarkStarted();

		// Assert
		state.IsRunning.ShouldBeTrue();
		state.LastActivityTime.ShouldNotBeNull();
	}

	[Fact]
	public void MarkStopped_SetsIsRunningFalse()
	{
		// Arrange
		var state = new DataProcessingHealthState();
		state.MarkStarted();
		state.IsRunning.ShouldBeTrue();

		// Act
		state.MarkStopped();

		// Assert
		state.IsRunning.ShouldBeFalse();
	}

	[Fact]
	public void RecordCycle_IncrementsTotalCycles()
	{
		// Arrange
		var state = new DataProcessingHealthState();

		// Act
		state.RecordCycle(succeeded: true);
		state.RecordCycle(succeeded: true);

		// Assert
		state.TotalCycles.ShouldBe(2);
		state.TotalFailed.ShouldBe(0);
	}

	[Fact]
	public void RecordCycle_IncrementsTotalFailed_WhenNotSucceeded()
	{
		// Arrange
		var state = new DataProcessingHealthState();

		// Act
		state.RecordCycle(succeeded: false);
		state.RecordCycle(succeeded: true);
		state.RecordCycle(succeeded: false);

		// Assert
		state.TotalCycles.ShouldBe(3);
		state.TotalFailed.ShouldBe(2);
	}

	[Fact]
	public void RecordCycle_UpdatesLastActivityTime()
	{
		// Arrange
		var state = new DataProcessingHealthState();
		var beforeRecord = DateTimeOffset.UtcNow;

		// Act
		state.RecordCycle(succeeded: true);

		// Assert
		state.LastActivityTime.ShouldNotBeNull();
		state.LastActivityTime!.Value.ShouldBeGreaterThanOrEqualTo(beforeRecord);
	}

	[Fact]
	public void RecordProcessed_IncrementsTotalProcessed()
	{
		// Arrange
		var state = new DataProcessingHealthState();

		// Act
		state.RecordProcessed(10);
		state.RecordProcessed(5);

		// Assert
		state.TotalProcessed.ShouldBe(15);
	}

	[Fact]
	public void RecordProcessed_IgnoresZeroOrNegativeCount()
	{
		// Arrange
		var state = new DataProcessingHealthState();

		// Act
		state.RecordProcessed(0);
		state.RecordProcessed(-1);

		// Assert
		state.TotalProcessed.ShouldBe(0);
	}

	[Fact]
	public void BeThreadSafe_UnderConcurrentAccess()
	{
		// Arrange
		var state = new DataProcessingHealthState();
		const int iterations = 1000;

		// Act — concurrent writes from multiple threads
		Parallel.For(0, iterations, _ =>
		{
			state.RecordCycle(succeeded: true);
			state.RecordProcessed(1);
		});

		// Assert — all increments should be accounted for
		state.TotalCycles.ShouldBe(iterations);
		state.TotalProcessed.ShouldBe(iterations);
	}

	[Fact]
	public void MarkStarted_UpdatesLastActivityTime()
	{
		// Arrange
		var state = new DataProcessingHealthState();
		var before = DateTimeOffset.UtcNow;

		// Act
		state.MarkStarted();

		// Assert
		state.LastActivityTime.ShouldNotBeNull();
		state.LastActivityTime!.Value.ShouldBeGreaterThanOrEqualTo(before);
		state.LastActivityTime!.Value.ShouldBeLessThanOrEqualTo(DateTimeOffset.UtcNow);
	}
}
