// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Jobs.Workflows;

namespace Excalibur.Jobs.Tests.Workflows;

/// <summary>
/// Unit tests for <see cref="WorkflowContext"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Jobs")]
[Trait("Feature", "Workflows")]
public sealed class WorkflowContextShould
{
	[Fact]
	public void CreateWithInstanceId()
	{
		// Act
		var context = new WorkflowContext("instance-123");

		// Assert
		context.InstanceId.ShouldBe("instance-123");
		context.CorrelationId.ShouldBeNull();
	}

	[Fact]
	public void CreateWithInstanceIdAndCorrelationId()
	{
		// Act
		var context = new WorkflowContext("instance-123", "correlation-456");

		// Assert
		context.InstanceId.ShouldBe("instance-123");
		context.CorrelationId.ShouldBe("correlation-456");
	}

	[Fact]
	public void ThrowOnNullInstanceId()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new WorkflowContext(null!));
	}

	[Fact]
	public void HaveStartedAtTimestamp()
	{
		// Arrange
		var before = DateTimeOffset.UtcNow;

		// Act
		var context = new WorkflowContext("instance");

		// Assert
		var after = DateTimeOffset.UtcNow;
		context.StartedAt.ShouldBeGreaterThanOrEqualTo(before);
		context.StartedAt.ShouldBeLessThanOrEqualTo(after);
	}

	[Fact]
	public void HaveEmptyPropertiesByDefault()
	{
		// Act
		var context = new WorkflowContext("instance");

		// Assert
		context.Properties.ShouldNotBeNull();
		context.Properties.Count.ShouldBe(0);
	}

	[Fact]
	public void HaveNullCurrentStepByDefault()
	{
		// Act
		var context = new WorkflowContext("instance");

		// Assert
		context.CurrentStepId.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingCurrentStep()
	{
		// Arrange
		var context = new WorkflowContext("instance");

		// Act
		context.CurrentStepId = "step-1";

		// Assert
		context.CurrentStepId.ShouldBe("step-1");
	}

	[Fact]
	public void AllowAddingProperties()
	{
		// Arrange
		var context = new WorkflowContext("instance");

		// Act
		context.Properties["key1"] = "value1";
		context.Properties["key2"] = 42;

		// Assert
		context.Properties["key1"].ShouldBe("value1");
		context.Properties["key2"].ShouldBe(42);
	}

	[Fact]
	public async Task CreateCheckpoint()
	{
		// Arrange
		var context = new WorkflowContext("instance");
		var checkpointData = new { State = "processing", Count = 10 };

		// Act
		await context.CreateCheckpointAsync(checkpointData, CancellationToken.None);

		// Assert
		context.Properties.Count.ShouldBeGreaterThan(0);
		context.Properties.Keys.ShouldContain(k => k.StartsWith("checkpoint_"));
	}

	[Fact]
	public async Task RaiseAndWaitForEvent()
	{
		// Arrange
		var context = new WorkflowContext("instance");
		var eventData = "approval-granted";

		// Act - start waiting in background
		var waitTask = context.WaitForEventAsync("approval", TimeSpan.FromSeconds(5), CancellationToken.None);

		// Give a small delay to ensure the wait is registered
		await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(50);

		// Raise the event
		await context.RaiseEventAsync("approval", eventData, CancellationToken.None);

		// Get result
		var result = await waitTask;

		// Assert
		result.ShouldBe(eventData);
	}

	[Fact]
	public async Task TimeoutWhenEventNotRaised()
	{
		// Arrange
		var context = new WorkflowContext("instance");

		// Act & Assert
		await Should.ThrowAsync<TimeoutException>(async () =>
			await context.WaitForEventAsync("never-raised", TimeSpan.FromMilliseconds(100), CancellationToken.None));
	}

	[Fact]
	public async Task ThrowOnNullEventName()
	{
		// Arrange
		var context = new WorkflowContext("instance");

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await context.WaitForEventAsync(null!, null, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowOnEmptyEventName()
	{
		// Arrange
		var context = new WorkflowContext("instance");

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await context.WaitForEventAsync("", null, CancellationToken.None));
	}

	[Fact]
	public async Task ScheduleStepImmediatelyWhenDelayIsZero()
	{
		// Arrange
		var context = new WorkflowContext("instance");

		// Act
		await context.ScheduleStepAsync("immediate-step", TimeSpan.Zero, "step-data", CancellationToken.None);

		// Assert
		context.CurrentStepId.ShouldBe("immediate-step");
		context.Properties.ShouldContainKey("step_immediate-step_executed_at");
	}

	[Fact]
	public async Task ScheduleStepImmediatelyWhenTimeIsInPast()
	{
		// Arrange
		var context = new WorkflowContext("instance");
		var pastTime = DateTimeOffset.UtcNow.AddMinutes(-1);

		// Act
		await context.ScheduleStepAsync("past-step", pastTime, "step-data", CancellationToken.None);

		// Assert
		context.CurrentStepId.ShouldBe("past-step");
		context.Properties.ShouldContainKey("step_past-step_executed_at");
	}

	[Fact]
	public async Task ThrowOnNullStepId()
	{
		// Arrange
		var context = new WorkflowContext("instance");

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await context.ScheduleStepAsync(null!, TimeSpan.FromSeconds(1), null, CancellationToken.None));
	}
}
