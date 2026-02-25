// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions.Tests.Middleware;

/// <summary>
/// Unit tests for <see cref="DispatchMiddlewareStage"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Middleware")]
[Trait("Priority", "0")]
public sealed class DispatchMiddlewareStageShould
{
	#region Enum Value Tests

	[Fact]
	public void Start_HasExpectedValue()
	{
		// Assert
		((int)DispatchMiddlewareStage.Start).ShouldBe(0);
	}

	[Fact]
	public void RateLimiting_HasExpectedValue()
	{
		// Assert
		((int)DispatchMiddlewareStage.RateLimiting).ShouldBe(50);
	}

	[Fact]
	public void PreProcessing_HasExpectedValue()
	{
		// Assert
		((int)DispatchMiddlewareStage.PreProcessing).ShouldBe(100);
	}

	[Fact]
	public void Instrumentation_HasExpectedValue()
	{
		// Assert
		((int)DispatchMiddlewareStage.Instrumentation).ShouldBe(150);
	}

	[Fact]
	public void Authentication_HasExpectedValue()
	{
		// Assert
		((int)DispatchMiddlewareStage.Authentication).ShouldBe(175);
	}

	[Fact]
	public void Logging_HasExpectedValue()
	{
		// Assert
		((int)DispatchMiddlewareStage.Logging).ShouldBe(190);
	}

	[Fact]
	public void Validation_HasExpectedValue()
	{
		// Assert
		((int)DispatchMiddlewareStage.Validation).ShouldBe(200);
	}

	[Fact]
	public void Serialization_HasExpectedValue()
	{
		// Assert
		((int)DispatchMiddlewareStage.Serialization).ShouldBe(250);
	}

	[Fact]
	public void Authorization_HasExpectedValue()
	{
		// Assert
		((int)DispatchMiddlewareStage.Authorization).ShouldBe(300);
	}

	[Fact]
	public void Cache_HasExpectedValue()
	{
		// Assert
		((int)DispatchMiddlewareStage.Cache).ShouldBe(400);
	}

	[Fact]
	public void Optimization_HasExpectedValue()
	{
		// Assert
		((int)DispatchMiddlewareStage.Optimization).ShouldBe(450);
	}

