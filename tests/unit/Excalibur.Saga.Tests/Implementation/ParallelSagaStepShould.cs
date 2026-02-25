// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Implementation;
using Excalibur.Saga.Models;

using Microsoft.Extensions.Logging.Abstractions;

using StepResult = Excalibur.Saga.Abstractions.StepResult;

namespace Excalibur.Saga.Tests.Implementation;

/// <summary>
/// Unit tests for <see cref="ParallelSagaStep{TData}"/>.
/// Verifies parallel execution strategies, compensation, and result aggregation.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class ParallelSagaStepShould
{

	#region Constructor Tests

	[Fact]
	public void ThrowArgumentNullException_WhenNameIsNull()
	{
		// Arrange
		var steps = new List<ISagaStep<TestSagaData>>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new ParallelSagaStep<TestSagaData>(null!, steps, NullLogger<ParallelSagaStep<TestSagaData>>.Instance));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenParallelStepsIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new ParallelSagaStep<TestSagaData>("TestStep", null, NullLogger<ParallelSagaStep<TestSagaData>>.Instance));
	}

	[Fact]
	public void CreateInstance_WithValidParameters()
	{
		// Arrange
		var steps = new List<ISagaStep<TestSagaData>> { CreateFakeStep("Step1") };

		// Act
		var sut = new ParallelSagaStep<TestSagaData>("TestStep", steps, NullLogger<ParallelSagaStep<TestSagaData>>.Instance);

		// Assert
		sut.ShouldNotBeNull();
		sut.Name.ShouldBe("TestStep");
	}

	[Fact]
	public void CreateInstance_WithNullLogger()
	{
		// Arrange
		var steps = new List<ISagaStep<TestSagaData>> { CreateFakeStep("Step1") };

		// Act
		var sut = new ParallelSagaStep<TestSagaData>("TestStep", steps, null);

		// Assert
		sut.ShouldNotBeNull();
	}

	#endregion

	#region Property Tests

	[Fact]
	public void HaveDefaultTimeout_OfFiveMinutes()
	{
		// Arrange
		var steps = new List<ISagaStep<TestSagaData>> { CreateFakeStep("Step1") };
		var sut = new ParallelSagaStep<TestSagaData>("TestStep", steps, NullLogger<ParallelSagaStep<TestSagaData>>.Instance);

		// Assert
		sut.Timeout.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void AllowTimeoutToBeSet()
	{
		// Arrange
		var steps = new List<ISagaStep<TestSagaData>> { CreateFakeStep("Step1") };
		var sut = new ParallelSagaStep<TestSagaData>("TestStep", steps, NullLogger<ParallelSagaStep<TestSagaData>>.Instance)
		{
			Timeout = TimeSpan.FromSeconds(30)
		};

		// Assert
		sut.Timeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void HaveDefaultStrategy_OfLimited()
	{
		// Arrange
		var steps = new List<ISagaStep<TestSagaData>> { CreateFakeStep("Step1") };
		var sut = new ParallelSagaStep<TestSagaData>("TestStep", steps, NullLogger<ParallelSagaStep<TestSagaData>>.Instance);

		// Assert
		sut.Strategy.ShouldBe(ParallelismStrategy.Limited);
	}

	[Fact]
	public void HaveDefaultMaxDegreeOfParallelism_EqualToProcessorCount()
	{
		// Arrange
		var steps = new List<ISagaStep<TestSagaData>> { CreateFakeStep("Step1") };
		var sut = new ParallelSagaStep<TestSagaData>("TestStep", steps, NullLogger<ParallelSagaStep<TestSagaData>>.Instance);

		// Assert
		sut.MaxDegreeOfParallelism.ShouldBe(Environment.ProcessorCount);
	}

	[Fact]
	public void HaveDefaultRequireAllSuccess_True()
	{
		// Arrange
		var steps = new List<ISagaStep<TestSagaData>> { CreateFakeStep("Step1") };
		var sut = new ParallelSagaStep<TestSagaData>("TestStep", steps, NullLogger<ParallelSagaStep<TestSagaData>>.Instance);

		// Assert
		sut.RequireAllSuccess.ShouldBeTrue();
	}

	[Fact]
	public void HaveDefaultContinueOnFailure_False()
	{
		// Arrange
		var steps = new List<ISagaStep<TestSagaData>> { CreateFakeStep("Step1") };
		var sut = new ParallelSagaStep<TestSagaData>("TestStep", steps, NullLogger<ParallelSagaStep<TestSagaData>>.Instance);

		// Assert
		sut.ContinueOnFailure.ShouldBeFalse();
	}

	[Fact]
	public void ReturnReadOnlyParallelSteps()
	{
		// Arrange
		var step1 = CreateFakeStep("Step1");
		var step2 = CreateFakeStep("Step2");
		var steps = new List<ISagaStep<TestSagaData>> { step1, step2 };
		var sut = new ParallelSagaStep<TestSagaData>("TestStep", steps, NullLogger<ParallelSagaStep<TestSagaData>>.Instance);

		// Assert
		sut.ParallelSteps.Count.ShouldBe(2);
		sut.ParallelSteps[0].ShouldBeSameAs(step1);
		sut.ParallelSteps[1].ShouldBeSameAs(step2);
	}

	[Fact]
	public void ReturnCanCompensate_True_WhenAllStepsCanCompensate()
	{
		// Arrange
		var step1 = CreateFakeStep("Step1", canCompensate: true);
		var step2 = CreateFakeStep("Step2", canCompensate: true);
		var steps = new List<ISagaStep<TestSagaData>> { step1, step2 };
		var sut = new ParallelSagaStep<TestSagaData>("TestStep", steps, NullLogger<ParallelSagaStep<TestSagaData>>.Instance);

		// Assert
		sut.CanCompensate.ShouldBeTrue();
	}

	[Fact]
	public void ReturnCanCompensate_False_WhenAnyStepCannotCompensate()
	{
		// Arrange
		var step1 = CreateFakeStep("Step1", canCompensate: true);
		var step2 = CreateFakeStep("Step2", canCompensate: false);
		var steps = new List<ISagaStep<TestSagaData>> { step1, step2 };
		var sut = new ParallelSagaStep<TestSagaData>("TestStep", steps, NullLogger<ParallelSagaStep<TestSagaData>>.Instance);

		// Assert
		sut.CanCompensate.ShouldBeFalse();
	}

	#endregion

	#region ExecuteAsync Tests

	[Fact]
	public async Task ExecuteAllSteps_WithUnlimitedStrategy()
	{
		// Arrange
		var step1 = CreateFakeStep("Step1");
		var step2 = CreateFakeStep("Step2");
		var context = A.Fake<ISagaContext<TestSagaData>>();

		A.CallTo(() => step1.ExecuteAsync(context, A<CancellationToken>._))
			.Returns(StepResult.Success());
		A.CallTo(() => step2.ExecuteAsync(context, A<CancellationToken>._))
			.Returns(StepResult.Success());

		var steps = new List<ISagaStep<TestSagaData>> { step1, step2 };
		var sut = new ParallelSagaStep<TestSagaData>("TestStep", steps, NullLogger<ParallelSagaStep<TestSagaData>>.Instance)
		{
			Strategy = ParallelismStrategy.Unlimited
		};

		// Act
		var result = await sut.ExecuteAsync(context, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		A.CallTo(() => step1.ExecuteAsync(context, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => step2.ExecuteAsync(context, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ExecuteAllSteps_WithLimitedStrategy()
	{
		// Arrange
		var step1 = CreateFakeStep("Step1");
		var step2 = CreateFakeStep("Step2");
		var context = A.Fake<ISagaContext<TestSagaData>>();

		A.CallTo(() => step1.ExecuteAsync(context, A<CancellationToken>._))
			.Returns(StepResult.Success());
		A.CallTo(() => step2.ExecuteAsync(context, A<CancellationToken>._))
			.Returns(StepResult.Success());

		var steps = new List<ISagaStep<TestSagaData>> { step1, step2 };
		var sut = new ParallelSagaStep<TestSagaData>("TestStep", steps, NullLogger<ParallelSagaStep<TestSagaData>>.Instance)
		{
			Strategy = ParallelismStrategy.Limited,
			MaxDegreeOfParallelism = 2
		};

		// Act
		var result = await sut.ExecuteAsync(context, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		A.CallTo(() => step1.ExecuteAsync(context, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => step2.ExecuteAsync(context, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ExecuteAllSteps_WithBatchedStrategy()
	{
		// Arrange
		var step1 = CreateFakeStep("Step1");
		var step2 = CreateFakeStep("Step2");
		var context = A.Fake<ISagaContext<TestSagaData>>();

		A.CallTo(() => step1.ExecuteAsync(context, A<CancellationToken>._))
			.Returns(StepResult.Success());
		A.CallTo(() => step2.ExecuteAsync(context, A<CancellationToken>._))
			.Returns(StepResult.Success());

		var steps = new List<ISagaStep<TestSagaData>> { step1, step2 };
		var sut = new ParallelSagaStep<TestSagaData>("TestStep", steps, NullLogger<ParallelSagaStep<TestSagaData>>.Instance)
		{
			Strategy = ParallelismStrategy.Batched,
			MaxDegreeOfParallelism = 1
		};

		// Act
		var result = await sut.ExecuteAsync(context, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnFailure_WhenAnyStepFails_AndRequireAllSuccessIsTrue()
	{
		// Arrange
		var step1 = CreateFakeStep("Step1");
		var step2 = CreateFakeStep("Step2");
		var context = A.Fake<ISagaContext<TestSagaData>>();

		A.CallTo(() => step1.ExecuteAsync(context, A<CancellationToken>._))
			.Returns(StepResult.Success());
		A.CallTo(() => step2.ExecuteAsync(context, A<CancellationToken>._))
			.Returns(StepResult.Failure("Step2 failed"));

		var steps = new List<ISagaStep<TestSagaData>> { step1, step2 };
		var sut = new ParallelSagaStep<TestSagaData>("TestStep", steps, NullLogger<ParallelSagaStep<TestSagaData>>.Instance)
		{
			Strategy = ParallelismStrategy.Unlimited,
			RequireAllSuccess = true
		};

		// Act
		var result = await sut.ExecuteAsync(context, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeFalse();
		result.ErrorMessage.ShouldContain("Parallel execution failed");
	}

	[Fact]
	public async Task HandleStepException_GracefullyReturningFailure()
	{
		// Arrange
		var step1 = CreateFakeStep("Step1");
		var context = A.Fake<ISagaContext<TestSagaData>>();

		A.CallTo(() => step1.ExecuteAsync(context, A<CancellationToken>._))
			.Throws(new InvalidOperationException("Test exception"));

		var steps = new List<ISagaStep<TestSagaData>> { step1 };
		var sut = new ParallelSagaStep<TestSagaData>("TestStep", steps, NullLogger<ParallelSagaStep<TestSagaData>>.Instance)
		{
			Strategy = ParallelismStrategy.Unlimited
		};

		// Act
		var result = await sut.ExecuteAsync(context, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeFalse();
		result.ErrorMessage.ShouldContain("failed");
	}

	[Fact]
	public async Task StopBatchProcessing_WhenStepFails_AndContinueOnFailureIsFalse()
	{
		// Arrange
		var step1 = CreateFakeStep("Step1");
		var step2 = CreateFakeStep("Step2");
		var step3 = CreateFakeStep("Step3");
		var context = A.Fake<ISagaContext<TestSagaData>>();

		A.CallTo(() => step1.ExecuteAsync(context, A<CancellationToken>._))
			.Returns(StepResult.Failure("Step1 failed"));
		A.CallTo(() => step2.ExecuteAsync(context, A<CancellationToken>._))
			.Returns(StepResult.Success());
		A.CallTo(() => step3.ExecuteAsync(context, A<CancellationToken>._))
			.Returns(StepResult.Success());

		var steps = new List<ISagaStep<TestSagaData>> { step1, step2, step3 };
		var sut = new ParallelSagaStep<TestSagaData>("TestStep", steps, NullLogger<ParallelSagaStep<TestSagaData>>.Instance)
		{
			Strategy = ParallelismStrategy.Batched,
			MaxDegreeOfParallelism = 1,
			ContinueOnFailure = false
		};

		// Act
		var result = await sut.ExecuteAsync(context, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeFalse();
		// Step3 should not be executed since we stopped after Step1 failed
		A.CallTo(() => step3.ExecuteAsync(context, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	#endregion

	#region CompensateAsync Tests

	[Fact]
	public async Task CompensateAllCompensableSteps_InReverseOrder()
	{
		// Arrange
		var step1 = CreateFakeStep("Step1", canCompensate: true);
		var step2 = CreateFakeStep("Step2", canCompensate: true);
		var context = A.Fake<ISagaContext<TestSagaData>>();

		A.CallTo(() => step1.CompensateAsync(context, A<CancellationToken>._))
			.Returns(StepResult.Success());
		A.CallTo(() => step2.CompensateAsync(context, A<CancellationToken>._))
			.Returns(StepResult.Success());

		var steps = new List<ISagaStep<TestSagaData>> { step1, step2 };
		var sut = new ParallelSagaStep<TestSagaData>("TestStep", steps, NullLogger<ParallelSagaStep<TestSagaData>>.Instance);

		// Act
		var result = await sut.CompensateAsync(context, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		A.CallTo(() => step1.CompensateAsync(context, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => step2.CompensateAsync(context, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task SkipNonCompensableSteps_DuringCompensation()
	{
		// Arrange
		var step1 = CreateFakeStep("Step1", canCompensate: true);
		var step2 = CreateFakeStep("Step2", canCompensate: false);
		var context = A.Fake<ISagaContext<TestSagaData>>();

		A.CallTo(() => step1.CompensateAsync(context, A<CancellationToken>._))
			.Returns(StepResult.Success());

		var steps = new List<ISagaStep<TestSagaData>> { step1, step2 };
		var sut = new ParallelSagaStep<TestSagaData>("TestStep", steps, NullLogger<ParallelSagaStep<TestSagaData>>.Instance);

		// Act
		var result = await sut.CompensateAsync(context, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		A.CallTo(() => step1.CompensateAsync(context, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => step2.CompensateAsync(context, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task HandleCompensationException_GracefullyReturningFailure()
	{
		// Arrange
		var step1 = CreateFakeStep("Step1", canCompensate: true);
		var context = A.Fake<ISagaContext<TestSagaData>>();

		A.CallTo(() => step1.CompensateAsync(context, A<CancellationToken>._))
			.Throws(new InvalidOperationException("Compensation failed"));

		var steps = new List<ISagaStep<TestSagaData>> { step1 };
		var sut = new ParallelSagaStep<TestSagaData>("TestStep", steps, NullLogger<ParallelSagaStep<TestSagaData>>.Instance);

		// Act
		var result = await sut.CompensateAsync(context, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeFalse();
		result.ErrorMessage.ShouldContain("compensation failed");
	}

	#endregion

	#region AggregateResults Tests

	[Fact]
	public void ThrowArgumentNullException_WhenResultsIsNull()
	{
		// Arrange
		var steps = new List<ISagaStep<TestSagaData>> { CreateFakeStep("Step1") };
		var sut = new ParallelSagaStep<TestSagaData>("TestStep", steps, NullLogger<ParallelSagaStep<TestSagaData>>.Instance);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => sut.AggregateResults(null!));
	}

	[Fact]
	public void ReturnSuccess_WhenResultsIsEmpty()
	{
		// Arrange
		var steps = new List<ISagaStep<TestSagaData>> { CreateFakeStep("Step1") };
		var sut = new ParallelSagaStep<TestSagaData>("TestStep", steps, NullLogger<ParallelSagaStep<TestSagaData>>.Instance);
		var results = new List<StepResult>();

		// Act
		var result = sut.AggregateResults(results);

		// Assert
		result.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public void ReturnSuccess_WhenAllResultsSucceed()
	{
		// Arrange
		var steps = new List<ISagaStep<TestSagaData>> { CreateFakeStep("Step1") };
		var sut = new ParallelSagaStep<TestSagaData>("TestStep", steps, NullLogger<ParallelSagaStep<TestSagaData>>.Instance);
		var results = new List<StepResult>
		{
			StepResult.Success(),
			StepResult.Success()
		};

		// Act
		var result = sut.AggregateResults(results);

		// Assert
		result.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public void ReturnFailure_WhenAnyResultFails_AndRequireAllSuccessIsTrue()
	{
		// Arrange
		var steps = new List<ISagaStep<TestSagaData>> { CreateFakeStep("Step1") };
		var sut = new ParallelSagaStep<TestSagaData>("TestStep", steps, NullLogger<ParallelSagaStep<TestSagaData>>.Instance)
		{
			RequireAllSuccess = true
		};
		var results = new List<StepResult>
		{
			StepResult.Success(),
			StepResult.Failure("Step failed")
		};

		// Act
		var result = sut.AggregateResults(results);

		// Assert
		result.IsSuccess.ShouldBeFalse();
		result.ErrorMessage.ShouldContain("Parallel execution failed");
	}

	[Fact]
	public void AggregateOutputData_FromSuccessfulResults()
	{
		// Arrange
		var steps = new List<ISagaStep<TestSagaData>> { CreateFakeStep("Step1") };
		var sut = new ParallelSagaStep<TestSagaData>("TestStep", steps, NullLogger<ParallelSagaStep<TestSagaData>>.Instance);
		var results = new List<StepResult>
		{
			StepResult.Success(new Dictionary<string, object> { ["Key1"] = "Value1" }),
			StepResult.Success(new Dictionary<string, object> { ["Key2"] = "Value2" })
		};

		// Act
		var result = sut.AggregateResults(results);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		result.OutputData.ShouldNotBeNull();
		result.OutputData["step_0_Key1"].ShouldBe("Value1");
		result.OutputData["step_1_Key2"].ShouldBe("Value2");
	}

	#endregion

	#region NotSupportedStrategy Tests

	[Fact]
	public async Task ThrowNotSupportedException_ForInvalidStrategy()
	{
		// Arrange
		var step1 = CreateFakeStep("Step1");
		var context = A.Fake<ISagaContext<TestSagaData>>();
		var steps = new List<ISagaStep<TestSagaData>> { step1 };
		var sut = new ParallelSagaStep<TestSagaData>("TestStep", steps, NullLogger<ParallelSagaStep<TestSagaData>>.Instance)
		{
			Strategy = (ParallelismStrategy)999
		};

		// Act & Assert
		await Should.ThrowAsync<NotSupportedException>(async () =>
			await sut.ExecuteAsync(context, CancellationToken.None));
	}

	#endregion

	#region Helper Methods

	private static ISagaStep<TestSagaData> CreateFakeStep(string name, bool canCompensate = true)
	{
		var step = A.Fake<ISagaStep<TestSagaData>>();
		A.CallTo(() => step.Name).Returns(name);
		A.CallTo(() => step.CanCompensate).Returns(canCompensate);
		A.CallTo(() => step.Timeout).Returns(TimeSpan.FromMinutes(1));
		return step;
	}

	#endregion
}
