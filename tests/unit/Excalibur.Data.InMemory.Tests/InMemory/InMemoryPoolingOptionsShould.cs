// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.InMemory;

namespace Excalibur.Data.Tests.InMemory;

/// <summary>
/// Unit tests for <see cref="InMemoryPoolingOptions"/>.
/// Verifies defaults, property assignment, and DataAnnotations range constraints.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InMemoryPoolingOptionsShould : UnitTestBase
{
	[Fact]
	public void HaveExpectedDefaults()
	{
		// Arrange & Act
		var options = new InMemoryPoolingOptions();

		// Assert
		options.EnableConnectionPooling.ShouldBeFalse();
		options.MaxPoolSize.ShouldBe(100);
		options.MinPoolSize.ShouldBe(0);
		options.ConnectionTimeout.ShouldBe(30);
		options.CommandTimeout.ShouldBe(30);
	}

	[Fact]
	public void AllowCustomPoolSizes()
	{
		// Arrange & Act
		var options = new InMemoryPoolingOptions
		{
			MaxPoolSize = 200,
			MinPoolSize = 10
		};

		// Assert
		options.MaxPoolSize.ShouldBe(200);
		options.MinPoolSize.ShouldBe(10);
	}

	[Fact]
	public void AllowCustomTimeouts()
	{
		// Arrange & Act
		var options = new InMemoryPoolingOptions
		{
			ConnectionTimeout = 60,
			CommandTimeout = 120
		};

		// Assert
		options.ConnectionTimeout.ShouldBe(60);
		options.CommandTimeout.ShouldBe(120);
	}

	[Fact]
	public void AllowEnablingConnectionPooling()
	{
		// Arrange & Act
		var options = new InMemoryPoolingOptions
		{
			EnableConnectionPooling = true
		};

		// Assert
		options.EnableConnectionPooling.ShouldBeTrue();
	}

	[Fact]
	public void HaveRangeAttributeOnMaxPoolSize()
	{
		// Assert -- [Range(1, int.MaxValue)]
		var prop = typeof(InMemoryPoolingOptions).GetProperty(nameof(InMemoryPoolingOptions.MaxPoolSize))!;
		var attr = prop.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.RangeAttribute), false);
		attr.ShouldNotBeEmpty();
		var range = (System.ComponentModel.DataAnnotations.RangeAttribute)attr[0];
		range.Minimum.ShouldBe(1);
	}

	[Fact]
	public void HaveRangeAttributeOnMinPoolSize()
	{
		// Assert -- [Range(0, int.MaxValue)]
		var prop = typeof(InMemoryPoolingOptions).GetProperty(nameof(InMemoryPoolingOptions.MinPoolSize))!;
		var attr = prop.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.RangeAttribute), false);
		attr.ShouldNotBeEmpty();
		var range = (System.ComponentModel.DataAnnotations.RangeAttribute)attr[0];
		range.Minimum.ShouldBe(0);
	}

	[Fact]
	public void HaveRangeAttributeOnConnectionTimeout()
	{
		// Assert -- [Range(1, int.MaxValue)]
		var prop = typeof(InMemoryPoolingOptions).GetProperty(nameof(InMemoryPoolingOptions.ConnectionTimeout))!;
		var attr = prop.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.RangeAttribute), false);
		attr.ShouldNotBeEmpty();
		var range = (System.ComponentModel.DataAnnotations.RangeAttribute)attr[0];
		range.Minimum.ShouldBe(1);
	}

	[Fact]
	public void HaveRangeAttributeOnCommandTimeout()
	{
		// Assert -- [Range(1, int.MaxValue)]
		var prop = typeof(InMemoryPoolingOptions).GetProperty(nameof(InMemoryPoolingOptions.CommandTimeout))!;
		var attr = prop.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.RangeAttribute), false);
		attr.ShouldNotBeEmpty();
		var range = (System.ComponentModel.DataAnnotations.RangeAttribute)attr[0];
		range.Minimum.ShouldBe(1);
	}
}
