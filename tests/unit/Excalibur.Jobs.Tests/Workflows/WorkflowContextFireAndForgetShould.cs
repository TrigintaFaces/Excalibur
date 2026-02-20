// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Reflection;

using Excalibur.Jobs.Workflows;

namespace Excalibur.Jobs.Tests.Workflows;

/// <summary>
/// Tests for Sprint 542 P0 fix S542.14 (bd-ik0lm):
/// WorkflowContext fire-and-forget Task.Run -> ConcurrentBag tracking.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Jobs")]
public sealed class WorkflowContextFireAndForgetShould
{
	[Fact]
	public void HaveScheduledStepsField()
	{
		var field = typeof(WorkflowContext)
			.GetField("_scheduledSteps", BindingFlags.NonPublic | BindingFlags.Instance);

		field.ShouldNotBeNull("WorkflowContext should have _scheduledSteps field");
	}

	[Fact]
	public void UsesConcurrentBagForScheduledSteps()
	{
		var field = typeof(WorkflowContext)
			.GetField("_scheduledSteps", BindingFlags.NonPublic | BindingFlags.Instance);

		field.ShouldNotBeNull();
		field.FieldType.IsGenericType.ShouldBeTrue();
		field.FieldType.GetGenericTypeDefinition().ShouldBe(typeof(ConcurrentBag<>));
		field.FieldType.GetGenericArguments()[0].ShouldBe(typeof(Task));
	}

	[Fact]
	public void HaveWaitForScheduledStepsAsyncMethod()
	{
		var method = typeof(WorkflowContext)
			.GetMethod("WaitForScheduledStepsAsync", BindingFlags.Public | BindingFlags.Instance);

		method.ShouldNotBeNull("WorkflowContext should expose WaitForScheduledStepsAsync");
		method.ReturnType.ShouldBe(typeof(Task));
	}

	[Fact]
	public async Task TrackScheduledStepsViaWaitForScheduledSteps()
	{
		// Arrange
		var context = new WorkflowContext("test-instance");

		// Act — schedule a step with a short delay
		await context.ScheduleStepAsync("tracked-step", TimeSpan.FromMilliseconds(50), "data", CancellationToken.None);

		// Wait for all scheduled steps — should NOT throw
		await context.WaitForScheduledStepsAsync();

		// Assert — step was executed
		context.Properties.ShouldContainKey("step_tracked-step_executed_at");
	}

	[Fact]
	public async Task WaitForScheduledStepsCompletesImmediatelyWhenNoSteps()
	{
		// Arrange
		var context = new WorkflowContext("test-instance");

		// Act & Assert — should complete immediately
		await context.WaitForScheduledStepsAsync();
	}

	[Fact]
	public async Task ScheduledStepCancellationIsPropagated()
	{
		// Arrange
		var context = new WorkflowContext("test-instance");
		using var cts = new CancellationTokenSource();

		// Act — schedule a step with a long delay, then cancel
		await context.ScheduleStepAsync("cancellable-step", TimeSpan.FromSeconds(30), "data", cts.Token);
		cts.Cancel();

		// Assert — waiting for scheduled steps should throw OperationCanceledException (or TaskCanceledException)
		await Should.ThrowAsync<TaskCanceledException>(
			async () => await context.WaitForScheduledStepsAsync());
	}
}
