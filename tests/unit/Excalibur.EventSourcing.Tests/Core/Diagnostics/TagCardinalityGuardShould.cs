// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.EventSourcing.Diagnostics;

namespace Excalibur.EventSourcing.Tests.Core.Diagnostics;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class TagCardinalityGuardShould
{
	private static readonly Type? GuardType = typeof(TelemetryEventStore).Assembly
		.GetType("Excalibur.EventSourcing.Diagnostics.TagCardinalityGuard");

	private static object CreateGuard(int maxCardinality = 128, string overflowValue = "__other__")
	{
		GuardType.ShouldNotBeNull("TagCardinalityGuard type should exist in assembly");
		var instance = Activator.CreateInstance(GuardType, BindingFlags.Instance | BindingFlags.NonPublic, null,
			[maxCardinality, overflowValue], null);
		instance.ShouldNotBeNull();
		return instance;
	}

	private static string InvokeGuard(object guard, string? tagValue)
	{
		var method = GuardType!.GetMethod("Guard", BindingFlags.Instance | BindingFlags.NonPublic);
		method.ShouldNotBeNull();
		var result = method.Invoke(guard, [tagValue]);
		result.ShouldNotBeNull();
		return (string)result;
	}

	private static string GetOverflowValue(object guard)
	{
		var prop = GuardType!.GetProperty("OverflowValue", BindingFlags.Instance | BindingFlags.NonPublic);
		prop.ShouldNotBeNull();
		var result = prop.GetValue(guard);
		result.ShouldNotBeNull();
		return (string)result;
	}

	[Fact]
	public void ExistInAssembly()
	{
		GuardType.ShouldNotBeNull();
	}

	[Fact]
	public void ReturnOriginalValueWhenWithinLimit()
	{
		// Arrange
		var guard = CreateGuard(maxCardinality: 10);

		// Act
		var result = InvokeGuard(guard, "value-1");

		// Assert
		result.ShouldBe("value-1");
	}

	[Fact]
	public void ReturnOverflowWhenExceedingLimit()
	{
		// Arrange
		var guard = CreateGuard(maxCardinality: 2);
		InvokeGuard(guard, "value-1");
		InvokeGuard(guard, "value-2");

		// Act
		var result = InvokeGuard(guard, "value-3");

		// Assert
		result.ShouldBe("__other__");
	}

	[Fact]
	public void ReturnOverflowForNullValue()
	{
		// Arrange
		var guard = CreateGuard();

		// Act
		var result = InvokeGuard(guard, null);

		// Assert
		result.ShouldBe("__other__");
	}

	[Fact]
	public void ReturnKnownValueAfterTracking()
	{
		// Arrange
		var guard = CreateGuard(maxCardinality: 3);
		InvokeGuard(guard, "value-1");

		// Act - access same value again
		var result = InvokeGuard(guard, "value-1");

		// Assert
		result.ShouldBe("value-1");
	}

	[Fact]
	public void UseCustomOverflowValue()
	{
		// Arrange
		var guard = CreateGuard(maxCardinality: 1, overflowValue: "__overflow__");

		InvokeGuard(guard, "value-1");

		// Act
		var result = InvokeGuard(guard, "value-2");

		// Assert
		result.ShouldBe("__overflow__");
	}

	[Fact]
	public void ExposeOverflowValueProperty()
	{
		// Arrange
		var guard = CreateGuard(overflowValue: "__custom__");

		// Assert
		GetOverflowValue(guard).ShouldBe("__custom__");
	}

	[Fact]
	public void HaveCorrectDefaultOverflowConstant()
	{
		var field = GuardType!.GetField("DefaultOverflowValue", BindingFlags.Static | BindingFlags.NonPublic);
		field.ShouldNotBeNull();
		var value = field.GetValue(null);
		value.ShouldNotBeNull();
		((string)value).ShouldBe("__other__");
	}
}
