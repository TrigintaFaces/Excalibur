// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Implementation;
using Excalibur.Saga.Models;

using Microsoft.Extensions.Logging.Abstractions;

using StepResult = Excalibur.Saga.Abstractions.StepResult;

namespace Excalibur.Saga.Tests.Implementation;

/// <summary>
/// Functional tests for <see cref="ParallelSagaStep{TData}"/> covering
/// multi-strategy execution, result aggregation, failure handling, and compensation.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ParallelSagaStepFunctionalShould
{
	[Fact]
	public async Task ExecuteMultipleSteps_AndAggregateOutputData()
	{
		// Arrange
		var context = A.Fake<ISagaContext<TestSagaData>>();
		var step1 = CreateFakeStep("ValidateInventory");
		var step2 = CreateFakeStep("ValidatePayment");
		var step3 = CreateFakeStep("ValidateShipping");

		A.CallTo(() => step1.ExecuteAsync(context, A<CancellationToken>._))
			.Returns(StepResult.Success(new Dictionary<string, object> { ["InventoryOk"] = true }));
		A.CallTo(() => step2.ExecuteAsync(context, A<CancellationToken>._))
			.Returns(StepResult.Success(new Dictionary<string, object> { ["PaymentOk"] = true }));
		A.CallTo(() => step3.ExecuteAsync(context, A<CancellationToken>._))
			.Returns(StepResult.Success(new Dictionary<string, object> { ["ShippingOk"] = true }));

		var sut = new ParallelSagaStep<TestSagaData>(
			"ValidationGate",
			[step1, step2, step3],
			NullLogger<ParallelSagaStep<TestSagaData>>.Instance)
		{
			Strategy = ParallelismStrategy.Unlimited,
		};

		// Act
		var result = await sut.ExecuteAsync(context, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		result.OutputData.ShouldNotBeNull();
		// Output data is prefixed with step index
		result.OutputData.Keys.ShouldContain("step_0_InventoryOk");
		result.OutputData.Keys.ShouldContain("step_1_PaymentOk");
		result.OutputData.Keys.ShouldContain("step_2_ShippingOk");
	}

	[Fact]
	public async Task FailEntireStep_WhenOneStepFails_RequireAllSuccess()
	{
		// Arrange
		var context = A.Fake<ISagaContext<TestSagaData>>();
		var step1 = CreateFakeStep("Step1");
		var step2 = CreateFakeStep("FailingStep");

		A.CallTo(() => step1.ExecuteAsync(context, A<CancellationToken>._))
			.Returns(StepResult.Success());
		A.CallTo(() => step2.ExecuteAsync(context, A<CancellationToken>._))
			.Returns(StepResult.Failure("Payment declined"));

		var sut = new ParallelSagaStep<TestSagaData>(
			"ParallelValidation",
			[step1, step2],
			NullLogger<ParallelSagaStep<TestSagaData>>.Instance)
		{
			Strategy = ParallelismStrategy.Unlimited,
			RequireAllSuccess = true,
		};

		// Act
		var result = await sut.ExecuteAsync(context, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeFalse();
		result.ErrorMessage.ShouldContain("Parallel execution failed");
	}

	[Fact]
	public async Task ContinueOnFailure_WhenConfigured()
	{
		// Arrange
		var context = A.Fake<ISagaContext<TestSagaData>>();
		var step1 = CreateFakeStep("Step1");
		var step2 = CreateFakeStep("FailingStep");
		var step3 = CreateFakeStep("Step3");

		A.CallTo(() => step1.ExecuteAsync(context, A<CancellationToken>._))
			.Returns(StepResult.Failure("Failed"));
		A.CallTo(() => step2.ExecuteAsync(context, A<CancellationToken>._))
			.Returns(StepResult.Success());
		A.CallTo(() => step3.ExecuteAsync(context, A<CancellationToken>._))
			.Returns(StepResult.Success());

		var sut = new ParallelSagaStep<TestSagaData>(
			"ParallelWithContinue",
			[step1, step2, step3],
			NullLogger<ParallelSagaStep<TestSagaData>>.Instance)
		{
			Strategy = ParallelismStrategy.Batched,
			MaxDegreeOfParallelism = 1,
			ContinueOnFailure = true,
			RequireAllSuccess = false,
		};

		// Act
		var result = await sut.ExecuteAsync(context, CancellationToken.None);

		// Assert - should succeed since RequireAllSuccess is false and ContinueOnFailure is true
		result.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public async Task LimitParallelism_WithSemaphoreStrategy()
	{
		// Arrange
		var context = A.Fake<ISagaContext<TestSagaData>>();
		var executionOrder = new List<int>();
		var steps = new List<ISagaStep<TestSagaData>>();

		for (var i = 0; i < 5; i++)
		{
			var step = CreateFakeStep($"Step{i}");
			var index = i;
			A.CallTo(() => step.ExecuteAsync(context, A<CancellationToken>._))
				.ReturnsLazily(async _ =>
				{
					lock (executionOrder) { executionOrder.Add(index); }
					await global::Tests.Shared.Infrastructure.TestTiming.PauseAsync(10).ConfigureAwait(false);
					return StepResult.Success();
				});
			steps.Add(step);
		}

		var sut = new ParallelSagaStep<TestSagaData>(
			"LimitedParallel",
			steps,
			NullLogger<ParallelSagaStep<TestSagaData>>.Instance)
		{
			Strategy = ParallelismStrategy.Limited,
			MaxDegreeOfParallelism = 2,
		};

		// Act
		var result = await sut.ExecuteAsync(context, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		executionOrder.Count.ShouldBe(5); // All 5 steps executed
	}

	[Fact]
	public async Task CompensateAllSteps_InParallel()
	{
		// Arrange
		var context = A.Fake<ISagaContext<TestSagaData>>();
		var step1 = CreateFakeStep("Step1", canCompensate: true);
		var step2 = CreateFakeStep("Step2", canCompensate: true);
		var step3 = CreateFakeStep("Step3", canCompensate: true);

		A.CallTo(() => step1.CompensateAsync(context, A<CancellationToken>._))
			.Returns(StepResult.Success());
		A.CallTo(() => step2.CompensateAsync(context, A<CancellationToken>._))
			.Returns(StepResult.Success());
		A.CallTo(() => step3.CompensateAsync(context, A<CancellationToken>._))
			.Returns(StepResult.Success());

		var sut = new ParallelSagaStep<TestSagaData>(
			"ParallelCompensation",
			[step1, step2, step3],
			NullLogger<ParallelSagaStep<TestSagaData>>.Instance);

		// Act
		var result = await sut.CompensateAsync(context, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		A.CallTo(() => step1.CompensateAsync(context, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		A.CallTo(() => step2.CompensateAsync(context, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		A.CallTo(() => step3.CompensateAsync(context, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task HandleStepException_GracefullyReturnFailure()
	{
		// Arrange
		var context = A.Fake<ISagaContext<TestSagaData>>();
		var step = CreateFakeStep("ThrowingStep");

		A.CallTo(() => step.ExecuteAsync(context, A<CancellationToken>._))
			.Throws(new InvalidOperationException("Database connection lost"));

		var sut = new ParallelSagaStep<TestSagaData>(
			"ExceptionHandling",
			[step],
			NullLogger<ParallelSagaStep<TestSagaData>>.Instance)
		{
			Strategy = ParallelismStrategy.Unlimited,
		};

		// Act
		var result = await sut.ExecuteAsync(context, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeFalse();
		result.ErrorMessage.ShouldContain("failed");
	}

	[Fact]
	public void AggregateEmptyResults_ReturnsSuccess()
	{
		// Arrange
		var sut = new ParallelSagaStep<TestSagaData>(
			"Empty",
			[CreateFakeStep("S")],
			NullLogger<ParallelSagaStep<TestSagaData>>.Instance);

		// Act
		var result = sut.AggregateResults([]);

		// Assert
		result.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public void AggregateResults_MergesOutputDataWithIndexPrefix()
	{
		// Arrange
		var sut = new ParallelSagaStep<TestSagaData>(
			"Aggregation",
			[CreateFakeStep("S")],
			NullLogger<ParallelSagaStep<TestSagaData>>.Instance);

		var results = new List<StepResult>
		{
			StepResult.Success(new Dictionary<string, object> { ["A"] = 1 }),
			StepResult.Success(new Dictionary<string, object> { ["B"] = 2 }),
		};

		// Act
		var aggregated = sut.AggregateResults(results);

		// Assert
		aggregated.IsSuccess.ShouldBeTrue();
		aggregated.OutputData.ShouldNotBeNull();
		aggregated.OutputData["step_0_A"].ShouldBe(1);
		aggregated.OutputData["step_1_B"].ShouldBe(2);
	}

	[Fact]
	public async Task ExecuteWithBatchedStrategy_ProcessesInBatches()
	{
		// Arrange - 4 steps with batch size 2
		var context = A.Fake<ISagaContext<TestSagaData>>();
		var steps = new List<ISagaStep<TestSagaData>>();

		for (var i = 0; i < 4; i++)
		{
			var step = CreateFakeStep($"Step{i}");
			A.CallTo(() => step.ExecuteAsync(context, A<CancellationToken>._))
				.Returns(StepResult.Success());
			steps.Add(step);
		}

		var sut = new ParallelSagaStep<TestSagaData>(
			"BatchExecution",
			steps,
			NullLogger<ParallelSagaStep<TestSagaData>>.Instance)
		{
			Strategy = ParallelismStrategy.Batched,
			MaxDegreeOfParallelism = 2,
		};

		// Act
		var result = await sut.ExecuteAsync(context, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		foreach (var step in steps)
		{
			A.CallTo(() => step.ExecuteAsync(context, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		}
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

