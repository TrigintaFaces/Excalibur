// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Implementation;
using Excalibur.Saga.Models;

using Microsoft.Extensions.Logging.Abstractions;

using StepResult = Excalibur.Saga.Abstractions.StepResult;

namespace Excalibur.Saga.Tests.Implementation;

/// <summary>
/// Test data class for MultiConditionalSagaStep tests.
/// </summary>
public sealed class MultiConditionalSagaStepTestData
{
	public int OrderId { get; set; }
	public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Unit tests for <see cref="MultiConditionalSagaStep{TData}"/>.
/// Verifies multi-branch conditional evaluation, execution, and compensation behavior.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class MultiConditionalSagaStepShould
{
	private readonly ISagaContext<MultiConditionalSagaStepTestData> _context;

	public MultiConditionalSagaStepShould()
	{
		_context = A.Fake<ISagaContext<MultiConditionalSagaStepTestData>>();
	}

	#region Constructor Tests

	[Fact]
	public void ThrowArgumentNullException_WhenNameIsNull()
	{
		// Arrange
		Func<ISagaContext<MultiConditionalSagaStepTestData>, CancellationToken, Task<string>> branchEvaluator =
			(_, _) => Task.FromResult("branch1");
		var branches = new Dictionary<string, ISagaStep<MultiConditionalSagaStepTestData>>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new MultiConditionalSagaStep<MultiConditionalSagaStepTestData>(null!, branchEvaluator, branches, null, NullLogger<MultiConditionalSagaStep<MultiConditionalSagaStepTestData>>.Instance));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenBranchEvaluatorIsNull()
	{
		// Arrange
		var branches = new Dictionary<string, ISagaStep<MultiConditionalSagaStepTestData>>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new MultiConditionalSagaStep<MultiConditionalSagaStepTestData>("TestStep", null!, branches, null, NullLogger<MultiConditionalSagaStep<MultiConditionalSagaStepTestData>>.Instance));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenBranchesIsNull()
	{
		// Arrange
		Func<ISagaContext<MultiConditionalSagaStepTestData>, CancellationToken, Task<string>> branchEvaluator =
			(_, _) => Task.FromResult("branch1");

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new MultiConditionalSagaStep<MultiConditionalSagaStepTestData>("TestStep", branchEvaluator, null!, null, NullLogger<MultiConditionalSagaStep<MultiConditionalSagaStepTestData>>.Instance));
	}

	[Fact]
	public void CreateInstance_WithValidParameters()
	{
		// Arrange
		Func<ISagaContext<MultiConditionalSagaStepTestData>, CancellationToken, Task<string>> branchEvaluator =
			(_, _) => Task.FromResult("branch1");
		var branches = new Dictionary<string, ISagaStep<MultiConditionalSagaStepTestData>>();

		// Act
		var sut = new MultiConditionalSagaStep<MultiConditionalSagaStepTestData>(
			"TestStep", branchEvaluator, branches, null, NullLogger<MultiConditionalSagaStep<MultiConditionalSagaStepTestData>>.Instance);

		// Assert
		sut.ShouldNotBeNull();
		sut.Name.ShouldBe("TestStep");
	}

	[Fact]
	public void CreateInstance_WithNullLogger()
	{
		// Arrange
		Func<ISagaContext<MultiConditionalSagaStepTestData>, CancellationToken, Task<string>> branchEvaluator =
			(_, _) => Task.FromResult("branch1");
		var branches = new Dictionary<string, ISagaStep<MultiConditionalSagaStepTestData>>();

		// Act
		var sut = new MultiConditionalSagaStep<MultiConditionalSagaStepTestData>("TestStep", branchEvaluator, branches, null, null);

		// Assert
		sut.ShouldNotBeNull();
	}

	[Fact]
	public void CreateInstance_WithDefaultStep()
	{
		// Arrange
		Func<ISagaContext<MultiConditionalSagaStepTestData>, CancellationToken, Task<string>> branchEvaluator =
			(_, _) => Task.FromResult("branch1");
		var branches = new Dictionary<string, ISagaStep<MultiConditionalSagaStepTestData>>();
		var defaultStep = CreateFakeStep("DefaultStep");

		// Act
		var sut = new MultiConditionalSagaStep<MultiConditionalSagaStepTestData>(
			"TestStep", branchEvaluator, branches, defaultStep, NullLogger<MultiConditionalSagaStep<MultiConditionalSagaStepTestData>>.Instance);

		// Assert
		sut.DefaultStep.ShouldBeSameAs(defaultStep);
	}