	[Fact]
	public void Routing_HasExpectedValue()
	{
		// Assert
		((int)DispatchMiddlewareStage.Routing).ShouldBe(500);
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
	public void Error_HasExpectedValue()
	{
		// Assert
		((int)DispatchMiddlewareStage.Error).ShouldBe(800);
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

	#endregion

	#region Enum Membership Tests

	[Fact]
	public void ContainsAllExpectedValues()
	{
		// Arrange
		var values = Enum.GetValues<DispatchMiddlewareStage>();

		// Assert
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
	public void HasExactlySeventeenValues()
	{
		// Arrange
		var values = Enum.GetValues<DispatchMiddlewareStage>();

		// Assert
		values.Length.ShouldBe(17);
	}

	#endregion

	#region String Conversion Tests

	[Theory]
	[InlineData(DispatchMiddlewareStage.Start, "Start")]
	[InlineData(DispatchMiddlewareStage.RateLimiting, "RateLimiting")]
	[InlineData(DispatchMiddlewareStage.PreProcessing, "PreProcessing")]
	[InlineData(DispatchMiddlewareStage.Instrumentation, "Instrumentation")]
	[InlineData(DispatchMiddlewareStage.Authentication, "Authentication")]
	[InlineData(DispatchMiddlewareStage.Logging, "Logging")]
	[InlineData(DispatchMiddlewareStage.Validation, "Validation")]
	[InlineData(DispatchMiddlewareStage.Serialization, "Serialization")]
	[InlineData(DispatchMiddlewareStage.Authorization, "Authorization")]
	[InlineData(DispatchMiddlewareStage.Cache, "Cache")]
	[InlineData(DispatchMiddlewareStage.Optimization, "Optimization")]
	[InlineData(DispatchMiddlewareStage.Routing, "Routing")]
	[InlineData(DispatchMiddlewareStage.Processing, "Processing")]
	[InlineData(DispatchMiddlewareStage.PostProcessing, "PostProcessing")]
	[InlineData(DispatchMiddlewareStage.Error, "Error")]
	[InlineData(DispatchMiddlewareStage.ErrorHandling, "ErrorHandling")]
	[InlineData(DispatchMiddlewareStage.End, "End")]
	public void ToString_ReturnsExpectedValue(DispatchMiddlewareStage stage, string expected)
	{
		// Act & Assert
		stage.ToString().ShouldBe(expected);
	}

	#endregion

	#region Parsing Tests

	[Theory]
	[InlineData("Start", DispatchMiddlewareStage.Start)]
	[InlineData("RateLimiting", DispatchMiddlewareStage.RateLimiting)]
	[InlineData("PreProcessing", DispatchMiddlewareStage.PreProcessing)]
	[InlineData("Validation", DispatchMiddlewareStage.Validation)]
	[InlineData("Authorization", DispatchMiddlewareStage.Authorization)]
	[InlineData("Processing", DispatchMiddlewareStage.Processing)]
	[InlineData("End", DispatchMiddlewareStage.End)]
	public void Parse_WithValidString_ReturnsExpectedValue(string value, DispatchMiddlewareStage expected)
	{
		// Act
		var result = Enum.Parse<DispatchMiddlewareStage>(value);

		// Assert
		result.ShouldBe(expected);
	}

	[Theory]
	[InlineData("start")]
	[InlineData("START")]
	[InlineData("validation")]
	[InlineData("VALIDATION")]
	public void Parse_CaseInsensitive_ReturnsExpectedValue(string value)
	{
		// Act
		var result = Enum.Parse<DispatchMiddlewareStage>(value, ignoreCase: true);

		// Assert
		Enum.IsDefined(result).ShouldBeTrue();
	}

	[Fact]
	public void TryParse_WithInvalidString_ReturnsFalse()
	{
		// Act
		var success = Enum.TryParse<DispatchMiddlewareStage>("Invalid", out _);

		// Assert
		success.ShouldBeFalse();
	}

	#endregion

	#region Default Value Tests

	[Fact]
	public void DefaultValue_IsStart()
	{
		// Arrange
		DispatchMiddlewareStage stage = default;

		// Assert
		stage.ShouldBe(DispatchMiddlewareStage.Start);
	}

	#endregion

	#region Stage Ordering Tests

	[Fact]
	public void Start_IsLowestValue()
	{
		// Arrange
		var values = Enum.GetValues<DispatchMiddlewareStage>();
		var minValue = values.Min(v => (int)v);

		// Assert
		minValue.ShouldBe((int)DispatchMiddlewareStage.Start);
	}

	[Fact]
	public void End_IsHighestValue()
	{
		// Arrange
		var values = Enum.GetValues<DispatchMiddlewareStage>();
		var maxValue = values.Max(v => (int)v);

		// Assert
		maxValue.ShouldBe((int)DispatchMiddlewareStage.End);
	}

	[Fact]
	public void Stages_AreInExpectedOrder()
	{
		// Assert - Verify key stages are ordered correctly
		((int)DispatchMiddlewareStage.Start).ShouldBeLessThan((int)DispatchMiddlewareStage.RateLimiting);
		((int)DispatchMiddlewareStage.RateLimiting).ShouldBeLessThan((int)DispatchMiddlewareStage.PreProcessing);
		((int)DispatchMiddlewareStage.PreProcessing).ShouldBeLessThan((int)DispatchMiddlewareStage.Authentication);
		((int)DispatchMiddlewareStage.Authentication).ShouldBeLessThan((int)DispatchMiddlewareStage.Validation);
		((int)DispatchMiddlewareStage.Validation).ShouldBeLessThan((int)DispatchMiddlewareStage.Authorization);
		((int)DispatchMiddlewareStage.Authorization).ShouldBeLessThan((int)DispatchMiddlewareStage.Cache);
		((int)DispatchMiddlewareStage.Cache).ShouldBeLessThan((int)DispatchMiddlewareStage.Routing);
		((int)DispatchMiddlewareStage.Routing).ShouldBeLessThan((int)DispatchMiddlewareStage.Processing);
		((int)DispatchMiddlewareStage.Processing).ShouldBeLessThan((int)DispatchMiddlewareStage.PostProcessing);
		((int)DispatchMiddlewareStage.PostProcessing).ShouldBeLessThan((int)DispatchMiddlewareStage.Error);
		((int)DispatchMiddlewareStage.Error).ShouldBeLessThan((int)DispatchMiddlewareStage.End);
	}

	[Fact]
	public void ErrorHandling_IsAfterError()
	{
		// Assert - ErrorHandling is an alias that comes right after Error
		((int)DispatchMiddlewareStage.ErrorHandling).ShouldBe((int)DispatchMiddlewareStage.Error + 1);
	}

	#endregion
}
