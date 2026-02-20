// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Implementation;
using Excalibur.Saga.Models;

namespace Excalibur.Saga.Tests.Implementation;

/// <summary>
/// Test data class for ConditionalStepBuilder tests.
/// </summary>
public sealed class ConditionalStepBuilderTestData
{
	public int OrderId { get; set; }
	public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Unit tests for <see cref="ConditionalStepBuilder{TData}"/>.
/// Verifies builder configuration and step construction.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class ConditionalStepBuilderShould
{
	#region Constructor Tests

	[Fact]
	public void ThrowArgumentNullException_WhenNameIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new ConditionalStepBuilder<ConditionalStepBuilderTestData>(null!));
	}

	[Fact]
	public void CreateInstance_WithValidName()
	{
		// Act
		var builder = new ConditionalStepBuilder<ConditionalStepBuilderTestData>("TestStep");

		// Assert
		builder.ShouldNotBeNull();
	}

	#endregion

	#region When Async Tests

	[Fact]
	public void ThrowArgumentNullException_WhenAsyncConditionIsNull()
	{
		// Arrange
		var builder = new ConditionalStepBuilder<ConditionalStepBuilderTestData>("TestStep");

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			builder.When((Func<ISagaContext<ConditionalStepBuilderTestData>, CancellationToken, Task<bool>>)null!));
	}

	[Fact]
	public void ReturnBuilder_AfterSettingAsyncCondition()
	{
		// Arrange
		var builder = new ConditionalStepBuilder<ConditionalStepBuilderTestData>("TestStep");
		Func<ISagaContext<ConditionalStepBuilderTestData>, CancellationToken, Task<bool>> condition =
			(_, _) => Task.FromResult(true);

		// Act
		var result = builder.When(condition);

		// Assert
		result.ShouldBe(builder);
	}

	#endregion

	#region When Sync Tests

	[Fact]
	public void ThrowArgumentNullException_WhenSyncPredicateIsNull()
	{
		// Arrange
		var builder = new ConditionalStepBuilder<ConditionalStepBuilderTestData>("TestStep");

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			builder.When((Func<ISagaContext<ConditionalStepBuilderTestData>, bool>)null!));
	}

	[Fact]
	public void ReturnBuilder_AfterSettingSyncPredicate()
	{
		// Arrange
		var builder = new ConditionalStepBuilder<ConditionalStepBuilderTestData>("TestStep");
		Func<ISagaContext<ConditionalStepBuilderTestData>, bool> predicate = _ => true;

		// Act
		var result = builder.When(predicate);

		// Assert
		result.ShouldBe(builder);
	}

	#endregion

	#region Then Tests

	[Fact]
	public void ThrowArgumentNullException_WhenThenStepIsNull()
	{
		// Arrange
		var builder = new ConditionalStepBuilder<ConditionalStepBuilderTestData>("TestStep");

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			builder.Then(null!));
	}

	[Fact]
	public void ReturnBuilder_AfterSettingThenStep()
	{
		// Arrange
		var builder = new ConditionalStepBuilder<ConditionalStepBuilderTestData>("TestStep");
		var step = A.Fake<ISagaStep<ConditionalStepBuilderTestData>>();

		// Act
		var result = builder.Then(step);

		// Assert
		result.ShouldBe(builder);
	}

	#endregion

	#region Else Tests

	[Fact]
	public void ThrowArgumentNullException_WhenElseStepIsNull()
	{
		// Arrange
		var builder = new ConditionalStepBuilder<ConditionalStepBuilderTestData>("TestStep");

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			builder.Else(null!));
	}

	[Fact]
	public void ReturnBuilder_AfterSettingElseStep()
	{
		// Arrange
		var builder = new ConditionalStepBuilder<ConditionalStepBuilderTestData>("TestStep");
		var step = A.Fake<ISagaStep<ConditionalStepBuilderTestData>>();

		// Act
		var result = builder.Else(step);

		// Assert
		result.ShouldBe(builder);
	}

	#endregion

	#region WithTimeout Tests

	[Fact]
	public void ReturnBuilder_AfterSettingTimeout()
	{
		// Arrange
		var builder = new ConditionalStepBuilder<ConditionalStepBuilderTestData>("TestStep");

		// Act
		var result = builder.WithTimeout(TimeSpan.FromMinutes(10));

		// Assert
		result.ShouldBe(builder);
	}

	[Fact]
	public void SetTimeout_OnBuiltStep()
	{
		// Arrange
		var builder = new ConditionalStepBuilder<ConditionalStepBuilderTestData>("TestStep");
		var thenStep = A.Fake<ISagaStep<ConditionalStepBuilderTestData>>();

		// Act
		builder.When(_ => true);
		builder.Then(thenStep);
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
		var builder = new ConditionalStepBuilder<ConditionalStepBuilderTestData>("TestStep");
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
		var builder = new ConditionalStepBuilder<ConditionalStepBuilderTestData>("TestStep");
		var thenStep = A.Fake<ISagaStep<ConditionalStepBuilderTestData>>();
		var retryPolicy = new RetryPolicy { MaxAttempts = 5 };

		// Act
		builder.When(_ => true);
		builder.Then(thenStep);
		builder.WithRetryPolicy(retryPolicy);
		var step = builder.Build();

		// Assert
		step.RetryPolicy.ShouldBe(retryPolicy);
	}

	#endregion

	#region Build Tests

	[Fact]
	public void ThrowInvalidOperationException_WhenConditionNotSet()
	{
		// Arrange
		var builder = new ConditionalStepBuilder<ConditionalStepBuilderTestData>("TestStep");
		var thenStep = A.Fake<ISagaStep<ConditionalStepBuilderTestData>>();
		builder.Then(thenStep);

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => builder.Build());
	}

	[Fact]
	public void BuildStep_WithValidConfiguration()
	{
		// Arrange
		var builder = new ConditionalStepBuilder<ConditionalStepBuilderTestData>("TestStep");
		var thenStep = A.Fake<ISagaStep<ConditionalStepBuilderTestData>>();

		// Act
		builder.When(_ => true);
		builder.Then(thenStep);
		var step = builder.Build();

		// Assert
		step.ShouldNotBeNull();
		step.Name.ShouldBe("TestStep");
	}

	[Fact]
	public void BuildStep_WithDefaultTimeout()
	{
		// Arrange
		var builder = new ConditionalStepBuilder<ConditionalStepBuilderTestData>("TestStep");
		var thenStep = A.Fake<ISagaStep<ConditionalStepBuilderTestData>>();

		// Act
		builder.When(_ => true);
		builder.Then(thenStep);
		var step = builder.Build();

		// Assert
		step.Timeout.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void BuildStep_WithNullRetryPolicyByDefault()
	{
		// Arrange
		var builder = new ConditionalStepBuilder<ConditionalStepBuilderTestData>("TestStep");
		var thenStep = A.Fake<ISagaStep<ConditionalStepBuilderTestData>>();

		// Act
		builder.When(_ => true);
		builder.Then(thenStep);
		var step = builder.Build();

		// Assert
		step.RetryPolicy.ShouldBeNull();
	}

	[Fact]
	public void BuildStep_WithBothThenAndElseSteps()
	{
		// Arrange
		var builder = new ConditionalStepBuilder<ConditionalStepBuilderTestData>("TestStep");
		var thenStep = A.Fake<ISagaStep<ConditionalStepBuilderTestData>>();
		var elseStep = A.Fake<ISagaStep<ConditionalStepBuilderTestData>>();
		A.CallTo(() => thenStep.Name).Returns("ThenStep");
		A.CallTo(() => elseStep.Name).Returns("ElseStep");

		// Act
		builder.When(_ => true);
		builder.Then(thenStep);
		builder.Else(elseStep);
		var step = builder.Build();

		// Assert
		step.ThenStep.ShouldBeSameAs(thenStep);
		step.ElseStep.ShouldBeSameAs(elseStep);
	}

	[Fact]
	public void BuildStep_WithOnlyThenStep()
	{
		// Arrange
		var builder = new ConditionalStepBuilder<ConditionalStepBuilderTestData>("TestStep");
		var thenStep = A.Fake<ISagaStep<ConditionalStepBuilderTestData>>();

		// Act
		builder.When(_ => true);
		builder.Then(thenStep);
		var step = builder.Build();

		// Assert
		step.ThenStep.ShouldBeSameAs(thenStep);
		step.ElseStep.ShouldBeNull();
	}

	[Fact]
	public void BuildStep_WithOnlyElseStep()
	{
		// Arrange
		var builder = new ConditionalStepBuilder<ConditionalStepBuilderTestData>("TestStep");
		var elseStep = A.Fake<ISagaStep<ConditionalStepBuilderTestData>>();

		// Act
		builder.When(_ => false);
		builder.Else(elseStep);
		var step = builder.Build();

		// Assert
		step.ThenStep.ShouldBeNull();
		step.ElseStep.ShouldBeSameAs(elseStep);
	}

	#endregion

	#region Fluent Chain Tests

	[Fact]
	public void SupportFullFluentChain()
	{
		// Arrange
		var thenStep = A.Fake<ISagaStep<ConditionalStepBuilderTestData>>();
		var elseStep = A.Fake<ISagaStep<ConditionalStepBuilderTestData>>();
		var retryPolicy = new RetryPolicy { MaxAttempts = 3 };

		// Act
		var step = new ConditionalStepBuilder<ConditionalStepBuilderTestData>("TestStep")
			.When(_ => true)
			.Then(thenStep)
			.Else(elseStep)
			.WithTimeout(TimeSpan.FromMinutes(20))
			.WithRetryPolicy(retryPolicy)
			.Build();

		// Assert
		step.Name.ShouldBe("TestStep");
		step.ThenStep.ShouldBeSameAs(thenStep);
		step.ElseStep.ShouldBeSameAs(elseStep);
		step.Timeout.ShouldBe(TimeSpan.FromMinutes(20));
		step.RetryPolicy.ShouldBe(retryPolicy);
	}

	[Fact]
	public void SupportAsyncConditionInFluentChain()
	{
		// Arrange
		var thenStep = A.Fake<ISagaStep<ConditionalStepBuilderTestData>>();
		Func<ISagaContext<ConditionalStepBuilderTestData>, CancellationToken, Task<bool>> asyncCondition =
			(_, _) => Task.FromResult(true);

		// Act
		var step = new ConditionalStepBuilder<ConditionalStepBuilderTestData>("TestStep")
			.When(asyncCondition)
			.Then(thenStep)
			.Build();

		// Assert
		step.ShouldNotBeNull();
		step.Name.ShouldBe("TestStep");
	}

	#endregion
}
