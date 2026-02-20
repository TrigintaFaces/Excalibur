// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CS0618 // Type or member is obsolete — testing preview WorkflowContext

using Excalibur.Jobs.Workflows;

namespace Excalibur.Jobs.Tests.Core;

/// <summary>
/// Depth tests for <see cref="WorkflowContext"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Jobs")]
public sealed class WorkflowContextDepthShould
{
	[Fact]
	public void ThrowArgumentNullException_WhenInstanceIdIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new WorkflowContext(null!));
	}

	[Fact]
	public void SetInstanceIdCorrectly()
	{
		var ctx = new WorkflowContext("inst-1");

		ctx.InstanceId.ShouldBe("inst-1");
	}

	[Fact]
	public void SetCorrelationIdCorrectly()
	{
		var ctx = new WorkflowContext("inst-1", "corr-1");

		ctx.CorrelationId.ShouldBe("corr-1");
	}

	[Fact]
	public void AllowNullCorrelationId()
	{
		var ctx = new WorkflowContext("inst-1");

		ctx.CorrelationId.ShouldBeNull();
	}

	[Fact]
	public void HaveStartedAtSet()
	{
		var before = DateTimeOffset.UtcNow;
		var ctx = new WorkflowContext("inst-1");
		var after = DateTimeOffset.UtcNow;

		ctx.StartedAt.ShouldBeGreaterThanOrEqualTo(before);
		ctx.StartedAt.ShouldBeLessThanOrEqualTo(after);
	}

	[Fact]
	public void HaveEmptyPropertiesInitially()
	{
		var ctx = new WorkflowContext("inst-1");

		ctx.Properties.ShouldBeEmpty();
	}

	[Fact]
	public void AllowSettingAndGettingProperties()
	{
		var ctx = new WorkflowContext("inst-1");

		ctx.Properties["key1"] = "value1";

		ctx.Properties["key1"].ShouldBe("value1");
	}

	[Fact]
	public async Task ScheduleStepAsync_WithPastTime_ExecutesImmediately()
	{
		// Arrange
		var ctx = new WorkflowContext("inst-1");
		var pastTime = DateTimeOffset.UtcNow.AddMinutes(-1);

		// Act
		await ctx.ScheduleStepAsync("step-1", pastTime, "data", CancellationToken.None);

		// Assert
		ctx.CurrentStepId.ShouldBe("step-1");
		ctx.Properties.ShouldContainKey("step_step-1_executed_at");
		ctx.Properties.ShouldContainKey("step_step-1_data");
	}

	[Fact]
	public async Task ScheduleStepAsync_WithTimeSpan_ZeroDelay_ExecutesImmediately()
	{
		// Arrange
		var ctx = new WorkflowContext("inst-1");

		// Act
		await ctx.ScheduleStepAsync("step-2", TimeSpan.Zero, "data2", CancellationToken.None);

		// Assert
		ctx.CurrentStepId.ShouldBe("step-2");
		ctx.Properties["step_step-2_data"].ShouldBe("data2");
	}

	[Fact]
	public async Task ScheduleStepAsync_ThrowsOnNullStepId()
	{
		var ctx = new WorkflowContext("inst-1");

		await Should.ThrowAsync<ArgumentException>(() =>
			ctx.ScheduleStepAsync(null!, DateTimeOffset.UtcNow, null, CancellationToken.None));
	}

	[Fact]
	public async Task ScheduleStepAsync_ThrowsOnEmptyStepId()
	{
		var ctx = new WorkflowContext("inst-1");

		await Should.ThrowAsync<ArgumentException>(() =>
			ctx.ScheduleStepAsync("", TimeSpan.Zero, null, CancellationToken.None));
	}

	[Fact]
	public async Task WaitForEventAsync_ThrowsOnNullEventName()
	{
		var ctx = new WorkflowContext("inst-1");

		await Should.ThrowAsync<ArgumentException>(() =>
			ctx.WaitForEventAsync(null!, null, CancellationToken.None));
	}

	[Fact]
	public async Task WaitForEventAsync_CompletesWhenEventRaised()
	{
		// Arrange
		var ctx = new WorkflowContext("inst-1");

		// Start waiting in background
		var waitTask = ctx.WaitForEventAsync("test-event", null, CancellationToken.None);

		// Act — raise the event
		await ctx.RaiseEventAsync("test-event", "event-data", CancellationToken.None);

		// Assert
		var result = await waitTask;
		result.ShouldBe("event-data");
	}

	[Fact]
	public async Task WaitForEventAsync_ThrowsTimeoutException_WhenTimedOut()
	{
		// Arrange
		var ctx = new WorkflowContext("inst-1");

		// Act & Assert
		await Should.ThrowAsync<TimeoutException>(() =>
			ctx.WaitForEventAsync("never-event", TimeSpan.FromMilliseconds(50), CancellationToken.None));
	}

	[Fact]
	public async Task WaitForEventAsync_RespectsCancellationToken()
	{
		// Arrange
		var ctx = new WorkflowContext("inst-1");
		using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

		// Act & Assert
		await Should.ThrowAsync<OperationCanceledException>(() =>
			ctx.WaitForEventAsync("event", null, cts.Token));
	}

	[Fact]
	public async Task RaiseEventAsync_ThrowsOnNullEventName()
	{
		var ctx = new WorkflowContext("inst-1");

		await Should.ThrowAsync<ArgumentException>(() =>
			ctx.RaiseEventAsync(null!, null, CancellationToken.None));
	}

	[Fact]
	public async Task RaiseEventAsync_DoesNothingWhenNoPendingWaiters()
	{
		// Arrange
		var ctx = new WorkflowContext("inst-1");

		// Act — should not throw
		await ctx.RaiseEventAsync("no-waiter", "data", CancellationToken.None);
	}

	[Fact]
	public async Task CreateCheckpointAsync_StoresCheckpointData()
	{
		// Arrange
		var ctx = new WorkflowContext("inst-1");

		// Act
		await ctx.CreateCheckpointAsync("checkpoint-data", CancellationToken.None);

		// Assert
		ctx.Properties.ShouldContain(
			kvp => kvp.Key.StartsWith("checkpoint_", StringComparison.Ordinal) &&
				   (string?)kvp.Value == "checkpoint-data");
	}

	[Fact]
	public async Task CreateCheckpointAsync_CreatesUniqueKeys()
	{
		// Arrange
		var ctx = new WorkflowContext("inst-1");

		// Act
		await ctx.CreateCheckpointAsync("cp1", CancellationToken.None);
		await ctx.CreateCheckpointAsync("cp2", CancellationToken.None);

		// Assert — should have 2 different checkpoint entries
		var checkpoints = ctx.Properties
			.Where(kvp => kvp.Key.StartsWith("checkpoint_", StringComparison.Ordinal))
			.ToList();

		checkpoints.Count.ShouldBe(2);
	}

	[Fact]
	public async Task WaitForScheduledStepsAsync_CompletesWhenNoSteps()
	{
		// Arrange
		var ctx = new WorkflowContext("inst-1");

		// Act & Assert — should complete immediately
		await ctx.WaitForScheduledStepsAsync();
	}

	[Fact]
	public void CurrentStepId_IsSettable()
	{
		var ctx = new WorkflowContext("inst-1");

		ctx.CurrentStepId = "current-step";

		ctx.CurrentStepId.ShouldBe("current-step");
	}
}