	#endregion

	#region Property Tests

	[Fact]
	public void ExposeName()
	{
		// Arrange
		var sut = CreateStep("TestStep", _ => "branch1");

		// Assert
		sut.Name.ShouldBe("TestStep");
	}

	[Fact]
	public void HaveDefaultTimeout_OfFiveMinutes()
	{
		// Arrange
		var sut = CreateStep("TestStep", _ => "branch1");

		// Assert
		sut.Timeout.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void AllowTimeoutToBeSet()
	{
		// Arrange
		var sut = CreateStep("TestStep", _ => "branch1");

		// Act
		sut.Timeout = TimeSpan.FromMinutes(10);

		// Assert
		sut.Timeout.ShouldBe(TimeSpan.FromMinutes(10));
	}

	[Fact]
	public void HaveNullRetryPolicy_ByDefault()
	{
		// Arrange
		var sut = CreateStep("TestStep", _ => "branch1");

		// Assert
		sut.RetryPolicy.ShouldBeNull();
	}

	[Fact]
	public void AllowRetryPolicyToBeSet()
	{
		// Arrange
		var sut = CreateStep("TestStep", _ => "branch1");
		var retryPolicy = new RetryPolicy { MaxAttempts = 5 };

		// Act
		sut.RetryPolicy = retryPolicy;

		// Assert
		sut.RetryPolicy.ShouldBe(retryPolicy);
	}

	[Fact]
	public void ExposeBranches()
	{
		// Arrange
		var branch1 = CreateFakeStep("Branch1");
		var branch2 = CreateFakeStep("Branch2");
		var branches = new Dictionary<string, ISagaStep<MultiConditionalSagaStepTestData>>
		{
			["key1"] = branch1,
			["key2"] = branch2
		};

		Func<ISagaContext<MultiConditionalSagaStepTestData>, CancellationToken, Task<string>> branchEvaluator =
			(_, _) => Task.FromResult("key1");

		var sut = new MultiConditionalSagaStep<MultiConditionalSagaStepTestData>(
			"TestStep", branchEvaluator, branches, null, null);

		// Assert
		sut.Branches.Count.ShouldBe(2);
		sut.Branches["key1"].ShouldBeSameAs(branch1);
		sut.Branches["key2"].ShouldBeSameAs(branch2);
	}

	#endregion

	#region CanCompensate Tests

	[Fact]
	public void ReturnTrue_WhenAllBranchesCanCompensate()
	{
		// Arrange
		var branch1 = CreateFakeStep("Branch1", canCompensate: true);
		var branch2 = CreateFakeStep("Branch2", canCompensate: true);
		var branches = new Dictionary<string, ISagaStep<MultiConditionalSagaStepTestData>>
		{
			["key1"] = branch1,
			["key2"] = branch2
		};

		var sut = new MultiConditionalSagaStep<MultiConditionalSagaStepTestData>(
			"TestStep", (_, _) => Task.FromResult("key1"), branches, null, null);

		// Assert
		sut.CanCompensate.ShouldBeTrue();
	}

	[Fact]
	public void ReturnFalse_WhenAnyBranchCannotCompensate()
	{
		// Arrange
		var branch1 = CreateFakeStep("Branch1", canCompensate: true);
		var branch2 = CreateFakeStep("Branch2", canCompensate: false);
		var branches = new Dictionary<string, ISagaStep<MultiConditionalSagaStepTestData>>
		{
			["key1"] = branch1,
			["key2"] = branch2
		};

		var sut = new MultiConditionalSagaStep<MultiConditionalSagaStepTestData>(
			"TestStep", (_, _) => Task.FromResult("key1"), branches, null, null);

		// Assert
		sut.CanCompensate.ShouldBeFalse();
	}

	[Fact]
	public void ReturnTrue_WhenBranchesEmptyAndNoDefaultStep()
	{
		// Arrange
		var branches = new Dictionary<string, ISagaStep<MultiConditionalSagaStepTestData>>();
		var sut = new MultiConditionalSagaStep<MultiConditionalSagaStepTestData>(
			"TestStep", (_, _) => Task.FromResult("key1"), branches, null, null);

		// Assert
		sut.CanCompensate.ShouldBeTrue();
	}

	[Fact]
	public void ReturnFalse_WhenDefaultStepCannotCompensate()
	{
		// Arrange
		var defaultStep = CreateFakeStep("Default", canCompensate: false);
		var branches = new Dictionary<string, ISagaStep<MultiConditionalSagaStepTestData>>();
		var sut = new MultiConditionalSagaStep<MultiConditionalSagaStepTestData>(
			"TestStep", (_, _) => Task.FromResult("key1"), branches, defaultStep, null);

		// Assert
		sut.CanCompensate.ShouldBeFalse();
	}

	#endregion

	#region EvaluateBranchAsync Tests

	[Fact]
	public async Task ReturnBranchFromEvaluator()
	{
		// Arrange
		var sut = CreateStep("TestStep", _ => "expectedBranch");

		// Act
		var branch = await sut.EvaluateBranchAsync(_context, CancellationToken.None);

		// Assert
		branch.ShouldBe("expectedBranch");
	}

	[Fact]
	public async Task PassContextToEvaluator()
	{
		// Arrange
		ISagaContext<MultiConditionalSagaStepTestData>? capturedContext = null;
		var sut = CreateStep("TestStep", ctx =>
		{
			capturedContext = ctx;
			return "branch1";
		});

		// Act
		await sut.EvaluateBranchAsync(_context, CancellationToken.None);

		// Assert
		capturedContext.ShouldBe(_context);
	}

	[Fact]
	public async Task RethrowException_FromBranchEvaluator()
	{
		// Arrange
		Func<ISagaContext<MultiConditionalSagaStepTestData>, CancellationToken, Task<string>> branchEvaluator =
			(_, _) => throw new InvalidOperationException("Evaluation failed");

		var sut = new MultiConditionalSagaStep<MultiConditionalSagaStepTestData>(
			"TestStep", branchEvaluator, new Dictionary<string, ISagaStep<MultiConditionalSagaStepTestData>>(), null, null);

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(async () =>
			await sut.EvaluateBranchAsync(_context, CancellationToken.None));
	}

	#endregion

	#region ExecuteAsync Tests

	[Fact]
	public async Task ExecuteMatchingBranch()
	{
		// Arrange
		var branchStep = CreateFakeStep("BranchStep");
		A.CallTo(() => branchStep.ExecuteAsync(A<ISagaContext<MultiConditionalSagaStepTestData>>._, A<CancellationToken>._))
			.Returns(StepResult.Success());

		var branches = new Dictionary<string, ISagaStep<MultiConditionalSagaStepTestData>>
		{
			["branch1"] = branchStep
		};

		var sut = new MultiConditionalSagaStep<MultiConditionalSagaStepTestData>(
			"TestStep", (_, _) => Task.FromResult("branch1"), branches, null, null);

		// Act
		var result = await sut.ExecuteAsync(_context, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		A.CallTo(() => branchStep.ExecuteAsync(_context, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ExecuteDefaultStep_WhenBranchNotFound()
	{
		// Arrange
		var defaultStep = CreateFakeStep("DefaultStep");
		A.CallTo(() => defaultStep.ExecuteAsync(A<ISagaContext<MultiConditionalSagaStepTestData>>._, A<CancellationToken>._))
			.Returns(StepResult.Success());

		var branches = new Dictionary<string, ISagaStep<MultiConditionalSagaStepTestData>>();

		var sut = new MultiConditionalSagaStep<MultiConditionalSagaStepTestData>(
			"TestStep", (_, _) => Task.FromResult("unknown"), branches, defaultStep, null);

		// Act
		var result = await sut.ExecuteAsync(_context, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		A.CallTo(() => defaultStep.ExecuteAsync(_context, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ReturnSkippedSuccess_WhenNoBranchAndNoDefault()
	{
		// Arrange
		var branches = new Dictionary<string, ISagaStep<MultiConditionalSagaStepTestData>>();
		var sut = new MultiConditionalSagaStep<MultiConditionalSagaStepTestData>(
			"TestStep", (_, _) => Task.FromResult("unknown"), branches, null, null);

		// Act
		var result = await sut.ExecuteAsync(_context, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		result.OutputData.ShouldContainKey("Skipped");
		result.OutputData["Skipped"].ShouldBe(true);
	}

	[Fact]
	public async Task ReturnFailure_WhenBranchStepFails()
	{
		// Arrange
		var branchStep = CreateFakeStep("BranchStep");
		A.CallTo(() => branchStep.ExecuteAsync(A<ISagaContext<MultiConditionalSagaStepTestData>>._, A<CancellationToken>._))
			.Returns(StepResult.Failure("Branch failed"));

		var branches = new Dictionary<string, ISagaStep<MultiConditionalSagaStepTestData>>
		{
			["branch1"] = branchStep
		};

		var sut = new MultiConditionalSagaStep<MultiConditionalSagaStepTestData>(
			"TestStep", (_, _) => Task.FromResult("branch1"), branches, null, null);

		// Act
		var result = await sut.ExecuteAsync(_context, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeFalse();
	}

	[Fact]
	public async Task ReturnFailure_WhenExceptionOccurs()
	{
		// Arrange
		var branchStep = CreateFakeStep("BranchStep");
		A.CallTo(() => branchStep.ExecuteAsync(A<ISagaContext<MultiConditionalSagaStepTestData>>._, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("Step failed"));

		var branches = new Dictionary<string, ISagaStep<MultiConditionalSagaStepTestData>>
		{
			["branch1"] = branchStep
		};

		var sut = new MultiConditionalSagaStep<MultiConditionalSagaStepTestData>(
			"TestStep", (_, _) => Task.FromResult("branch1"), branches, null, null);

		// Act
		var result = await sut.ExecuteAsync(_context, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeFalse();
		result.ErrorMessage.ShouldContain("Step failed");
	}

	[Fact]
	public async Task AddBranchInfoToResult()
	{
		// Arrange
		var branchStep = CreateFakeStep("BranchStep");
		var outputData = new Dictionary<string, object>(StringComparer.Ordinal);
		A.CallTo(() => branchStep.ExecuteAsync(A<ISagaContext<MultiConditionalSagaStepTestData>>._, A<CancellationToken>._))
			.Returns(new StepResult { IsSuccess = true, OutputData = outputData });

		var branches = new Dictionary<string, ISagaStep<MultiConditionalSagaStepTestData>>
		{
			["branch1"] = branchStep
		};

		var sut = new MultiConditionalSagaStep<MultiConditionalSagaStepTestData>(
			"TestStep", (_, _) => Task.FromResult("branch1"), branches, null, null);

		// Act
		var result = await sut.ExecuteAsync(_context, CancellationToken.None);

		// Assert
		result.OutputData.ShouldContainKey("ExecutedBranch");
		result.OutputData["ExecutedBranch"].ShouldBe("branch1");
		result.OutputData.ShouldContainKey("MultiConditionalStepName");
		result.OutputData["MultiConditionalStepName"].ShouldBe("TestStep");
	}

	#endregion

	#region CompensateAsync Tests

	[Fact]
	public async Task ReturnSuccess_WhenNoStepWasExecuted()
	{
		// Arrange
		var sut = CreateStep("TestStep", _ => "branch1");

		// Act
		var result = await sut.CompensateAsync(_context, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public async Task CompensateExecutedStep()
	{
		// Arrange
		var branchStep = CreateFakeStep("BranchStep", canCompensate: true);
		A.CallTo(() => branchStep.ExecuteAsync(A<ISagaContext<MultiConditionalSagaStepTestData>>._, A<CancellationToken>._))
			.Returns(StepResult.Success());
		A.CallTo(() => branchStep.CompensateAsync(A<ISagaContext<MultiConditionalSagaStepTestData>>._, A<CancellationToken>._))
			.Returns(StepResult.Success());

		var branches = new Dictionary<string, ISagaStep<MultiConditionalSagaStepTestData>>
		{
			["branch1"] = branchStep
		};

		var sut = new MultiConditionalSagaStep<MultiConditionalSagaStepTestData>(
			"TestStep", (_, _) => Task.FromResult("branch1"), branches, null, null);

		// Execute first to set the executed step
		await sut.ExecuteAsync(_context, CancellationToken.None);

		// Act
		var result = await sut.CompensateAsync(_context, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		A.CallTo(() => branchStep.CompensateAsync(_context, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ReturnSuccess_WhenExecutedStepCannotCompensate()
	{
		// Arrange
		var branchStep = CreateFakeStep("BranchStep", canCompensate: false);
		A.CallTo(() => branchStep.ExecuteAsync(A<ISagaContext<MultiConditionalSagaStepTestData>>._, A<CancellationToken>._))
			.Returns(StepResult.Success());

		var branches = new Dictionary<string, ISagaStep<MultiConditionalSagaStepTestData>>
		{
			["branch1"] = branchStep
		};

		var sut = new MultiConditionalSagaStep<MultiConditionalSagaStepTestData>(
			"TestStep", (_, _) => Task.FromResult("branch1"), branches, null, null);

		// Execute first
		await sut.ExecuteAsync(_context, CancellationToken.None);

		// Act
		var result = await sut.CompensateAsync(_context, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		A.CallTo(() => branchStep.CompensateAsync(A<ISagaContext<MultiConditionalSagaStepTestData>>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task ReturnFailure_WhenCompensationFails()
	{
		// Arrange
		var branchStep = CreateFakeStep("BranchStep", canCompensate: true);
		A.CallTo(() => branchStep.ExecuteAsync(A<ISagaContext<MultiConditionalSagaStepTestData>>._, A<CancellationToken>._))
			.Returns(StepResult.Success());
		A.CallTo(() => branchStep.CompensateAsync(A<ISagaContext<MultiConditionalSagaStepTestData>>._, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("Compensation failed"));

		var branches = new Dictionary<string, ISagaStep<MultiConditionalSagaStepTestData>>
		{
			["branch1"] = branchStep
		};

		var sut = new MultiConditionalSagaStep<MultiConditionalSagaStepTestData>(
			"TestStep", (_, _) => Task.FromResult("branch1"), branches, null, null);

		// Execute first
		await sut.ExecuteAsync(_context, CancellationToken.None);

		// Act
		var result = await sut.CompensateAsync(_context, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeFalse();
		result.ErrorMessage.ShouldContain("Compensation failed");
	}

	#endregion

	#region CreateBuilder Tests

	[Fact]
	public void CreateBuilder_ReturnsBuilder()
	{
		// Act
		var builder = MultiConditionalSagaStep<MultiConditionalSagaStepTestData>.CreateBuilder("TestStep");

		// Assert
		builder.ShouldNotBeNull();
	}

	#endregion

	#region Helper Methods

	private static MultiConditionalSagaStep<MultiConditionalSagaStepTestData> CreateStep(
		string name,
		Func<ISagaContext<MultiConditionalSagaStepTestData>, string> branchEvaluator,
		IDictionary<string, ISagaStep<MultiConditionalSagaStepTestData>>? branches = null,
		ISagaStep<MultiConditionalSagaStepTestData>? defaultStep = null)
	{
		Func<ISagaContext<MultiConditionalSagaStepTestData>, CancellationToken, Task<string>> asyncBranchEvaluator =
			(ctx, _) => Task.FromResult(branchEvaluator(ctx));

		return new MultiConditionalSagaStep<MultiConditionalSagaStepTestData>(
			name,
			asyncBranchEvaluator,
			branches ?? new Dictionary<string, ISagaStep<MultiConditionalSagaStepTestData>>(),
			defaultStep,
			NullLogger<MultiConditionalSagaStep<MultiConditionalSagaStepTestData>>.Instance);
	}

	private static ISagaStep<MultiConditionalSagaStepTestData> CreateFakeStep(string name, bool canCompensate = true)
	{
		var step = A.Fake<ISagaStep<MultiConditionalSagaStepTestData>>();
		A.CallTo(() => step.Name).Returns(name);
		A.CallTo(() => step.CanCompensate).Returns(canCompensate);
		return step;
	}

	#endregion
}
