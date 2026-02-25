// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Caching;

namespace Excalibur.Dispatch.Caching.Tests;

/// <summary>
/// Unit tests for <see cref="CachedValue"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Caching")]
public sealed class CachedValueShould
{
	#region Default Value Tests

	[Fact]
	public void HaveNullValue_ByDefault()
	{
		// Arrange & Act
		var cachedValue = new CachedValue();

		// Assert
		cachedValue.Value.ShouldBeNull();
	}

	[Fact]
	public void HaveFalseShouldCache_ByDefault()
	{
		// Arrange & Act
		var cachedValue = new CachedValue();

		// Assert
		cachedValue.ShouldCache.ShouldBeFalse();
	}

	[Fact]
	public void HaveFalseHasExecuted_ByDefault()
	{
		// Arrange & Act
		var cachedValue = new CachedValue();

		// Assert
		cachedValue.HasExecuted.ShouldBeFalse();
	}

	[Fact]
	public void HaveNullTypeName_ByDefault()
	{
		// Arrange & Act
		var cachedValue = new CachedValue();

		// Assert
		cachedValue.TypeName.ShouldBeNull();
	}

	#endregion

	#region Property Initialization Tests

	[Fact]
	public void AllowSettingValue_ViaInit()
	{
		// Arrange & Act
		var cachedValue = new CachedValue { Value = "test value" };

		// Assert
		cachedValue.Value.ShouldBe("test value");
	}

	[Fact]
	public void AllowSettingValueToComplexType_ViaInit()
	{
		// Arrange
		var complexValue = new TestDto { Id = 42, Name = "Test" };

		// Act
		var cachedValue = new CachedValue { Value = complexValue };

		// Assert
		cachedValue.Value.ShouldBe(complexValue);
	}

	[Fact]
	public void AllowSettingShouldCache_ViaInit()
	{
		// Arrange & Act
		var cachedValue = new CachedValue { ShouldCache = true };

		// Assert
		cachedValue.ShouldCache.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingHasExecuted_ViaInit()
	{
		// Arrange & Act
		var cachedValue = new CachedValue { HasExecuted = true };

		// Assert
		cachedValue.HasExecuted.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingTypeName_ViaInit()
	{
		// Arrange & Act
		var cachedValue = new CachedValue { TypeName = "System.String, System.Private.CoreLib" };

		// Assert
		cachedValue.TypeName.ShouldBe("System.String, System.Private.CoreLib");
	}

	#endregion

	#region Complete Initialization Tests

	[Fact]
	public void AllowSettingAllProperties_ViaInit()
	{
		// Arrange
		var value = new TestDto { Id = 1, Name = "Complete" };

		// Act
		var cachedValue = new CachedValue
		{
			Value = value,
			ShouldCache = true,
			HasExecuted = true,
			TypeName = typeof(TestDto).AssemblyQualifiedName,
		};

		// Assert
		cachedValue.Value.ShouldBe(value);
		cachedValue.ShouldCache.ShouldBeTrue();
		cachedValue.HasExecuted.ShouldBeTrue();
		cachedValue.TypeName.ShouldNotBeNull();
		cachedValue.TypeName.ShouldContain("TestDto");
	}

	[Fact]
	public void HandleNullValue_WithTypeName()
	{
		// Arrange & Act
		var cachedValue = new CachedValue
		{
			Value = null,
			ShouldCache = true,
			HasExecuted = true,
			TypeName = "System.Object, System.Private.CoreLib",
		};

		// Assert
		cachedValue.Value.ShouldBeNull();
		cachedValue.ShouldCache.ShouldBeTrue();
		cachedValue.HasExecuted.ShouldBeTrue();
		cachedValue.TypeName.ShouldNotBeNull();
	}

	#endregion

	#region Value Type Support Tests

	[Fact]
	public void SupportIntegerValue()
	{
		// Arrange & Act
		var cachedValue = new CachedValue { Value = 42 };

		// Assert
		cachedValue.Value.ShouldBe(42);
	}

	[Fact]
	public void SupportBooleanValue()
	{
		// Arrange & Act
		var cachedValue = new CachedValue { Value = true };

		// Assert
		cachedValue.Value.ShouldBe(true);
	}

	[Fact]
	public void SupportDecimalValue()
	{
		// Arrange & Act
		var cachedValue = new CachedValue { Value = 123.456m };

		// Assert
		cachedValue.Value.ShouldBe(123.456m);
	}

	[Fact]
	public void SupportDateTimeValue()
	{
		// Arrange
		var dateTime = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);

		// Act
		var cachedValue = new CachedValue { Value = dateTime };

		// Assert
		cachedValue.Value.ShouldBe(dateTime);
	}

	[Fact]
	public void SupportArrayValue()
	{
		// Arrange
		var array = new[] { 1, 2, 3, 4, 5 };

		// Act
		var cachedValue = new CachedValue { Value = array };

		// Assert
		cachedValue.Value.ShouldBe(array);
	}

	[Fact]
	public void SupportListValue()
	{
		// Arrange
		var list = new List<string> { "a", "b", "c" };

		// Act
		var cachedValue = new CachedValue { Value = list };

		// Assert
		cachedValue.Value.ShouldBe(list);
	}

	#endregion

	#region Test Helpers

	private sealed class TestDto
	{
		public int Id { get; set; }
		public string? Name { get; set; }
	}

	#endregion
}
