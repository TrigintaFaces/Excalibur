// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Implementation;
using Excalibur.Saga.Models;

using Microsoft.Extensions.Logging.Abstractions;

using StepResult = Excalibur.Saga.Abstractions.StepResult;

namespace Excalibur.Saga.Tests.Implementation;

/// <summary>
/// Unit tests for <see cref="ConditionalSagaStep{TData}"/>.
/// Verifies conditional evaluation, branch execution, and compensation behavior.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class ConditionalSagaStepShould
{
	private readonly ISagaContext<TestSagaData> _context;

	public ConditionalSagaStepShould()
	{
		_context = A.Fake<ISagaContext<TestSagaData>>();
	}

	#region Constructor Tests

	[Fact]
	public void ThrowArgumentNullException_WhenNameIsNull()
	{
		// Arrange
		Func<ISagaContext<TestSagaData>, CancellationToken, Task<bool>> condition =
			(_, _) => Task.FromResult(true);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new ConditionalSagaStep<TestSagaData>(null!, condition, null, null, NullLogger<ConditionalSagaStep<TestSagaData>>.Instance));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenConditionIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new ConditionalSagaStep<TestSagaData>("TestStep", null!, null, null, NullLogger<ConditionalSagaStep<TestSagaData>>.Instance));
	}

	[Fact]
	public void CreateInstance_WithValidParameters()
	{
		// Arrange
		Func<ISagaContext<TestSagaData>, CancellationToken, Task<bool>> condition =
			(_, _) => Task.FromResult(true);

		// Act
		var sut = new ConditionalSagaStep<TestSagaData>("TestStep", condition, null, null, NullLogger<ConditionalSagaStep<TestSagaData>>.Instance);

		// Assert
		sut.ShouldNotBeNull();
		sut.Name.ShouldBe("TestStep");
	}

	[Fact]
	public void CreateInstance_WithNullLogger()
	{
		// Arrange
		Func<ISagaContext<TestSagaData>, CancellationToken, Task<bool>> condition =
			(_, _) => Task.FromResult(true);

		// Act
		var sut = new ConditionalSagaStep<TestSagaData>("TestStep", condition, null, null, null);

		// Assert
		sut.ShouldNotBeNull();
	}

	[Fact]
	public void CreateInstance_WithThenAndElseSteps()
	{
		// Arrange
		Func<ISagaContext<TestSagaData>, CancellationToken, Task<bool>> condition =
			(_, _) => Task.FromResult(true);
		var thenStep = CreateFakeStep("ThenStep");
		var elseStep = CreateFakeStep("ElseStep");

		// Act
		var sut = new ConditionalSagaStep<TestSagaData>("TestStep", condition, thenStep, elseStep, NullLogger<ConditionalSagaStep<TestSagaData>>.Instance);

		// Assert
		sut.ThenStep.ShouldBeSameAs(thenStep);
		sut.ElseStep.ShouldBeSameAs(elseStep);
	}

	#endregion

	#region Property Tests

	[Fact]
	public void HaveDefaultTimeout_OfFiveMinutes()
	{
		// Arrange
		var sut = CreateSut(_ => true);

		// Assert
		sut.Timeout.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void AllowTimeoutToBeSet()
	{
		// Arrange
		var sut = CreateSut(_ => true);
		sut.Timeout = TimeSpan.FromSeconds(30);

		// Assert
		sut.Timeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void HaveDefaultStrategy_OfSimple()
	{
		// Arrange
		var sut = CreateSut(_ => true);

		// Assert
		sut.Strategy.ShouldBe(BranchingStrategy.Simple);
	}

	[Fact]
	public void ReturnCanCompensate_True_WhenBothStepsCanCompensate()
	{
		// Arrange
		var thenStep = CreateFakeStep("ThenStep", canCompensate: true);
		var elseStep = CreateFakeStep("ElseStep", canCompensate: true);
		var sut = CreateSut(_ => true, thenStep, elseStep);

		// Assert
		sut.CanCompensate.ShouldBeTrue();
	}

	[Fact]
	public void ReturnCanCompensate_True_WhenNoStepsProvided()
	{
		// Arrange
		var sut = CreateSut(_ => true, null, null);

		// Assert
		sut.CanCompensate.ShouldBeTrue();
	}

	[Fact]
	public void ReturnCanCompensate_False_WhenThenStepCannotCompensate()
	{
		// Arrange
		var thenStep = CreateFakeStep("ThenStep", canCompensate: false);
		var elseStep = CreateFakeStep("ElseStep", canCompensate: true);
		var sut = CreateSut(_ => true, thenStep, elseStep);

		// Assert
		sut.CanCompensate.ShouldBeFalse();
	}

	[Fact]
	public void ReturnCanCompensate_False_WhenElseStepCannotCompensate()
	{
		// Arrange
		var thenStep = CreateFakeStep("ThenStep", canCompensate: true);
		var elseStep = CreateFakeStep("ElseStep", canCompensate: false);
		var sut = CreateSut(_ => true, thenStep, elseStep);

		// Assert
		sut.CanCompensate.ShouldBeFalse();
	}

	#endregion

	#region EvaluateConditionAsync Tests

	[Fact]
	public async Task EvaluateCondition_ReturnsTrue_WhenConditionIsTrue()
	{
		// Arrange
		var sut = CreateSut(_ => true);

		// Act
		var result = await sut.EvaluateConditionAsync(_context, CancellationToken.None);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public async Task EvaluateCondition_ReturnsFalse_WhenConditionIsFalse()
	{
		// Arrange
		var sut = CreateSut(_ => false);

		// Act
		var result = await sut.EvaluateConditionAsync(_context, CancellationToken.None);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task EvaluateCondition_RethrowsException_WhenConditionThrows()
	{
		// Arrange
		Func<ISagaContext<TestSagaData>, CancellationToken, Task<bool>> condition =
			(_, _) => throw new InvalidOperationException("Condition evaluation failed");
		var sut = new ConditionalSagaStep<TestSagaData>("TestStep", condition, null, null, NullLogger<ConditionalSagaStep<TestSagaData>>.Instance);

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(async () =>
			await sut.EvaluateConditionAsync(_context, CancellationToken.None));
	}

	#endregion

	#region ExecuteAsync Tests

	[Fact]
	public async Task ExecuteThenStep_WhenConditionIsTrue()
	{
		// Arrange
		var thenStep = CreateFakeStep("ThenStep");
		var elseStep = CreateFakeStep("ElseStep");

		A.CallTo(() => thenStep.ExecuteAsync(_context, A<CancellationToken>._))
			.Returns(StepResult.Success(new Dictionary<string, object>()));

		var sut = CreateSut(_ => true, thenStep, elseStep);

		// Act
		var result = await sut.ExecuteAsync(_context, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		A.CallTo(() => thenStep.ExecuteAsync(_context, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => elseStep.ExecuteAsync(_context, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task ExecuteElseStep_WhenConditionIsFalse()
	{
		// Arrange
		var thenStep = CreateFakeStep("ThenStep");
		var elseStep = CreateFakeStep("ElseStep");

		A.CallTo(() => elseStep.ExecuteAsync(_context, A<CancellationToken>._))
			.Returns(StepResult.Success(new Dictionary<string, object>()));

		var sut = CreateSut(_ => false, thenStep, elseStep);

		// Act
		var result = await sut.ExecuteAsync(_context, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		A.CallTo(() => elseStep.ExecuteAsync(_context, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => thenStep.ExecuteAsync(_context, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task ReturnSuccess_WhenConditionIsTrueAndNoThenStep()
	{
		// Arrange
		var sut = CreateSut(_ => true, null, null);

		// Act
		var result = await sut.ExecuteAsync(_context, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		result.OutputData.ShouldNotBeNull();
		result.OutputData["ConditionResult"].ShouldBe(true);
		result.OutputData["Branch"].ShouldBe("Then");
		result.OutputData["Skipped"].ShouldBe(true);
	}

	[Fact]
	public async Task ReturnSuccess_WhenConditionIsFalseAndNoElseStep()
	{
		// Arrange
		var sut = CreateSut(_ => false, null, null);

		// Act
		var result = await sut.ExecuteAsync(_context, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		result.OutputData.ShouldNotBeNull();
		result.OutputData["ConditionResult"].ShouldBe(false);
		result.OutputData["Branch"].ShouldBe("Else");
		result.OutputData["Skipped"].ShouldBe(true);
	}

	[Fact]
	public async Task AddBranchInfo_ToSuccessfulResult()
	{
		// Arrange
		var thenStep = CreateFakeStep("ThenStep");

		A.CallTo(() => thenStep.ExecuteAsync(_context, A<CancellationToken>._))
			.Returns(StepResult.Success(new Dictionary<string, object> { ["Data"] = "Value" }));

		var sut = CreateSut(_ => true, thenStep, null);

		// Act
		var result = await sut.ExecuteAsync(_context, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		result.OutputData.ShouldNotBeNull();
		result.OutputData["ConditionalBranch"].ShouldBe("Then");
		result.OutputData["ConditionalStepName"].ShouldBe("TestStep");
	}

	[Fact]
	public async Task ReturnFailure_WhenStepReturnsNull()
	{
		// Arrange
		var thenStep = CreateFakeStep("ThenStep");

		A.CallTo(() => thenStep.ExecuteAsync(_context, A<CancellationToken>._))
			.Returns((StepResult)null!);

		var sut = CreateSut(_ => true, thenStep, null);

		// Act
		var result = await sut.ExecuteAsync(_context, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeFalse();
		result.ErrorMessage.ShouldContain("returned null result");
	}

	[Fact]
	public async Task ReturnFailure_WhenStepThrowsException()
	{
		// Arrange
		var thenStep = CreateFakeStep("ThenStep");

		A.CallTo(() => thenStep.ExecuteAsync(_context, A<CancellationToken>._))
			.Throws(new InvalidOperationException("Step execution failed"));

		var sut = CreateSut(_ => true, thenStep, null);

		// Act
		var result = await sut.ExecuteAsync(_context, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeFalse();
		result.ErrorMessage.ShouldContain("failed");
	}

	#endregion

	#region CompensateAsync Tests

	[Fact]
	public async Task ReturnSuccess_WhenNoStepWasExecuted()
	{
		// Arrange
		var sut = CreateSut(_ => true, null, null);

		// We need to call ExecuteAsync first to set _executedStep to null (since no ThenStep exists)
		_ = await sut.ExecuteAsync(_context, CancellationToken.None);

		// Act
		var result = await sut.CompensateAsync(_context, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public async Task CompensateExecutedStep_WhenItCanCompensate()
	{
		// Arrange
		var thenStep = CreateFakeStep("ThenStep", canCompensate: true);

		A.CallTo(() => thenStep.ExecuteAsync(_context, A<CancellationToken>._))
			.Returns(StepResult.Success(new Dictionary<string, object>()));
		A.CallTo(() => thenStep.CompensateAsync(_context, A<CancellationToken>._))
			.Returns(StepResult.Success());

		var sut = CreateSut(_ => true, thenStep, null);

		// Execute first to set _executedStep
		_ = await sut.ExecuteAsync(_context, CancellationToken.None);

		// Act
		var result = await sut.CompensateAsync(_context, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		A.CallTo(() => thenStep.CompensateAsync(_context, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ReturnSuccess_WhenExecutedStepCannotCompensate()
	{
		// Arrange
		var thenStep = CreateFakeStep("ThenStep", canCompensate: false);

		A.CallTo(() => thenStep.ExecuteAsync(_context, A<CancellationToken>._))
			.Returns(StepResult.Success(new Dictionary<string, object>()));

		var sut = CreateSut(_ => true, thenStep, null);

		// Execute first to set _executedStep
		_ = await sut.ExecuteAsync(_context, CancellationToken.None);

		// Act
		var result = await sut.CompensateAsync(_context, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		A.CallTo(() => thenStep.CompensateAsync(_context, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task ReturnFailure_WhenCompensationThrowsException()
	{
		// Arrange
		var thenStep = CreateFakeStep("ThenStep", canCompensate: true);

		A.CallTo(() => thenStep.ExecuteAsync(_context, A<CancellationToken>._))
			.Returns(StepResult.Success(new Dictionary<string, object>()));
		A.CallTo(() => thenStep.CompensateAsync(_context, A<CancellationToken>._))
			.Throws(new InvalidOperationException("Compensation failed"));

		var sut = CreateSut(_ => true, thenStep, null);

		// Execute first to set _executedStep
		_ = await sut.ExecuteAsync(_context, CancellationToken.None);

		// Act
		var result = await sut.CompensateAsync(_context, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeFalse();
		result.ErrorMessage.ShouldContain("Compensation failed");
	}

	#endregion

	#region Builder Tests

	[Fact]
	public void CreateBuilder_ReturnsBuilderInstance()
	{
		// Act
		var builder = ConditionalSagaStep<TestSagaData>.CreateBuilder("TestStep");

		// Assert
		builder.ShouldNotBeNull();
	}

	#endregion

	#region Helper Methods

	private ConditionalSagaStep<TestSagaData> CreateSut(
		Func<ISagaContext<TestSagaData>, bool> syncCondition,
		ISagaStep<TestSagaData>? thenStep = null,
		ISagaStep<TestSagaData>? elseStep = null)
	{
		Func<ISagaContext<TestSagaData>, CancellationToken, Task<bool>> condition =
			(ctx, _) => Task.FromResult(syncCondition(ctx));

		return new ConditionalSagaStep<TestSagaData>("TestStep", condition, thenStep, elseStep, NullLogger<ConditionalSagaStep<TestSagaData>>.Instance);
	}

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
