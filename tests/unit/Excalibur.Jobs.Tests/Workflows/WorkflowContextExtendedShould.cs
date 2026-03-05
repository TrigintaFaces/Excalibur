// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Jobs.Workflows;

namespace Excalibur.Jobs.Tests.Workflows;

/// <summary>
/// Extended unit tests for <see cref="WorkflowContext"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
[Trait("Feature", "Jobs")]
public sealed class WorkflowContextExtendedShould
{
#pragma warning disable CS0618 // Type or member is obsolete (WorkflowContext is preview)

	[Fact]
	public void ThrowWhenInstanceIdIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new WorkflowContext(null!));
	}

	[Fact]
	public void StoreInstanceId()
	{
		// Act
		var context = new WorkflowContext("instance-1");

		// Assert
		context.InstanceId.ShouldBe("instance-1");
	}

	[Fact]
	public void StoreCorrelationId()
	{
		// Act
		var context = new WorkflowContext("instance-1", "corr-1");

		// Assert
		context.CorrelationId.ShouldBe("corr-1");
	}

	[Fact]
	public void HaveNullCorrelationIdByDefault()
	{
		// Act
		var context = new WorkflowContext("instance-1");

		// Assert
		context.CorrelationId.ShouldBeNull();
	}

	[Fact]
	public void HaveStartedAtSet()
	{
		// Arrange
		var before = DateTimeOffset.UtcNow;

		// Act
		var context = new WorkflowContext("instance-1");

		// Assert
		context.StartedAt.ShouldBeGreaterThanOrEqualTo(before);
		var assertionUpperBound1 = DateTimeOffset.UtcNow;
		context.StartedAt.ShouldBeLessThanOrEqualTo(assertionUpperBound1);
	}

	[Fact]
	public void AllowSettingCurrentStepId()
	{
		// Arrange
		var context = new WorkflowContext("instance-1");

		// Act
		context.CurrentStepId = "step-1";

		// Assert
		context.CurrentStepId.ShouldBe("step-1");
	}

	[Fact]
	public void HaveEmptyPropertiesByDefault()
	{
		// Act
		var context = new WorkflowContext("instance-1");

		// Assert
		context.Properties.ShouldBeEmpty();
	}

	[Fact]
	public async Task ScheduleStepWithDelayExecutesStep()
	{
		// Arrange
		var context = new WorkflowContext("instance-1");

		// Act - schedule with no delay (executes immediately)
		await context.ScheduleStepAsync("step-1", TimeSpan.Zero, "data", CancellationToken.None);

		// Assert
		context.CurrentStepId.ShouldBe("step-1");
		context.Properties.ShouldContainKey("step_step-1_executed_at");
	}

	[Fact]
	public async Task ScheduleStepWithPastTimeExecutesImmediately()
	{
		// Arrange
		var context = new WorkflowContext("instance-1");
		var pastTime = DateTimeOffset.UtcNow.AddSeconds(-10);

		// Act
		await context.ScheduleStepAsync("step-1", pastTime, "data", CancellationToken.None);

		// Assert
		context.Properties.ShouldContainKey("step_step-1_executed_at");
	}

	[Fact]
	public async Task ScheduleStepWithFutureDelay()
	{
		// Arrange
		var context = new WorkflowContext("instance-1");

		// Act - schedule 50ms in the future
		await context.ScheduleStepAsync("step-1", TimeSpan.FromMilliseconds(50), "data", CancellationToken.None);

		// Wait for step to execute
		await context.WaitForScheduledStepsAsync();

		// Assert
		context.Properties.ShouldContainKey("step_step-1_executed_at");
	}

	[Fact]
	public async Task ThrowWhenStepIdIsNullOrWhitespace()
	{
		// Arrange
		var context = new WorkflowContext("instance-1");

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(() =>
			context.ScheduleStepAsync("", TimeSpan.Zero, null, CancellationToken.None));
	}

	[Fact]
	public async Task RaiseEventCompletesWaitingTask()
	{
		// Arrange
		var context = new WorkflowContext("instance-1");
		var waitTask = context.WaitForEventAsync("my-event", null, CancellationToken.None);

		// Act
		await context.RaiseEventAsync("my-event", "event-data", CancellationToken.None);

		// Assert
		var result = await waitTask;
		result.ShouldBe("event-data");
	}

	[Fact]
	public async Task WaitForEventTimesOutWhenNoRaise()
	{
		// Arrange
		var context = new WorkflowContext("instance-1");

		// Act & Assert
		await Should.ThrowAsync<TimeoutException>(() =>
			context.WaitForEventAsync("never-event", TimeSpan.FromMilliseconds(50), CancellationToken.None));
	}

	[Fact]
	public async Task RaiseEventWithNoWaiterDoesNotThrow()
	{
		// Arrange
		var context = new WorkflowContext("instance-1");

		// Act & Assert - should not throw
		await context.RaiseEventAsync("unwaited-event", "data", CancellationToken.None);
	}

	[Fact]
	public async Task CreateCheckpointAddsToProperties()
	{
		// Arrange
		var context = new WorkflowContext("instance-1");

		// Act
		await context.CreateCheckpointAsync("checkpoint-data", CancellationToken.None);

		// Assert
		context.Properties.Count.ShouldBe(1);
		var checkpointKey = context.Properties.Keys.First();
		checkpointKey.ShouldStartWith("checkpoint_");
	}

	[Fact]
	public async Task CreateMultipleCheckpointsWithUniqueKeys()
	{
		// Arrange
		var context = new WorkflowContext("instance-1");

		// Act
		await context.CreateCheckpointAsync("data-1", CancellationToken.None);
		await context.CreateCheckpointAsync("data-2", CancellationToken.None);

		// Assert
		context.Properties.Count.ShouldBe(2);
	}

	[Fact]
	public async Task ThrowWhenScheduleStepByTimeWithNullStepId()
	{
		// Arrange
		var context = new WorkflowContext("instance-1");

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(() =>
			context.ScheduleStepAsync("", DateTimeOffset.UtcNow, null, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowWhenRaiseEventWithNullEventName()
	{
		// Arrange
		var context = new WorkflowContext("instance-1");

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(() =>
			context.RaiseEventAsync("", null, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowWhenWaitForEventWithNullEventName()
	{
		// Arrange
		var context = new WorkflowContext("instance-1");

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(() =>
			context.WaitForEventAsync("", null, CancellationToken.None));
	}

#pragma warning restore CS0618
}
