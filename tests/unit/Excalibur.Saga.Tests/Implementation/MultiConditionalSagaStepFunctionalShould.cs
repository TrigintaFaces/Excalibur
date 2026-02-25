// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Implementation;

using Microsoft.Extensions.Logging.Abstractions;

using StepResult = Excalibur.Saga.Abstractions.StepResult;

namespace Excalibur.Saga.Tests.Implementation;

/// <summary>
/// Functional tests for <see cref="MultiConditionalSagaStep{TData}"/> covering
/// multi-way branching, default step fallback, compensation, and dynamic routing.
/// </summary>
[Trait("Category", "Unit")]
public sealed class MultiConditionalSagaStepFunctionalShould
{
	[Fact]
	public async Task RouteToCorrectBranch_BasedOnEvaluator()
	{
		// Arrange
		var context = A.Fake<ISagaContext<TestSagaData>>();
		A.CallTo(() => context.Data).Returns(new TestSagaData { Value = "premium" });

		var premiumStep = CreateFakeStep("PremiumProcessing");
		var standardStep = CreateFakeStep("StandardProcessing");

		A.CallTo(() => premiumStep.ExecuteAsync(context, A<CancellationToken>._))
			.Returns(StepResult.Success(new Dictionary<string, object> { ["Tier"] = "Premium" }));

		var branches = new Dictionary<string, ISagaStep<TestSagaData>>
		{
			["premium"] = premiumStep,
			["standard"] = standardStep,
		};

		var sut = new MultiConditionalSagaStep<TestSagaData>(
			"TierRouting",
			(ctx, _) => Task.FromResult(ctx.Data.Value ?? "standard"),
			branches,
			logger: NullLogger<MultiConditionalSagaStep<TestSagaData>>.Instance);

		// Act
		var result = await sut.ExecuteAsync(context, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		result.OutputData.ShouldNotBeNull();
		result.OutputData["ExecutedBranch"].ShouldBe("premium");
		A.CallTo(() => premiumStep.ExecuteAsync(context, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		A.CallTo(() => standardStep.ExecuteAsync(context, A<CancellationToken>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task FallbackToDefaultStep_WhenNoBranchMatches()
	{
		// Arrange
		var context = A.Fake<ISagaContext<TestSagaData>>();
		A.CallTo(() => context.Data).Returns(new TestSagaData { Value = "unknown-tier" });

		var defaultStep = CreateFakeStep("DefaultProcessing");

		A.CallTo(() => defaultStep.ExecuteAsync(context, A<CancellationToken>._))
			.Returns(StepResult.Success(new Dictionary<string, object> { ["Tier"] = "Default" }));

		var branches = new Dictionary<string, ISagaStep<TestSagaData>>
		{
			["premium"] = CreateFakeStep("PremiumProcessing"),
		};

		var sut = new MultiConditionalSagaStep<TestSagaData>(
			"TierRouting",
			(ctx, _) => Task.FromResult(ctx.Data.Value ?? "default"),
			branches,
			defaultStep,
			NullLogger<MultiConditionalSagaStep<TestSagaData>>.Instance);

		// Act
		var result = await sut.ExecuteAsync(context, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		result.OutputData.ShouldNotBeNull();
		result.OutputData["ExecutedBranch"].ShouldBe("default");
		A.CallTo(() => defaultStep.ExecuteAsync(context, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ReturnSkippedResult_WhenNoBranchAndNoDefault()
	{
		// Arrange
		var context = A.Fake<ISagaContext<TestSagaData>>();

		var branches = new Dictionary<string, ISagaStep<TestSagaData>>
		{
			["a"] = CreateFakeStep("StepA"),
		};

		var sut = new MultiConditionalSagaStep<TestSagaData>(
			"NoMatch",
			(_, _) => Task.FromResult("nonexistent"),
			branches,
			defaultStep: null,
			NullLogger<MultiConditionalSagaStep<TestSagaData>>.Instance);

		// Act
		var result = await sut.ExecuteAsync(context, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		result.OutputData.ShouldNotBeNull();
		result.OutputData["Skipped"].ShouldBe(true);
		result.OutputData["BranchFound"].ShouldBe(false);
	}

	[Fact]
	public async Task CompensateTheExecutedBranch()
	{
		// Arrange
		var context = A.Fake<ISagaContext<TestSagaData>>();

		var stepA = CreateFakeStep("StepA", canCompensate: true);
		var stepB = CreateFakeStep("StepB", canCompensate: true);

		A.CallTo(() => stepA.ExecuteAsync(context, A<CancellationToken>._))
			.Returns(StepResult.Success(new Dictionary<string, object>()));
		A.CallTo(() => stepA.CompensateAsync(context, A<CancellationToken>._))
			.Returns(StepResult.Success());

		var branches = new Dictionary<string, ISagaStep<TestSagaData>>
		{
			["a"] = stepA,
			["b"] = stepB,
		};

		var sut = new MultiConditionalSagaStep<TestSagaData>(
			"BranchCompensation",
			(_, _) => Task.FromResult("a"),
			branches,
			logger: NullLogger<MultiConditionalSagaStep<TestSagaData>>.Instance);

		// Execute first (takes branch "a")
		await sut.ExecuteAsync(context, CancellationToken.None);

		// Act
		var result = await sut.CompensateAsync(context, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		A.CallTo(() => stepA.CompensateAsync(context, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		A.CallTo(() => stepB.CompensateAsync(context, A<CancellationToken>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task CompensateDefaultStep_WhenDefaultWasExecuted()
	{
		// Arrange
		var context = A.Fake<ISagaContext<TestSagaData>>();

		var defaultStep = CreateFakeStep("DefaultStep", canCompensate: true);
		A.CallTo(() => defaultStep.ExecuteAsync(context, A<CancellationToken>._))
			.Returns(StepResult.Success(new Dictionary<string, object>()));
		A.CallTo(() => defaultStep.CompensateAsync(context, A<CancellationToken>._))
			.Returns(StepResult.Success());

		var branches = new Dictionary<string, ISagaStep<TestSagaData>>
		{
			["a"] = CreateFakeStep("StepA"),
		};

		var sut = new MultiConditionalSagaStep<TestSagaData>(
			"DefaultCompensation",
			(_, _) => Task.FromResult("nonexistent"),
			branches,
			defaultStep,
			NullLogger<MultiConditionalSagaStep<TestSagaData>>.Instance);

		// Execute (falls through to default)
		await sut.ExecuteAsync(context, CancellationToken.None);

		// Act
		var result = await sut.CompensateAsync(context, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		A.CallTo(() => defaultStep.CompensateAsync(context, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task SkipCompensation_WhenNoStepWasExecuted()
	{
		// Arrange
		var context = A.Fake<ISagaContext<TestSagaData>>();

		var branches = new Dictionary<string, ISagaStep<TestSagaData>>
		{
			["a"] = CreateFakeStep("StepA"),
		};

		var sut = new MultiConditionalSagaStep<TestSagaData>(
			"NeverExecuted",
			(_, _) => Task.FromResult("a"),
			branches,
			logger: NullLogger<MultiConditionalSagaStep<TestSagaData>>.Instance);

		// Act - compensate without prior execution
		var result = await sut.CompensateAsync(context, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public async Task HandleBranchEvaluatorException_GracefullyReturnFailure()
	{
		// Arrange
		var context = A.Fake<ISagaContext<TestSagaData>>();

		var branches = new Dictionary<string, ISagaStep<TestSagaData>>
		{
			["a"] = CreateFakeStep("StepA"),
		};

		var sut = new MultiConditionalSagaStep<TestSagaData>(
			"FailingEvaluator",
			(_, _) => throw new InvalidOperationException("Evaluator broke"),
			branches,
			logger: NullLogger<MultiConditionalSagaStep<TestSagaData>>.Instance);

		// Act
		var result = await sut.ExecuteAsync(context, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeFalse();
		result.ErrorMessage.ShouldContain("failed");
	}

	[Fact]
	public void ReportCanCompensate_True_WhenAllBranchesCanCompensate()
	{
		// Arrange
		var branches = new Dictionary<string, ISagaStep<TestSagaData>>
		{
			["a"] = CreateFakeStep("StepA", canCompensate: true),
			["b"] = CreateFakeStep("StepB", canCompensate: true),
		};

		var sut = new MultiConditionalSagaStep<TestSagaData>(
			"AllCompensable",
			(_, _) => Task.FromResult("a"),
			branches,
			logger: NullLogger<MultiConditionalSagaStep<TestSagaData>>.Instance);

		// Assert
		sut.CanCompensate.ShouldBeTrue();
	}

	[Fact]
	public void ReportCanCompensate_False_WhenAnyBranchCannotCompensate()
	{
		// Arrange
		var branches = new Dictionary<string, ISagaStep<TestSagaData>>
		{
			["a"] = CreateFakeStep("StepA", canCompensate: true),
			["b"] = CreateFakeStep("StepB", canCompensate: false),
		};

		var sut = new MultiConditionalSagaStep<TestSagaData>(
			"PartialCompensable",
			(_, _) => Task.FromResult("a"),
			branches,
			logger: NullLogger<MultiConditionalSagaStep<TestSagaData>>.Instance);

		// Assert
		sut.CanCompensate.ShouldBeFalse();
	}

	[Fact]
	public void ReportCanCompensate_False_WhenDefaultStepCannotCompensate()
	{
		// Arrange
		var branches = new Dictionary<string, ISagaStep<TestSagaData>>
		{
			["a"] = CreateFakeStep("StepA", canCompensate: true),
		};
		var defaultStep = CreateFakeStep("Default", canCompensate: false);

		var sut = new MultiConditionalSagaStep<TestSagaData>(
			"DefaultNonCompensable",
			(_, _) => Task.FromResult("a"),
			branches,
			defaultStep,
			NullLogger<MultiConditionalSagaStep<TestSagaData>>.Instance);

		// Assert
		sut.CanCompensate.ShouldBeFalse();
	}

	[Fact]
	public void ThrowOnNullConstructorArguments()
	{
		var branches = new Dictionary<string, ISagaStep<TestSagaData>>();
		Func<ISagaContext<TestSagaData>, CancellationToken, Task<string>> evaluator = (_, _) => Task.FromResult("a");

		Should.Throw<ArgumentNullException>(() =>
			new MultiConditionalSagaStep<TestSagaData>(null!, evaluator, branches));
		Should.Throw<ArgumentNullException>(() =>
			new MultiConditionalSagaStep<TestSagaData>("step", null!, branches));
		Should.Throw<ArgumentNullException>(() =>
			new MultiConditionalSagaStep<TestSagaData>("step", evaluator, null!));
	}

	[Fact]
	public async Task IncludeStepNameInOutputData()
	{
		// Arrange
		var context = A.Fake<ISagaContext<TestSagaData>>();
		var stepA = CreateFakeStep("ProcessA");

		A.CallTo(() => stepA.ExecuteAsync(context, A<CancellationToken>._))
			.Returns(StepResult.Success(new Dictionary<string, object> { ["Result"] = "Done" }));

		var branches = new Dictionary<string, ISagaStep<TestSagaData>>
		{
			["route-a"] = stepA,
		};

		var sut = new MultiConditionalSagaStep<TestSagaData>(
			"Router",
			(_, _) => Task.FromResult("route-a"),
			branches,
			logger: NullLogger<MultiConditionalSagaStep<TestSagaData>>.Instance);

		// Act
		var result = await sut.ExecuteAsync(context, CancellationToken.None);

		// Assert
		result.OutputData.ShouldNotBeNull();
		result.OutputData["MultiConditionalStepName"].ShouldBe("Router");
		result.OutputData["ExecutedBranch"].ShouldBe("route-a");
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
