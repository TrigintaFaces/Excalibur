// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

namespace Excalibur.LeaderElection.Tests.Diagnostics;

/// <summary>
/// Tests for the internal TagCardinalityGuard class via reflection.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class TagCardinalityGuardShould
{
	private static readonly Type GuardType = typeof(Excalibur.LeaderElection.Watch.LeaderWatchOptions).Assembly
		.GetType("Excalibur.LeaderElection.Diagnostics.TagCardinalityGuard")!;

	private static readonly MethodInfo GuardMethod = GuardType.GetMethod(
		"Guard", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)!;

	[Fact]
	public void ReturnValueWhenWithinCardinality()
	{
		// Arrange
		var guard = CreateGuard(maxCardinality: 10);

		// Act
		var result = InvokeGuard(guard, "value1");

		// Assert
		result.ShouldBe("value1");
	}

	[Fact]
	public void ReturnOverflowWhenExceedingCardinality()
	{
		// Arrange
		var guard = CreateGuard(maxCardinality: 3);
		InvokeGuard(guard, "v1");
		InvokeGuard(guard, "v2");
		InvokeGuard(guard, "v3");

		// Act
		var result = InvokeGuard(guard, "v4");

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
	public void ReturnExistingValueEvenWhenAtCapacity()
	{
		// Arrange
		var guard = CreateGuard(maxCardinality: 2);
		InvokeGuard(guard, "v1");
		InvokeGuard(guard, "v2");

		// Act -- v1 already tracked, should return it even at capacity
		var result = InvokeGuard(guard, "v1");

		// Assert
		result.ShouldBe("v1");
	}

	[Fact]
	public void UseCustomOverflowValue()
	{
		// Arrange
		var guard = CreateGuard(maxCardinality: 1, overflowValue: "OVERFLOW");
		InvokeGuard(guard, "v1");

		// Act
		var result = InvokeGuard(guard, "v2");

		// Assert
		result.ShouldBe("OVERFLOW");
	}

	[Fact]
	public void HandleMultipleDistinctValues()
	{
		// Arrange
		var guard = CreateGuard(maxCardinality: 100);

		// Act & Assert
		for (var i = 0; i < 50; i++)
		{
			var result = InvokeGuard(guard, $"value_{i}");
			result.ShouldBe($"value_{i}");
		}
	}

	[Fact]
	public void BeThreadSafe()
	{
		// Arrange
		var guard = CreateGuard(maxCardinality: 1000);
		var results = new System.Collections.Concurrent.ConcurrentBag<string>();

		// Act
		Parallel.For(0, 500, i =>
		{
			var result = InvokeGuard(guard, $"thread_{i}");
			results.Add(result);
		});

		// Assert -- No exceptions + all results present
		results.Count.ShouldBe(500);
	}

	private static object CreateGuard(
		int maxCardinality = 100,
		string overflowValue = "__other__")
	{
		var instance = Activator.CreateInstance(
			GuardType,
			BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
			binder: null,
			args: [maxCardinality, overflowValue],
			culture: null);
		return instance!;
	}

	private static string InvokeGuard(object guard, string? value)
	{
		return (string)GuardMethod.Invoke(guard, [value])!;
	}
}
