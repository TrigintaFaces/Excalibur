// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Implementation;

using Microsoft.Extensions.Logging.Abstractions;

using StepResult = Excalibur.Saga.Abstractions.StepResult;

namespace Excalibur.Saga.Tests.Implementation;

/// <summary>
/// Functional tests for <see cref="ConditionalSagaStep{TData}"/> covering
/// context-driven branching, chained conditional steps, and compensation workflows.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ConditionalSagaStepFunctionalShould
{
	[Fact]
	public async Task ExecuteThenBranch_BasedOnContextData()
	{
		// Arrange - condition inspects the context data
		var context = A.Fake<ISagaContext<TestSagaData>>();
		A.CallTo(() => context.Data).Returns(new TestSagaData { Counter = 10 });

		var thenStep = CreateFakeStep("HighPath");
		var elseStep = CreateFakeStep("LowPath");

		A.CallTo(() => thenStep.ExecuteAsync(context, A<CancellationToken>._))
			.Returns(StepResult.Success(new Dictionary<string, object> { ["Path"] = "High" }));

		var sut = new ConditionalSagaStep<TestSagaData>(
			"ThresholdCheck",
			(ctx, _) => Task.FromResult(ctx.Data.Counter > 5),
			thenStep, elseStep,
			NullLogger<ConditionalSagaStep<TestSagaData>>.Instance);

		// Act
		var result = await sut.ExecuteAsync(context, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		result.OutputData.ShouldNotBeNull();
		result.OutputData["ConditionalBranch"].ShouldBe("Then");
		A.CallTo(() => thenStep.ExecuteAsync(context, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		A.CallTo(() => elseStep.ExecuteAsync(context, A<CancellationToken>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task ExecuteElseBranch_BasedOnContextData()
	{
		// Arrange - counter below threshold
		var context = A.Fake<ISagaContext<TestSagaData>>();
		A.CallTo(() => context.Data).Returns(new TestSagaData { Counter = 2 });

		var thenStep = CreateFakeStep("HighPath");
		var elseStep = CreateFakeStep("LowPath");

		A.CallTo(() => elseStep.ExecuteAsync(context, A<CancellationToken>._))
			.Returns(StepResult.Success(new Dictionary<string, object> { ["Path"] = "Low" }));

		var sut = new ConditionalSagaStep<TestSagaData>(
			"ThresholdCheck",
			(ctx, _) => Task.FromResult(ctx.Data.Counter > 5),
			thenStep, elseStep,
			NullLogger<ConditionalSagaStep<TestSagaData>>.Instance);

		// Act
		var result = await sut.ExecuteAsync(context, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		result.OutputData.ShouldNotBeNull();
		result.OutputData["ConditionalBranch"].ShouldBe("Else");
		A.CallTo(() => elseStep.ExecuteAsync(context, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		A.CallTo(() => thenStep.ExecuteAsync(context, A<CancellationToken>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task CompensateCorrectBranch_AfterExecution()
	{
		// Arrange - execute then branch, then compensate
		var context = A.Fake<ISagaContext<TestSagaData>>();
		A.CallTo(() => context.Data).Returns(new TestSagaData { Counter = 10 });

		var thenStep = CreateFakeStep("ThenStep", canCompensate: true);
		var elseStep = CreateFakeStep("ElseStep", canCompensate: true);

		A.CallTo(() => thenStep.ExecuteAsync(context, A<CancellationToken>._))
			.Returns(StepResult.Success(new Dictionary<string, object>()));
		A.CallTo(() => thenStep.CompensateAsync(context, A<CancellationToken>._))
			.Returns(StepResult.Success());

		var sut = new ConditionalSagaStep<TestSagaData>(
			"Conditional",
			(ctx, _) => Task.FromResult(ctx.Data.Counter > 5),
			thenStep, elseStep,
			NullLogger<ConditionalSagaStep<TestSagaData>>.Instance);

		// Execute first (takes then branch)
		await sut.ExecuteAsync(context, CancellationToken.None);

		// Act - compensate
		var result = await sut.CompensateAsync(context, CancellationToken.None);

		// Assert - only then step should be compensated
		result.IsSuccess.ShouldBeTrue();
		A.CallTo(() => thenStep.CompensateAsync(context, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		A.CallTo(() => elseStep.CompensateAsync(context, A<CancellationToken>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task TrackBranchInfoInOutputData_ForThenBranch()
	{
		// Arrange
		var context = A.Fake<ISagaContext<TestSagaData>>();
		var thenStep = CreateFakeStep("ProcessPayment");

		A.CallTo(() => thenStep.ExecuteAsync(context, A<CancellationToken>._))
			.Returns(StepResult.Success(new Dictionary<string, object> { ["PaymentId"] = "pay-123" }));

		var sut = new ConditionalSagaStep<TestSagaData>(
			"PaymentDecision",
			(_, _) => Task.FromResult(true),
			thenStep, null,
			NullLogger<ConditionalSagaStep<TestSagaData>>.Instance);

		// Act
		var result = await sut.ExecuteAsync(context, CancellationToken.None);

		// Assert
		result.OutputData.ShouldNotBeNull();
		result.OutputData["ConditionalStepName"].ShouldBe("PaymentDecision");
		result.OutputData["ConditionalBranch"].ShouldBe("Then");
		result.OutputData["PaymentId"].ShouldBe("pay-123");
	}

	[Fact]
	public async Task ReturnSkippedResult_WhenNoBranchConfigured()
	{
		// Arrange - condition is true but no then step
		var context = A.Fake<ISagaContext<TestSagaData>>();

		var sut = new ConditionalSagaStep<TestSagaData>(
			"OptionalStep",
			(_, _) => Task.FromResult(true),
			null, null,
			NullLogger<ConditionalSagaStep<TestSagaData>>.Instance);

		// Act
		var result = await sut.ExecuteAsync(context, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		result.OutputData.ShouldNotBeNull();
		result.OutputData["Skipped"].ShouldBe(true);
	}

	[Fact]
	public async Task HandleAsyncConditionCorrectly()
	{
		// Arrange - condition with async delay
		var context = A.Fake<ISagaContext<TestSagaData>>();
		var thenStep = CreateFakeStep("AsyncBranch");

		A.CallTo(() => thenStep.ExecuteAsync(context, A<CancellationToken>._))
			.Returns(StepResult.Success(new Dictionary<string, object>()));

		var sut = new ConditionalSagaStep<TestSagaData>(
			"AsyncCondition",
			async (_, ct) =>
			{
				await global::Tests.Shared.Infrastructure.TestTiming.PauseAsync(1, ct).ConfigureAwait(false);
				return true;
			},
			thenStep, null,
			NullLogger<ConditionalSagaStep<TestSagaData>>.Instance);

		// Act
		var result = await sut.ExecuteAsync(context, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		A.CallTo(() => thenStep.ExecuteAsync(context, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ReturnFailure_WhenConditionEvaluationThrows()
	{
		// Arrange
		var context = A.Fake<ISagaContext<TestSagaData>>();

		var sut = new ConditionalSagaStep<TestSagaData>(
			"FailingCondition",
			(_, _) => throw new InvalidOperationException("Database unreachable"),
			null, null,
			NullLogger<ConditionalSagaStep<TestSagaData>>.Instance);

		// Act
		var result = await sut.ExecuteAsync(context, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeFalse();
		result.ErrorMessage.ShouldContain("failed");
	}

	[Fact]
	public async Task CompensateBeforeExecution_ShouldSucceed()
	{
		// Arrange - compensation without prior execution
		var context = A.Fake<ISagaContext<TestSagaData>>();

		var sut = new ConditionalSagaStep<TestSagaData>(
			"NeverExecuted",
			(_, _) => Task.FromResult(true),
			CreateFakeStep("ThenStep"), null,
			NullLogger<ConditionalSagaStep<TestSagaData>>.Instance);

		// Act - compensate without executing first
		var result = await sut.CompensateAsync(context, CancellationToken.None);

		// Assert - no step to compensate, so success
		result.IsSuccess.ShouldBeTrue();
	}

	private static ISagaStep<TestSagaData> CreateFakeStep(string name, bool canCompensate = true)
	{
		var step = A.Fake<ISagaStep<TestSagaData>>();
		A.CallTo(() => step.Name).Returns(name);
		A.CallTo(() => step.CanCompensate).Returns(canCompensate);
		A.CallTo(() => step.Timeout).Returns(TimeSpan.FromMinutes(1));
		return step;
	}
}

