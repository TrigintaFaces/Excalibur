// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Hosting.AzureFunctions;

namespace Excalibur.Hosting.Tests.AzureFunctions.Sagas;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class SagaBuilderShould : UnitTestBase
{
	[Fact]
	public void CreateBuilderWithName()
	{
		// Act
		var builder = SagaBuilder<SagaTestInput, SagaTestOutput>.Create("test-saga");

		// Assert
		builder.ShouldNotBeNull();
	}

	[Fact]
	public void ThrowWhenNameIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			SagaBuilder<SagaTestInput, SagaTestOutput>.Create(null!));
	}

	[Fact]
	public void SupportFluentTimeout()
	{
		// Act
		var builder = SagaBuilder<SagaTestInput, SagaTestOutput>.Create("saga")
			.WithTimeout(TimeSpan.FromMinutes(60));

		// Assert
		builder.ShouldNotBeNull();
	}

	[Fact]
	public void SupportFluentAutoCompensation()
	{
		// Act
		var builder = SagaBuilder<SagaTestInput, SagaTestOutput>.Create("saga")
			.WithAutoCompensation(false);

		// Assert
		builder.ShouldNotBeNull();
	}

	[Fact]
	public void SupportFluentDefaultRetry()
	{
		// Act
		var builder = SagaBuilder<SagaTestInput, SagaTestOutput>.Create("saga")
			.WithDefaultRetry(5, TimeSpan.FromSeconds(2), 3.0);

		// Assert
		builder.ShouldNotBeNull();
	}

	[Fact]
	public void SupportFluentInputValidation()
	{
		// Act
		var builder = SagaBuilder<SagaTestInput, SagaTestOutput>.Create("saga")
			.WithInputValidation(_ => Task.CompletedTask);

		// Assert
		builder.ShouldNotBeNull();
	}

	[Fact]
	public void SupportFluentOutputBuilder()
	{
		// Act
		var builder = SagaBuilder<SagaTestInput, SagaTestOutput>.Create("saga")
			.WithOutputBuilder((_, _) => Task.FromResult(new SagaTestOutput()));

		// Assert
		builder.ShouldNotBeNull();
	}

	[Fact]
	public void SupportFluentErrorHandler()
	{
		// Act
		var builder = SagaBuilder<SagaTestInput, SagaTestOutput>.Create("saga")
			.WithErrorHandler((_, _) => { });

		// Assert
		builder.ShouldNotBeNull();
	}

	[Fact]
	public void SupportFluentChaining()
	{
		// Act
		var builder = SagaBuilder<SagaTestInput, SagaTestOutput>.Create("saga")
			.WithTimeout(TimeSpan.FromMinutes(10))
			.WithAutoCompensation(true)
			.WithDefaultRetry(3, TimeSpan.FromSeconds(1), 2.0)
			.WithInputValidation(_ => Task.CompletedTask)
			.WithOutputBuilder((_, _) => Task.FromResult(new SagaTestOutput()))
			.WithErrorHandler((_, _) => { });

		// Assert
		builder.ShouldNotBeNull();
	}

	[Fact]
	public void BuildWithOutputBuilder()
	{
		// Arrange
		var builder = SagaBuilder<SagaTestInput, SagaTestOutput>.Create("saga")
			.WithOutputBuilder((_, _) => Task.FromResult(new SagaTestOutput()));

		// Act
		var orchestration = builder.Build(NullLogger<FluentSagaOrchestration<SagaTestInput, SagaTestOutput>>.Instance);

		// Assert
		orchestration.ShouldNotBeNull();
	}

	[Fact]
	public void ThrowWhenBuildingWithoutOutputBuilder()
	{
		// Arrange
		var builder = SagaBuilder<SagaTestInput, SagaTestOutput>.Create("saga");

		// Act & Assert
		Should.Throw<InvalidOperationException>(() =>
			builder.Build(NullLogger<FluentSagaOrchestration<SagaTestInput, SagaTestOutput>>.Instance));
	}

	[Fact]
	public void AddStepSuccessfully()
	{
		// Arrange
		var builder = SagaBuilder<SagaTestInput, SagaTestOutput>.Create("saga")
			.WithOutputBuilder((_, _) => Task.FromResult(new SagaTestOutput()));

		// Act
		var result = builder.AddStep<string, string>("step1", step =>
			step.ExecuteActivity("DoWork")
				.WithInput((_, _) => "input"));

		// Assert
		result.ShouldNotBeNull();
	}

	[Fact]
	public void AddConditionalStepSuccessfully()
	{
		// Arrange
		var builder = SagaBuilder<SagaTestInput, SagaTestOutput>.Create("saga")
			.WithOutputBuilder((_, _) => Task.FromResult(new SagaTestOutput()));

		// Act
		var result = builder.AddConditionalStep<string, string>(
			"conditional-step",
			(_, _) => true,
			step => step.ExecuteActivity("ConditionalWork")
						.WithInput((_, _) => "input"));

		// Assert
		result.ShouldNotBeNull();
	}

	[Fact]
	public void ThrowWhenAddStepConfigureIsNull()
	{
		// Arrange
		var builder = SagaBuilder<SagaTestInput, SagaTestOutput>.Create("saga");

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			builder.AddStep<string, string>("step1", null!));
	}

	[Fact]
	public void ThrowWhenAddConditionalStepConfigureIsNull()
	{
		// Arrange
		var builder = SagaBuilder<SagaTestInput, SagaTestOutput>.Create("saga");

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			builder.AddConditionalStep<string, string>("step1", (_, _) => true, null!));
	}

	[Fact]
	public void ThrowWhenAddParallelStepsConfigureIsNull()
	{
		// Arrange
		var builder = SagaBuilder<SagaTestInput, SagaTestOutput>.Create("saga");

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			builder.AddParallelSteps("group1", null!));
	}
}

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class SagaTestInput
{
	public string Value { get; set; } = string.Empty;
}

internal sealed class SagaTestOutput
{
	public string Result { get; set; } = string.Empty;
}
#pragma warning restore CA1812
