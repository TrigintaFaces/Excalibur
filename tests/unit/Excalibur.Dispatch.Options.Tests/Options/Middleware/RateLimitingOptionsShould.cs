// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Middleware;
using Excalibur.Dispatch.Options.Middleware;

namespace Excalibur.Dispatch.Tests.Options.Middleware;

/// <summary>
/// Unit tests for <see cref="RateLimitingOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class RateLimitingOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_Enabled_IsTrue()
	{
		// Arrange & Act
		var options = new RateLimitingOptions();

		// Assert
		options.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void Default_EnablePerTenantLimiting_IsTrue()
	{
		// Arrange & Act
		var options = new RateLimitingOptions();

		// Assert
		options.EnablePerTenantLimiting.ShouldBeTrue();
	}

	[Fact]
	public void Default_DefaultLimit_IsNotNull()
	{
		// Arrange & Act
		var options = new RateLimitingOptions();

		// Assert
		_ = options.DefaultLimit.ShouldNotBeNull();
	}

	[Fact]
	public void Default_DefaultLimit_HasTokenBucketAlgorithm()
	{
		// Arrange & Act
		var options = new RateLimitingOptions();

		// Assert
		options.DefaultLimit.Algorithm.ShouldBe(RateLimitAlgorithm.TokenBucket);
	}

	[Fact]
	public void Default_DefaultLimit_HasTokenLimitOf100()
	{
		// Arrange & Act
		var options = new RateLimitingOptions();

		// Assert
		options.DefaultLimit.TokenLimit.ShouldBe(100);
	}

	[Fact]
	public void Default_DefaultLimit_HasOneSecondReplenishmentPeriod()
	{
		// Arrange & Act
		var options = new RateLimitingOptions();

		// Assert
		options.DefaultLimit.ReplenishmentPeriod.ShouldBe(TimeSpan.FromSeconds(1));
	}

	[Fact]
	public void Default_DefaultLimit_HasTokensPerPeriodOf100()
	{
		// Arrange & Act
		var options = new RateLimitingOptions();

		// Assert
		options.DefaultLimit.TokensPerPeriod.ShouldBe(100);
	}

	[Fact]
	public void Default_GlobalLimit_IsNotNull()
	{
		// Arrange & Act
		var options = new RateLimitingOptions();

		// Assert
		_ = options.GlobalLimit.ShouldNotBeNull();
	}

	[Fact]
	public void Default_GlobalLimit_HasTokenLimitOf1000()
	{
		// Arrange & Act
		var options = new RateLimitingOptions();

		// Assert
		options.GlobalLimit.TokenLimit.ShouldBe(1000);
	}

	[Fact]
	public void Default_GlobalLimit_HasTokensPerPeriodOf1000()
	{
		// Arrange & Act
		var options = new RateLimitingOptions();

		// Assert
		options.GlobalLimit.TokensPerPeriod.ShouldBe(1000);
	}

	[Fact]
	public void Default_MessageTypeLimits_IsEmpty()
	{
		// Arrange & Act
		var options = new RateLimitingOptions();

		// Assert
		_ = options.MessageTypeLimits.ShouldNotBeNull();
		options.MessageTypeLimits.ShouldBeEmpty();
	}

	[Fact]
	public void Default_BypassRateLimitingForTypes_IsNull()
	{
		// Arrange & Act
		var options = new RateLimitingOptions();

		// Assert
		options.BypassRateLimitingForTypes.ShouldBeNull();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void Enabled_CanBeSet()
	{
		// Arrange
		var options = new RateLimitingOptions();

		// Act
		options.Enabled = false;

		// Assert
		options.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void EnablePerTenantLimiting_CanBeSet()
	{
		// Arrange
		var options = new RateLimitingOptions();

		// Act
		options.EnablePerTenantLimiting = false;

		// Assert
		options.EnablePerTenantLimiting.ShouldBeFalse();
	}

	[Fact]
	public void DefaultLimit_CanBeSet()
	{
		// Arrange
		var options = new RateLimitingOptions();
		var newLimit = new RateLimitConfiguration
		{
			Algorithm = RateLimitAlgorithm.SlidingWindow,
			TokenLimit = 50,
		};

		// Act
		options.DefaultLimit = newLimit;

		// Assert
		options.DefaultLimit.ShouldBe(newLimit);
	}

	[Fact]
	public void GlobalLimit_CanBeSet()
	{
		// Arrange
		var options = new RateLimitingOptions();
		var newLimit = new RateLimitConfiguration
		{
			Algorithm = RateLimitAlgorithm.FixedWindow,
			TokenLimit = 500,
		};

		// Act
		options.GlobalLimit = newLimit;

		// Assert
		options.GlobalLimit.ShouldBe(newLimit);
	}

	[Fact]
	public void BypassRateLimitingForTypes_CanBeSet()
	{
		// Arrange
		var options = new RateLimitingOptions();
		var types = new[] { "System.String", "System.Int32" };

		// Act
		options.BypassRateLimitingForTypes = types;

		// Assert
		options.BypassRateLimitingForTypes.ShouldBe(types);
	}

	[Fact]
	public void MessageTypeLimits_CanAddEntries()
	{
		// Arrange
		var options = new RateLimitingOptions();
		var config = new RateLimitConfiguration { TokenLimit = 10 };

		// Act
		options.MessageTypeLimits["OrderCommand"] = config;

		// Assert
		options.MessageTypeLimits.Count.ShouldBe(1);
		options.MessageTypeLimits["OrderCommand"].ShouldBe(config);
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Arrange
		var defaultLimit = new RateLimitConfiguration { TokenLimit = 200 };
		var globalLimit = new RateLimitConfiguration { TokenLimit = 2000 };
		var bypassTypes = new[] { "SystemMessage" };

		// Act
		var options = new RateLimitingOptions
		{
			Enabled = false,
			EnablePerTenantLimiting = false,
			DefaultLimit = defaultLimit,
			GlobalLimit = globalLimit,
			BypassRateLimitingForTypes = bypassTypes,
		};

		// Assert
		options.Enabled.ShouldBeFalse();
		options.EnablePerTenantLimiting.ShouldBeFalse();
		options.DefaultLimit.ShouldBe(defaultLimit);
		options.GlobalLimit.ShouldBe(globalLimit);
		options.BypassRateLimitingForTypes.ShouldBe(bypassTypes);
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Options_ForHighVolumeApi_HasHigherLimits()
	{
		// Act
		var options = new RateLimitingOptions
		{
			DefaultLimit = new RateLimitConfiguration
			{
				Algorithm = RateLimitAlgorithm.TokenBucket,
				TokenLimit = 500,
				ReplenishmentPeriod = TimeSpan.FromSeconds(1),
				TokensPerPeriod = 500,
			},
			GlobalLimit = new RateLimitConfiguration
			{
				TokenLimit = 5000,
				TokensPerPeriod = 5000,
			},
		};

		// Assert
		options.DefaultLimit.TokenLimit.ShouldBeGreaterThan(100);
		options.GlobalLimit.TokenLimit.ShouldBeGreaterThan(1000);
	}

	[Fact]
	public void Options_WithMessageTypeLimits_HasPerTypeConfiguration()
	{
		// Act
		var options = new RateLimitingOptions();
		options.MessageTypeLimits["OrderCommand"] = new RateLimitConfiguration { TokenLimit = 10 };
		options.MessageTypeLimits["QueryCommand"] = new RateLimitConfiguration { TokenLimit = 100 };

		// Assert
		options.MessageTypeLimits.Count.ShouldBe(2);
		options.MessageTypeLimits["OrderCommand"].TokenLimit.ShouldBe(10);
		options.MessageTypeLimits["QueryCommand"].TokenLimit.ShouldBe(100);
	}

	#endregion
}
