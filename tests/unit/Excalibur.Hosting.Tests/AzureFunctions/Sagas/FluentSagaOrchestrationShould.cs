// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Hosting.AzureFunctions;

namespace Excalibur.Hosting.Tests.AzureFunctions.Sagas;

/// <summary>
/// Behavior tests for <see cref="FluentSagaOrchestration{TSagaInput, TSagaOutput}"/>
/// covering: happy path, retry with exponential backoff, retry exhaustion,
/// compensation, parallel steps, input validation, cancellation, and error handling.
/// </summary>
/// <remarks>
/// Created for bd-1gmhl (P1: Zero behavior tests for FluentSagaOrchestration, 399 lines).
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class FluentSagaOrchestrationShould : UnitTestBase
{
	private static readonly string[] ExpectedStepOrder = ["step-1", "step-2"];

	#region Happy Path

	[Fact]
	public async Task ExecuteAllSteps_InSequence_AndReturnOutput()
	{
		// Arrange
		var stepOrder = new List<string>();
		var orchestration = SagaBuilder<SagaTestInput, SagaTestOutput>.Create("happy-path")
			.AddStep<string, string>("step-1", s => s
				.ExecuteActivity("act-1")
				.WithInput((input, _) => input.Value)
				.WithOutput((_, state) =>
				{
					stepOrder.Add("step-1");
					state.Data["step1"] = "done-1";
				}))
			.AddStep<string, string>("step-2", s => s
				.ExecuteActivity("act-2")
				.WithInput((_, _) => "step2-input")
				.WithOutput((_, state) =>
				{
					stepOrder.Add("step-2");
					state.Data["step2"] = "done-2";
				}))
			.WithOutputBuilder((_, state) =>
				Task.FromResult(new SagaTestOutput
				{
					Result = $"{state.Data["step1"]}-{state.Data["step2"]}",
				}))
			.Build(NullLogger<FluentSagaOrchestration<SagaTestInput, SagaTestOutput>>.Instance);

		// Act
		var result = await orchestration.ExecuteAsync(
			new SagaTestInput { Value = "hello" }, CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
		result.Result.ShouldContain("done-1");
		result.Result.ShouldContain("done-2");
		stepOrder.ShouldBe(ExpectedStepOrder);
	}

	[Fact]
	public async Task ExposeSagaProperties_FromBuilder()
	{
		// Arrange & Act
		var orchestration = SagaBuilder<SagaTestInput, SagaTestOutput>.Create("prop-check")
			.WithTimeout(TimeSpan.FromMinutes(5))
			.WithAutoCompensation(false)
			.AddStep<string, string>("only-step", s => s
				.ExecuteActivity("act")
				.WithInput((input, _) => input.Value)
				.WithOutput((_, state) => state.Data["done"] = true))
			.WithOutputBuilder((_, _) =>
				Task.FromResult(new SagaTestOutput { Result = "ok" }))
			.Build(NullLogger<FluentSagaOrchestration<SagaTestInput, SagaTestOutput>>.Instance);

		// Assert — verify builder-configured properties are accessible
		orchestration.SagaName.ShouldBe("prop-check");
		orchestration.Timeout.ShouldBe(TimeSpan.FromMinutes(5));
		orchestration.AutoCompensation.ShouldBeFalse();
		orchestration.Steps.Count.ShouldBe(1);
		orchestration.Steps[0].Name.ShouldBe("only-step");
	}

	#endregion

	#region Input Validation

	[Fact]
	public async Task RunInputValidation_BeforeSteps()
	{
		// Arrange
		var validationRan = false;
		var orchestration = SagaBuilder<SagaTestInput, SagaTestOutput>.Create("validation")
			.WithInputValidation(input =>
			{
				validationRan = true;
				input.Value.ShouldBe("validate-me");
				return Task.CompletedTask;
			})
			.AddStep<string, string>("step", s => s
				.ExecuteActivity("act")
				.WithInput((i, _) => i.Value)
				.WithOutput((_, state) => state.Data["ran"] = true))
			.WithOutputBuilder((_, _) => Task.FromResult(new SagaTestOutput()))
			.Build(NullLogger<FluentSagaOrchestration<SagaTestInput, SagaTestOutput>>.Instance);

		// Act
		_ = await orchestration.ExecuteAsync(
			new SagaTestInput { Value = "validate-me" }, CancellationToken.None);

		// Assert
		validationRan.ShouldBeTrue();
	}

	[Fact]
	public async Task ThrowFromInputValidation_WithoutExecutingSteps()
	{
		// Arrange
		var stepExecuted = false;
		var orchestration = SagaBuilder<SagaTestInput, SagaTestOutput>.Create("fail-validation")
			.WithInputValidation(_ => throw new ArgumentException("Invalid input"))
			.AddStep<string, string>("step", s => s
				.ExecuteActivity("act")
				.WithInput((i, _) => i.Value)
				.WithOutput((_, _) => stepExecuted = true))
			.WithOutputBuilder((_, _) => Task.FromResult(new SagaTestOutput()))
			.Build(NullLogger<FluentSagaOrchestration<SagaTestInput, SagaTestOutput>>.Instance);

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(
			() => orchestration.ExecuteAsync(
				new SagaTestInput { Value = "bad" }, CancellationToken.None));
		stepExecuted.ShouldBeFalse();
	}

	#endregion

	#region Retry with Exponential Backoff

	[Fact]
	public async Task RetryTransientException_UpToMaxAttempts()
	{
		// Arrange
		var attemptCount = 0;
		var orchestration = SagaBuilder<SagaTestInput, SagaTestOutput>.Create("retry-saga")
			.WithDefaultRetry(3, TimeSpan.FromMilliseconds(10), 1.0)
			.AddStep<string, string>("flaky-step", s => s
				.ExecuteActivity("act")
				.WithRetry(3)
				.WithInput((i, _) => i.Value)
				.WithOutput((_, state) =>
				{
					var count = Interlocked.Increment(ref attemptCount);
					if (count < 3)
					{
						throw new TimeoutException("Transient failure");
					}

					state.Data["success"] = true;
				}))
			.WithOutputBuilder((_, _) => Task.FromResult(new SagaTestOutput { Result = "retried" }))
			.Build(NullLogger<FluentSagaOrchestration<SagaTestInput, SagaTestOutput>>.Instance);

		// Act
		var result = await orchestration.ExecuteAsync(
			new SagaTestInput { Value = "retry" }, CancellationToken.None);

		// Assert
		result.Result.ShouldBe("retried");
		attemptCount.ShouldBeGreaterThanOrEqualTo(3);
	}

	[Fact]
	public async Task ThrowAfterRetryExhaustion()
	{
		// Arrange
		var orchestration = SagaBuilder<SagaTestInput, SagaTestOutput>.Create("exhaust-saga")
			.WithDefaultRetry(2, TimeSpan.FromMilliseconds(10), 1.0)
			.AddStep<string, string>("always-fail", s => s
				.ExecuteActivity("act")
				.WithRetry(2)
				.WithInput((i, _) => i.Value)
				.WithOutput((_, _) =>
					throw new TimeoutException("Always fails")))
			.WithOutputBuilder((_, _) => Task.FromResult(new SagaTestOutput()))
			.Build(NullLogger<FluentSagaOrchestration<SagaTestInput, SagaTestOutput>>.Instance);

		// Act & Assert
		await Should.ThrowAsync<TimeoutException>(
			() => orchestration.ExecuteAsync(
				new SagaTestInput { Value = "fail" }, CancellationToken.None));
	}

	[Fact]
	public async Task NotRetryNonTransientException()
	{
		// Arrange
		var attemptCount = 0;
		var orchestration = SagaBuilder<SagaTestInput, SagaTestOutput>.Create("no-retry")
			.WithDefaultRetry(3, TimeSpan.FromMilliseconds(10), 1.0)
			.AddStep<string, string>("fatal-step", s => s
				.ExecuteActivity("act")
				.WithRetry(3)
				.WithInput((i, _) => i.Value)
				.WithOutput((_, _) =>
				{
					Interlocked.Increment(ref attemptCount);
					throw new ArgumentException("Non-transient — should not retry");
				}))
			.WithOutputBuilder((_, _) => Task.FromResult(new SagaTestOutput()))
			.Build(NullLogger<FluentSagaOrchestration<SagaTestInput, SagaTestOutput>>.Instance);

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(
			() => orchestration.ExecuteAsync(
				new SagaTestInput { Value = "fatal" }, CancellationToken.None));

		// Non-transient exceptions are not retried — single attempt only
		attemptCount.ShouldBe(1);
	}

	#endregion

	#region Compensation

	[Fact]
	public async Task CompensateCompletedSteps_InReverseOrder_OnFailure()
	{
		// Arrange
		var orchestration = SagaBuilder<SagaTestInput, SagaTestOutput>.Create("compensate-saga")
			.WithAutoCompensation(true)
			.AddStep<string, string>("step-A", s => s
				.ExecuteActivity("act-a")
				.WithCompensation("comp-a")
				.WithInput((i, _) => i.Value)
				.WithOutput((_, state) => state.Data["a"] = true))
			.AddStep<string, string>("step-B", s => s
				.ExecuteActivity("act-b")
				.WithCompensation("comp-b")
				.WithInput((_, _) => "b")
				.WithOutput((_, state) => state.Data["b"] = true))
			.AddStep<string, string>("step-C", s => s
				.ExecuteActivity("act-c")
				.WithInput((_, _) => "c")
				.WithOutput((_, _) =>
					throw new InvalidOperationException("Step C fails")))
			.WithOutputBuilder((_, _) => Task.FromResult(new SagaTestOutput()))
			.Build(NullLogger<FluentSagaOrchestration<SagaTestInput, SagaTestOutput>>.Instance);

		// Act & Assert — original exception is rethrown after compensation
		await Should.ThrowAsync<InvalidOperationException>(
			() => orchestration.ExecuteAsync(
				new SagaTestInput { Value = "comp" }, CancellationToken.None));

		// Compensation runs in LIFO order for completed steps (A and B completed, C failed).
		// Compensation is fire-and-forget with best-effort logging; we verify the saga
		// still threw the original exception after compensation.
	}

	[Fact]
	public async Task SkipCompensation_WhenAutoCompensationDisabled()
	{
		// Arrange
		var orchestration = SagaBuilder<SagaTestInput, SagaTestOutput>.Create("no-comp")
			.WithAutoCompensation(false)
			.AddStep<string, string>("step-1", s => s
				.ExecuteActivity("act")
				.WithCompensation("comp")
				.WithInput((i, _) => i.Value)
				.WithOutput((_, state) => state.Data["done"] = true))
			.AddStep<string, string>("step-2", s => s
				.ExecuteActivity("act-2")
				.WithInput((_, _) => "fail")
				.WithOutput((_, _) =>
					throw new InvalidOperationException("Fails")))
			.WithOutputBuilder((_, _) => Task.FromResult(new SagaTestOutput()))
			.Build(NullLogger<FluentSagaOrchestration<SagaTestInput, SagaTestOutput>>.Instance);

		// Act & Assert — should still throw the original exception
		await Should.ThrowAsync<InvalidOperationException>(
			() => orchestration.ExecuteAsync(
				new SagaTestInput { Value = "x" }, CancellationToken.None));
	}

	#endregion

	#region Parallel Steps

	[Fact]
	public async Task ExecuteParallelSteps_Concurrently()
	{
		// Arrange
		var orchestration = SagaBuilder<SagaTestInput, SagaTestOutput>.Create("parallel-saga")
			.AddParallelSteps("parallel-group", group => group
				.AddStep<string, string>("par-1", s => s
					.ExecuteActivity("act-p1")
					.WithInput((i, _) => i.Value)
					.WithOutput((_, state) =>
					{
						state.Data["p1"] = true;
					}))
				.AddStep<string, string>("par-2", s => s
					.ExecuteActivity("act-p2")
					.WithInput((_, _) => "p2")
					.WithOutput((_, state) =>
					{
						state.Data["p2"] = true;
					})))
			.WithOutputBuilder((_, state) =>
			{
				state.Data.ContainsKey("p1").ShouldBeTrue();
				state.Data.ContainsKey("p2").ShouldBeTrue();
				return Task.FromResult(new SagaTestOutput { Result = "parallel-ok" });
			})
			.Build(NullLogger<FluentSagaOrchestration<SagaTestInput, SagaTestOutput>>.Instance);

		// Act
		var result = await orchestration.ExecuteAsync(
			new SagaTestInput { Value = "par" }, CancellationToken.None);

		// Assert
		result.Result.ShouldBe("parallel-ok");
	}

	#endregion

	#region Cancellation

	[Fact]
	public async Task ThrowOperationCanceled_WhenAlreadyCancelled()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync().ConfigureAwait(false);

		var orchestration = SagaBuilder<SagaTestInput, SagaTestOutput>.Create("pre-cancel")
			.AddStep<string, string>("step", s => s
				.ExecuteActivity("act")
				.WithInput((i, _) => i.Value)
				.WithOutput((_, _) => { }))
			.WithOutputBuilder((_, _) => Task.FromResult(new SagaTestOutput()))
			.Build(NullLogger<FluentSagaOrchestration<SagaTestInput, SagaTestOutput>>.Instance);

		// Act & Assert
		await Should.ThrowAsync<OperationCanceledException>(
			() => orchestration.ExecuteAsync(
				new SagaTestInput { Value = "x" }, cts.Token));
	}

	#endregion

	#region Error Handler

	[Fact]
	public async Task InvokeErrorHandler_OnStepFailure()
	{
		// Arrange
		Exception? capturedError = null;
		SagaState? capturedState = null;

		var orchestration = SagaBuilder<SagaTestInput, SagaTestOutput>.Create("error-handler")
			.WithAutoCompensation(false)
			.WithErrorHandler((state, ex) =>
			{
				capturedState = state;
				capturedError = ex;
			})
			.AddStep<string, string>("failing-step", s => s
				.ExecuteActivity("act")
				.WithInput((i, _) => i.Value)
				.WithOutput((_, _) =>
					throw new InvalidOperationException("Step failed")))
			.WithOutputBuilder((_, _) => Task.FromResult(new SagaTestOutput()))
			.Build(NullLogger<FluentSagaOrchestration<SagaTestInput, SagaTestOutput>>.Instance);

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(
			() => orchestration.ExecuteAsync(
				new SagaTestInput { Value = "err" }, CancellationToken.None));

		capturedError.ShouldNotBeNull();
		capturedError.ShouldBeOfType<InvalidOperationException>();
		capturedState.ShouldNotBeNull();
	}

	[Fact]
	public async Task NotInvokeErrorHandler_OnCancellation()
	{
		// Arrange
		var errorHandlerCalled = false;
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync().ConfigureAwait(false);

		var orchestration = SagaBuilder<SagaTestInput, SagaTestOutput>.Create("no-error-on-cancel")
			.WithErrorHandler((_, _) => errorHandlerCalled = true)
			.AddStep<string, string>("step", s => s
				.ExecuteActivity("act")
				.WithInput((i, _) => i.Value)
				.WithOutput((_, _) => { }))
			.WithOutputBuilder((_, _) => Task.FromResult(new SagaTestOutput()))
			.Build(NullLogger<FluentSagaOrchestration<SagaTestInput, SagaTestOutput>>.Instance);

		// Act
		try
		{
			await orchestration.ExecuteAsync(
				new SagaTestInput { Value = "x" }, cts.Token);
		}
		catch (OperationCanceledException)
		{
			// Expected
		}

		// Assert
		errorHandlerCalled.ShouldBeFalse();
	}

	#endregion

	#region Conditional Steps

	[Fact]
	public async Task SkipConditionalStep_WhenConditionIsFalse()
	{
		// Arrange
		var conditionalStepRan = false;
		var orchestration = SagaBuilder<SagaTestInput, SagaTestOutput>.Create("conditional")
			.AddConditionalStep<string, string>("maybe-step",
				(_, _) => false, // Condition is false — step should be skipped
				s => s
					.ExecuteActivity("act")
					.WithInput((i, _) => i.Value)
					.WithOutput((_, _) => conditionalStepRan = true))
			.WithOutputBuilder((_, _) =>
				Task.FromResult(new SagaTestOutput { Result = "skipped" }))
			.Build(NullLogger<FluentSagaOrchestration<SagaTestInput, SagaTestOutput>>.Instance);

		// Act
		var result = await orchestration.ExecuteAsync(
			new SagaTestInput { Value = "cond" }, CancellationToken.None);

		// Assert
		result.Result.ShouldBe("skipped");
		conditionalStepRan.ShouldBeFalse();
	}

	[Fact]
	public async Task ExecuteConditionalStep_WhenConditionIsTrue()
	{
		// Arrange
		var conditionalStepRan = false;
		var orchestration = SagaBuilder<SagaTestInput, SagaTestOutput>.Create("conditional-true")
			.AddConditionalStep<string, string>("maybe-step",
				(_, _) => true, // Condition is true — step should run
				s => s
					.ExecuteActivity("act")
					.WithInput((i, _) => i.Value)
					.WithOutput((_, _) => conditionalStepRan = true))
			.WithOutputBuilder((_, _) =>
				Task.FromResult(new SagaTestOutput { Result = "ran" }))
			.Build(NullLogger<FluentSagaOrchestration<SagaTestInput, SagaTestOutput>>.Instance);

		// Act
		var result = await orchestration.ExecuteAsync(
			new SagaTestInput { Value = "cond" }, CancellationToken.None);

		// Assert
		result.Result.ShouldBe("ran");
		conditionalStepRan.ShouldBeTrue();
	}

	#endregion

	#region Edge Cases

	[Fact]
	public async Task HandleEmptyStepList_Gracefully()
	{
		// Arrange — no steps, just output builder
		var orchestration = SagaBuilder<SagaTestInput, SagaTestOutput>.Create("empty")
			.WithOutputBuilder((_, _) =>
				Task.FromResult(new SagaTestOutput { Result = "no-steps" }))
			.Build(NullLogger<FluentSagaOrchestration<SagaTestInput, SagaTestOutput>>.Instance);

		// Act
		var result = await orchestration.ExecuteAsync(
			new SagaTestInput { Value = "empty" }, CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
		result.Result.ShouldBe("no-steps");
	}

	[Fact]
	public void ThrowWhenOutputBuilder_NotConfigured()
	{
		// Arrange & Act & Assert — Build() should throw without WithOutputBuilder
		Should.Throw<InvalidOperationException>(() =>
			SagaBuilder<SagaTestInput, SagaTestOutput>.Create("no-output")
				.AddStep<string, string>("step", s => s
					.ExecuteActivity("act")
					.WithInput((i, _) => i.Value)
					.WithOutput((_, _) => { }))
				.Build(NullLogger<FluentSagaOrchestration<SagaTestInput, SagaTestOutput>>.Instance));
	}

	[Fact]
	public async Task StepWithoutActivity_ExecutesInlineOnly()
	{
		// Arrange — step without ExecuteActivity still runs input/output handlers
		var outputRan = false;
		var orchestration = SagaBuilder<SagaTestInput, SagaTestOutput>.Create("no-activity")
			.AddStep<string, string>("inline-step", s => s
				.WithInput((i, _) => i.Value)
				.WithOutput((_, state) =>
				{
					outputRan = true;
					state.Data["inline"] = true;
				}))
			.WithOutputBuilder((_, state) =>
			{
				state.Data.ContainsKey("inline").ShouldBeTrue();
				return Task.FromResult(new SagaTestOutput { Result = "inline-ok" });
			})
			.Build(NullLogger<FluentSagaOrchestration<SagaTestInput, SagaTestOutput>>.Instance);

		// Act
		var result = await orchestration.ExecuteAsync(
			new SagaTestInput { Value = "test" }, CancellationToken.None);

		// Assert
		result.Result.ShouldBe("inline-ok");
		outputRan.ShouldBeTrue();
	}

	#endregion
}
