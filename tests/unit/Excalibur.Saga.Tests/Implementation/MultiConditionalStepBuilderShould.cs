// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Implementation;
using Excalibur.Saga.Models;

namespace Excalibur.Saga.Tests.Implementation;

/// <summary>
/// Test data class for MultiConditionalStepBuilder tests.
/// </summary>
public sealed class MultiConditionalStepBuilderTestData
{
	public int OrderId { get; set; }
	public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Unit tests for <see cref="MultiConditionalStepBuilder{TData}"/>.
/// Verifies builder configuration and step construction.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class MultiConditionalStepBuilderShould
{
	#region Constructor Tests

	[Fact]
	public void ThrowArgumentNullException_WhenNameIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new MultiConditionalStepBuilder<MultiConditionalStepBuilderTestData>(null!));
	}

	[Fact]
	public void CreateInstance_WithValidName()
	{
		// Act
		var builder = new MultiConditionalStepBuilder<MultiConditionalStepBuilderTestData>("TestStep");

		// Assert
		builder.ShouldNotBeNull();
	}

	#endregion

	#region EvaluateWith Async Tests

	[Fact]
	public void ThrowArgumentNullException_WhenAsyncEvaluatorIsNull()
	{
		// Arrange
		var builder = new MultiConditionalStepBuilder<MultiConditionalStepBuilderTestData>("TestStep");

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			builder.EvaluateWith((Func<ISagaContext<MultiConditionalStepBuilderTestData>, CancellationToken, Task<string>>)null!));
	}

	[Fact]
	public void ReturnBuilder_AfterSettingAsyncEvaluator()
	{
		// Arrange
		var builder = new MultiConditionalStepBuilder<MultiConditionalStepBuilderTestData>("TestStep");
		Func<ISagaContext<MultiConditionalStepBuilderTestData>, CancellationToken, Task<string>> evaluator =
			(_, _) => Task.FromResult("branch1");

		// Act
		var result = builder.EvaluateWith(evaluator);

		// Assert
		result.ShouldBe(builder);
	}

	#endregion

	#region EvaluateWith Sync Tests

	[Fact]
	public void ThrowArgumentNullException_WhenSyncEvaluatorIsNull()
	{
		// Arrange
		var builder = new MultiConditionalStepBuilder<MultiConditionalStepBuilderTestData>("TestStep");

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			builder.EvaluateWith((Func<ISagaContext<MultiConditionalStepBuilderTestData>, string>)null!));
	}

	[Fact]
	public void ReturnBuilder_AfterSettingSyncEvaluator()
	{
		// Arrange
		var builder = new MultiConditionalStepBuilder<MultiConditionalStepBuilderTestData>("TestStep");
		Func<ISagaContext<MultiConditionalStepBuilderTestData>, string> evaluator = _ => "branch1";

		// Act
		var result = builder.EvaluateWith(evaluator);

		// Assert
		result.ShouldBe(builder);
	}

	#endregion

	#region AddBranch Tests

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ThrowArgumentException_WhenBranchKeyIsNullOrWhitespace(string? branchKey)
	{
		// Arrange
		var builder = new MultiConditionalStepBuilder<MultiConditionalStepBuilderTestData>("TestStep");
		var step = A.Fake<ISagaStep<MultiConditionalStepBuilderTestData>>();

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			builder.AddBranch(branchKey, step));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenStepIsNull()
	{
		// Arrange
		var builder = new MultiConditionalStepBuilder<MultiConditionalStepBuilderTestData>("TestStep");

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			builder.AddBranch("branch1", null!));
	}

	[Fact]
	public void ReturnBuilder_AfterAddingBranch()
	{
		// Arrange
		var builder = new MultiConditionalStepBuilder<MultiConditionalStepBuilderTestData>("TestStep");
		var step = A.Fake<ISagaStep<MultiConditionalStepBuilderTestData>>();

		// Act
		var result = builder.AddBranch("branch1", step);

		// Assert
		result.ShouldBe(builder);
	}

	[Fact]
	public void AllowMultipleBranches()
	{
		// Arrange
		var builder = new MultiConditionalStepBuilder<MultiConditionalStepBuilderTestData>("TestStep");
		var step1 = A.Fake<ISagaStep<MultiConditionalStepBuilderTestData>>();
		var step2 = A.Fake<ISagaStep<MultiConditionalStepBuilderTestData>>();

		// Act
		builder.AddBranch("branch1", step1).AddBranch("branch2", step2);
		builder.EvaluateWith(_ => "branch1");
		var step = builder.Build();

		// Assert
		step.Branches.Count.ShouldBe(2);
	}

	[Fact]
	public void OverwriteBranch_WhenSameKeyIsUsed()
	{
		// Arrange
		var builder = new MultiConditionalStepBuilder<MultiConditionalStepBuilderTestData>("TestStep");
		var step1 = A.Fake<ISagaStep<MultiConditionalStepBuilderTestData>>();
		var step2 = A.Fake<ISagaStep<MultiConditionalStepBuilderTestData>>();
		A.CallTo(() => step1.Name).Returns("Step1");
		A.CallTo(() => step2.Name).Returns("Step2");

		// Act
		builder.AddBranch("branch1", step1).AddBranch("branch1", step2);
		builder.EvaluateWith(_ => "branch1");
		var step = builder.Build();

		// Assert
		step.Branches["branch1"].Name.ShouldBe("Step2");
	}

	#endregion

	#region AddBranches Tests

	[Fact]
	public void ThrowArgumentNullException_WhenBranchesIsNull()
	{
		// Arrange
		var builder = new MultiConditionalStepBuilder<MultiConditionalStepBuilderTestData>("TestStep");

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			builder.AddBranches(null!));
	}

	[Fact]
	public void ReturnBuilder_AfterAddingBranches()
	{
		// Arrange
		var builder = new MultiConditionalStepBuilder<MultiConditionalStepBuilderTestData>("TestStep");
		var branches = new Dictionary<string, ISagaStep<MultiConditionalStepBuilderTestData>>
		{
			["branch1"] = A.Fake<ISagaStep<MultiConditionalStepBuilderTestData>>(),
			["branch2"] = A.Fake<ISagaStep<MultiConditionalStepBuilderTestData>>()
		};

		// Act
		var result = builder.AddBranches(branches);

		// Assert
		result.ShouldBe(builder);
	}

	[Fact]
	public void AddAllBranches()
	{
		// Arrange
		var builder = new MultiConditionalStepBuilder<MultiConditionalStepBuilderTestData>("TestStep");
		var branches = new Dictionary<string, ISagaStep<MultiConditionalStepBuilderTestData>>
		{
			["branch1"] = A.Fake<ISagaStep<MultiConditionalStepBuilderTestData>>(),
			["branch2"] = A.Fake<ISagaStep<MultiConditionalStepBuilderTestData>>(),
			["branch3"] = A.Fake<ISagaStep<MultiConditionalStepBuilderTestData>>()
		};

		// Act
		builder.AddBranches(branches);
		builder.EvaluateWith(_ => "branch1");
		var step = builder.Build();

		// Assert
		step.Branches.Count.ShouldBe(3);
	}

	#endregion

	#region WithDefault Tests

	[Fact]
	public void ThrowArgumentNullException_WhenDefaultStepIsNull()
	{
		// Arrange
		var builder = new MultiConditionalStepBuilder<MultiConditionalStepBuilderTestData>("TestStep");

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			builder.WithDefault(null!));
	}

	[Fact]
	public void ReturnBuilder_AfterSettingDefaultStep()
	{
		// Arrange
		var builder = new MultiConditionalStepBuilder<MultiConditionalStepBuilderTestData>("TestStep");
		var defaultStep = A.Fake<ISagaStep<MultiConditionalStepBuilderTestData>>();

		// Act
		var result = builder.WithDefault(defaultStep);

		// Assert
		result.ShouldBe(builder);
	}

	[Fact]
	public void SetDefaultStep_OnBuiltStep()
	{
		// Arrange
		var builder = new MultiConditionalStepBuilder<MultiConditionalStepBuilderTestData>("TestStep");
		var defaultStep = A.Fake<ISagaStep<MultiConditionalStepBuilderTestData>>();
		var branchStep = A.Fake<ISagaStep<MultiConditionalStepBuilderTestData>>();

		// Act
		builder.EvaluateWith(_ => "branch1");
		builder.AddBranch("branch1", branchStep);
		builder.WithDefault(defaultStep);
		var step = builder.Build();

		// Assert
		step.DefaultStep.ShouldBeSameAs(defaultStep);
	}

	#endregion

	#region WithTimeout Tests

	[Fact]
	public void ReturnBuilder_AfterSettingTimeout()
	{
		// Arrange
		var builder = new MultiConditionalStepBuilder<MultiConditionalStepBuilderTestData>("TestStep");

		// Act
		var result = builder.WithTimeout(TimeSpan.FromMinutes(10));

		// Assert
		result.ShouldBe(builder);
	}

	[Fact]
	public void SetTimeout_OnBuiltStep()
	{
		// Arrange
		var builder = new MultiConditionalStepBuilder<MultiConditionalStepBuilderTestData>("TestStep");
		var branchStep = A.Fake<ISagaStep<MultiConditionalStepBuilderTestData>>();

		// Act
		builder.EvaluateWith(_ => "branch1");
		builder.AddBranch("branch1", branchStep);
		builder.WithTimeout(TimeSpan.FromMinutes(15));
		var step = builder.Build();

		// Assert
		step.Timeout.ShouldBe(TimeSpan.FromMinutes(15));
	}

	#endregion

	#region WithRetryPolicy Tests

	[Fact]
	public void ReturnBuilder_AfterSettingRetryPolicy()
	{
		// Arrange
		var builder = new MultiConditionalStepBuilder<MultiConditionalStepBuilderTestData>("TestStep");
		var retryPolicy = new RetryPolicy { MaxAttempts = 3 };

		// Act
		var result = builder.WithRetryPolicy(retryPolicy);

		// Assert
		result.ShouldBe(builder);
	}

	[Fact]
	public void SetRetryPolicy_OnBuiltStep()
	{
		// Arrange
		var builder = new MultiConditionalStepBuilder<MultiConditionalStepBuilderTestData>("TestStep");
		var branchStep = A.Fake<ISagaStep<MultiConditionalStepBuilderTestData>>();
		var retryPolicy = new RetryPolicy { MaxAttempts = 5 };

		// Act
		builder.EvaluateWith(_ => "branch1");
		builder.AddBranch("branch1", branchStep);
		builder.WithRetryPolicy(retryPolicy);
		var step = builder.Build();

		// Assert
		step.RetryPolicy.ShouldBe(retryPolicy);
	}

	#endregion

	#region Build Tests

	[Fact]
	public void ThrowInvalidOperationException_WhenEvaluatorNotSet()
	{
		// Arrange
		var builder = new MultiConditionalStepBuilder<MultiConditionalStepBuilderTestData>("TestStep");
		var branchStep = A.Fake<ISagaStep<MultiConditionalStepBuilderTestData>>();
		builder.AddBranch("branch1", branchStep);

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => builder.Build());
	}

	[Fact]
	public void ThrowInvalidOperationException_WhenNoBranches()
	{
		// Arrange
		var builder = new MultiConditionalStepBuilder<MultiConditionalStepBuilderTestData>("TestStep");
		builder.EvaluateWith(_ => "branch1");

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => builder.Build());
	}

	[Fact]
	public void BuildStep_WithValidConfiguration()
	{
		// Arrange
		var builder = new MultiConditionalStepBuilder<MultiConditionalStepBuilderTestData>("TestStep");
		var branchStep = A.Fake<ISagaStep<MultiConditionalStepBuilderTestData>>();

		// Act
		builder.EvaluateWith(_ => "branch1");
		builder.AddBranch("branch1", branchStep);
		var step = builder.Build();

		// Assert
		step.ShouldNotBeNull();
		step.Name.ShouldBe("TestStep");
	}

	[Fact]
	public void BuildStep_WithDefaultTimeout()
	{
		// Arrange
		var builder = new MultiConditionalStepBuilder<MultiConditionalStepBuilderTestData>("TestStep");
		var branchStep = A.Fake<ISagaStep<MultiConditionalStepBuilderTestData>>();

		// Act
		builder.EvaluateWith(_ => "branch1");
		builder.AddBranch("branch1", branchStep);
		var step = builder.Build();

		// Assert
		step.Timeout.ShouldBe(TimeSpan.FromMinutes(5));
	}

	#endregion

	#region Fluent Chain Tests

	[Fact]
	public void SupportFullFluentChain()
	{
		// Arrange
		var branchStep = A.Fake<ISagaStep<MultiConditionalStepBuilderTestData>>();
		var defaultStep = A.Fake<ISagaStep<MultiConditionalStepBuilderTestData>>();
		var retryPolicy = new RetryPolicy { MaxAttempts = 3 };

		// Act
		var step = new MultiConditionalStepBuilder<MultiConditionalStepBuilderTestData>("TestStep")
			.EvaluateWith((Func<ISagaContext<MultiConditionalStepBuilderTestData>, string>)(_ => "branch1"))
			.AddBranch("branch1", branchStep)
			.WithDefault(defaultStep)
			.WithTimeout(TimeSpan.FromMinutes(20))
			.WithRetryPolicy(retryPolicy)
			.Build();

		// Assert
		step.Name.ShouldBe("TestStep");
		step.Branches.Count.ShouldBe(1);
		step.DefaultStep.ShouldBeSameAs(defaultStep);
		step.Timeout.ShouldBe(TimeSpan.FromMinutes(20));
		step.RetryPolicy.ShouldBe(retryPolicy);
	}

	#endregion
}
