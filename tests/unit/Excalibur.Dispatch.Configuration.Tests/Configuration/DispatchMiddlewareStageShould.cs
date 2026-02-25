// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Tests.Configuration;

/// <summary>
/// Unit tests for <see cref="DispatchMiddlewareStage"/> enum (canonical version from Abstractions).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DispatchMiddlewareStageShould
{
	[Fact]
	public void HaveExpectedDistinctValues()
	{
		// Arrange
		var values = Enum.GetValues<DispatchMiddlewareStage>();

		// Assert - canonical enum has 17 members (including ErrorHandling alias)
		values.Length.ShouldBe(17);
		values.ShouldContain(DispatchMiddlewareStage.Start);
		values.ShouldContain(DispatchMiddlewareStage.RateLimiting);
		values.ShouldContain(DispatchMiddlewareStage.PreProcessing);
		values.ShouldContain(DispatchMiddlewareStage.Instrumentation);
		values.ShouldContain(DispatchMiddlewareStage.Authentication);
		values.ShouldContain(DispatchMiddlewareStage.Logging);
		values.ShouldContain(DispatchMiddlewareStage.Validation);
		values.ShouldContain(DispatchMiddlewareStage.Serialization);
		values.ShouldContain(DispatchMiddlewareStage.Authorization);
		values.ShouldContain(DispatchMiddlewareStage.Cache);
		values.ShouldContain(DispatchMiddlewareStage.Optimization);
		values.ShouldContain(DispatchMiddlewareStage.Routing);
		values.ShouldContain(DispatchMiddlewareStage.Processing);
		values.ShouldContain(DispatchMiddlewareStage.PostProcessing);
		values.ShouldContain(DispatchMiddlewareStage.Error);
		values.ShouldContain(DispatchMiddlewareStage.ErrorHandling);
		values.ShouldContain(DispatchMiddlewareStage.End);
	}

	[Fact]
	public void Start_HasExpectedValue()
	{
		// Assert
		((int)DispatchMiddlewareStage.Start).ShouldBe(0);
	}

	[Fact]
	public void PreProcessing_HasExpectedValue()
	{
		// Assert
		((int)DispatchMiddlewareStage.PreProcessing).ShouldBe(100);
	}

	[Fact]
	public void Authorization_HasExpectedValue()
	{
		// Assert
		((int)DispatchMiddlewareStage.Authorization).ShouldBe(300);
	}

	[Fact]
	public void Validation_HasExpectedValue()
	{
		// Assert
		((int)DispatchMiddlewareStage.Validation).ShouldBe(200);
	}

	[Fact]
	public void Processing_HasExpectedValue()
	{
		// Assert
		((int)DispatchMiddlewareStage.Processing).ShouldBe(600);
	}

	[Fact]
	public void PostProcessing_HasExpectedValue()
	{
		// Assert
		((int)DispatchMiddlewareStage.PostProcessing).ShouldBe(700);
	}

	[Fact]
	public void ErrorHandling_HasExpectedValue()
	{
		// Assert
		((int)DispatchMiddlewareStage.ErrorHandling).ShouldBe(801);
	}

	[Fact]
	public void End_HasExpectedValue()
	{
		// Assert
		((int)DispatchMiddlewareStage.End).ShouldBe(1000);
	}

	[Fact]
	public void Start_IsDefaultValue()
	{
		// Arrange
		DispatchMiddlewareStage defaultStage = default;

		// Assert
		defaultStage.ShouldBe(DispatchMiddlewareStage.Start);
	}

	[Theory]
	[InlineData(DispatchMiddlewareStage.Start)]
	[InlineData(DispatchMiddlewareStage.RateLimiting)]
	[InlineData(DispatchMiddlewareStage.PreProcessing)]
	[InlineData(DispatchMiddlewareStage.Authentication)]
	[InlineData(DispatchMiddlewareStage.Validation)]
	[InlineData(DispatchMiddlewareStage.Authorization)]
	[InlineData(DispatchMiddlewareStage.Processing)]
	[InlineData(DispatchMiddlewareStage.PostProcessing)]
	[InlineData(DispatchMiddlewareStage.Error)]
	[InlineData(DispatchMiddlewareStage.ErrorHandling)]
	[InlineData(DispatchMiddlewareStage.End)]
	public void BeDefinedForAllValues(DispatchMiddlewareStage stage)
	{
		// Assert
		Enum.IsDefined(stage).ShouldBeTrue();
	}

	[Fact]
	public void HaveStagesOrderedByExecutionSequence()
	{
		// Assert - Stages should progress in execution order
		(DispatchMiddlewareStage.Start < DispatchMiddlewareStage.RateLimiting).ShouldBeTrue();
		(DispatchMiddlewareStage.RateLimiting < DispatchMiddlewareStage.PreProcessing).ShouldBeTrue();
		(DispatchMiddlewareStage.PreProcessing < DispatchMiddlewareStage.Validation).ShouldBeTrue();
		(DispatchMiddlewareStage.Validation < DispatchMiddlewareStage.Authorization).ShouldBeTrue();
		(DispatchMiddlewareStage.Authorization < DispatchMiddlewareStage.Processing).ShouldBeTrue();
		(DispatchMiddlewareStage.Processing < DispatchMiddlewareStage.PostProcessing).ShouldBeTrue();
		(DispatchMiddlewareStage.PostProcessing < DispatchMiddlewareStage.Error).ShouldBeTrue();
		(DispatchMiddlewareStage.Error < DispatchMiddlewareStage.End).ShouldBeTrue();
	}
}
