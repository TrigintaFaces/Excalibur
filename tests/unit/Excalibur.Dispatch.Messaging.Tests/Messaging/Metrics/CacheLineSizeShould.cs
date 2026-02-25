// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

namespace Excalibur.Dispatch.Tests.Messaging.Metrics;

/// <summary>
/// Unit tests for <see cref="CacheLineSize"/> internal class.
/// Uses reflection to test internal constant.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Metrics")]
public sealed class CacheLineSizeShould : UnitTestBase
{
	#region Constant Value Tests

	[Fact]
	public void HaveSize64()
	{
		// Arrange - Access internal constant via reflection
		var cacheLineSizeType = typeof(Dispatch.Metrics.MetricRegistry).Assembly
			.GetType("Excalibur.Dispatch.Metrics.CacheLineSize");

		cacheLineSizeType.ShouldNotBeNull();

		// Act
		var sizeField = cacheLineSizeType.GetField("Size", BindingFlags.Public | BindingFlags.Static);
		sizeField.ShouldNotBeNull();

		var sizeValue = sizeField.GetValue(null);

		// Assert
		sizeValue.ShouldBe(64);
	}

	[Fact]
	public void BeInternalClass()
	{
		// Arrange
		var cacheLineSizeType = typeof(Dispatch.Metrics.MetricRegistry).Assembly
			.GetType("Excalibur.Dispatch.Metrics.CacheLineSize");

		// Assert
		cacheLineSizeType.ShouldNotBeNull();
		cacheLineSizeType.IsNotPublic.ShouldBeTrue();
	}

	[Fact]
	public void BeStaticClass()
	{
		// Arrange
		var cacheLineSizeType = typeof(Dispatch.Metrics.MetricRegistry).Assembly
			.GetType("Excalibur.Dispatch.Metrics.CacheLineSize");

		// Assert
		cacheLineSizeType.ShouldNotBeNull();
		cacheLineSizeType.IsAbstract.ShouldBeTrue(); // Static classes are abstract + sealed
		cacheLineSizeType.IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void HaveSizeAsConst()
	{
		// Arrange
		var cacheLineSizeType = typeof(Dispatch.Metrics.MetricRegistry).Assembly
			.GetType("Excalibur.Dispatch.Metrics.CacheLineSize");

		cacheLineSizeType.ShouldNotBeNull();

		// Act
		var sizeField = cacheLineSizeType.GetField("Size", BindingFlags.Public | BindingFlags.Static);

		// Assert
		sizeField.ShouldNotBeNull();
		sizeField.IsLiteral.ShouldBeTrue(); // Const fields are literal
	}

	#endregion

	#region Platform Alignment Tests

	[Fact]
	public void AlignToTypicalX86CacheLine()
	{
		// Arrange - Access internal constant via reflection
		var cacheLineSizeType = typeof(Dispatch.Metrics.MetricRegistry).Assembly
			.GetType("Excalibur.Dispatch.Metrics.CacheLineSize");

		cacheLineSizeType.ShouldNotBeNull();

		var sizeField = cacheLineSizeType.GetField("Size", BindingFlags.Public | BindingFlags.Static);
		sizeField.ShouldNotBeNull();

		var sizeValue = (int)sizeField.GetValue(null)!;

		// Assert - 64 bytes is standard for Intel/AMD x86/x64 and most ARM64
		sizeValue.ShouldBe(64);
	}

	[Fact]
	public void BePowerOfTwo()
	{
		// Arrange
		var cacheLineSizeType = typeof(Dispatch.Metrics.MetricRegistry).Assembly
			.GetType("Excalibur.Dispatch.Metrics.CacheLineSize");

		cacheLineSizeType.ShouldNotBeNull();

		var sizeField = cacheLineSizeType.GetField("Size", BindingFlags.Public | BindingFlags.Static);
		sizeField.ShouldNotBeNull();

		var sizeValue = (int)sizeField.GetValue(null)!;

		// Assert - Cache line sizes are always powers of 2
		(sizeValue > 0 && (sizeValue & (sizeValue - 1)) == 0).ShouldBeTrue();
	}

	[Fact]
	public void BeAtLeast32Bytes()
	{
		// Arrange
		var cacheLineSizeType = typeof(Dispatch.Metrics.MetricRegistry).Assembly
			.GetType("Excalibur.Dispatch.Metrics.CacheLineSize");

		cacheLineSizeType.ShouldNotBeNull();

		var sizeField = cacheLineSizeType.GetField("Size", BindingFlags.Public | BindingFlags.Static);
		sizeField.ShouldNotBeNull();

		var sizeValue = (int)sizeField.GetValue(null)!;

		// Assert - Minimum typical cache line size
		sizeValue.ShouldBeGreaterThanOrEqualTo(32);
	}

	[Fact]
	public void BeAtMost128Bytes()
	{
		// Arrange
		var cacheLineSizeType = typeof(Dispatch.Metrics.MetricRegistry).Assembly
			.GetType("Excalibur.Dispatch.Metrics.CacheLineSize");

		cacheLineSizeType.ShouldNotBeNull();

		var sizeField = cacheLineSizeType.GetField("Size", BindingFlags.Public | BindingFlags.Static);
		sizeField.ShouldNotBeNull();

		var sizeValue = (int)sizeField.GetValue(null)!;

		// Assert - Maximum typical cache line size
		sizeValue.ShouldBeLessThanOrEqualTo(128);
	}

	#endregion
}
