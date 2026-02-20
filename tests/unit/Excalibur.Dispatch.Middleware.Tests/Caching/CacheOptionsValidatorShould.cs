// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Caching;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Middleware.Tests.Caching;

/// <summary>
/// Unit tests for <see cref="CacheOptionsValidator"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class CacheOptionsValidatorShould : UnitTestBase
{
	private readonly CacheOptionsValidator _validator = new();

	[Fact]
	public void Validate_ThrowsArgumentNullException_WhenOptionsIsNull()
	{
		Should.Throw<ArgumentNullException>(() => _validator.Validate("test", null!));
	}

	[Fact]
	public void Validate_ReturnsSuccess_WhenOptionsAreValid()
	{
		// Arrange
		var options = new CacheOptions
		{
			Behavior = new CacheBehaviorOptions
			{
				DefaultExpiration = TimeSpan.FromMinutes(10),
				CacheTimeout = TimeSpan.FromMilliseconds(200),
				JitterRatio = 0.10,
			},
		};

		// Act
		var result = _validator.Validate("test", options);

		// Assert
		result.ShouldBe(ValidateOptionsResult.Success);
	}

	[Fact]
	public void Validate_Fails_WhenDefaultExpirationIsZero()
	{
		// Arrange
		var options = new CacheOptions
		{
			Behavior = new CacheBehaviorOptions
			{
				DefaultExpiration = TimeSpan.Zero,
				CacheTimeout = TimeSpan.FromMilliseconds(200),
			},
		};

		// Act
		var result = _validator.Validate("test", options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("DefaultExpiration");
	}

	[Fact]
	public void Validate_Fails_WhenDefaultExpirationIsNegative()
	{
		// Arrange
		var options = new CacheOptions
		{
			Behavior = new CacheBehaviorOptions
			{
				DefaultExpiration = TimeSpan.FromMinutes(-1),
				CacheTimeout = TimeSpan.FromMilliseconds(200),
			},
		};

		// Act
		var result = _validator.Validate("test", options);

		// Assert
		result.Failed.ShouldBeTrue();
	}

	[Fact]
	public void Validate_Fails_WhenCacheTimeoutIsZero()
	{
		// Arrange
		var options = new CacheOptions
		{
			Behavior = new CacheBehaviorOptions
			{
				DefaultExpiration = TimeSpan.FromMinutes(10),
				CacheTimeout = TimeSpan.Zero,
			},
		};

		// Act
		var result = _validator.Validate("test", options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("CacheTimeout");
	}

	[Fact]
	public void Validate_Fails_WhenJitterRatioIsNegative()
	{
		// Arrange
		var options = new CacheOptions
		{
			Behavior = new CacheBehaviorOptions
			{
				DefaultExpiration = TimeSpan.FromMinutes(10),
				CacheTimeout = TimeSpan.FromMilliseconds(200),
				JitterRatio = -0.1,
			},
		};

		// Act
		var result = _validator.Validate("test", options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("JitterRatio");
	}

	[Fact]
	public void Validate_Fails_WhenJitterRatioExceedsOne()
	{
		// Arrange
		var options = new CacheOptions
		{
			Behavior = new CacheBehaviorOptions
			{
				DefaultExpiration = TimeSpan.FromMinutes(10),
				CacheTimeout = TimeSpan.FromMilliseconds(200),
				JitterRatio = 1.5,
			},
		};

		// Act
		var result = _validator.Validate("test", options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("JitterRatio");
	}

	[Fact]
	public void Validate_Fails_WhenCacheTimeoutExceedsDefaultExpiration()
	{
		// Arrange
		var options = new CacheOptions
		{
			Behavior = new CacheBehaviorOptions
			{
				DefaultExpiration = TimeSpan.FromMilliseconds(100),
				CacheTimeout = TimeSpan.FromMinutes(10),
				JitterRatio = 0.10,
			},
		};

		// Act
		var result = _validator.Validate("test", options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("CacheTimeout");
		result.FailureMessage.ShouldContain("DefaultExpiration");
	}

	[Fact]
	public void Validate_Fails_WhenCacheTimeoutEqualsDefaultExpiration()
	{
		// Arrange
		var sameTime = TimeSpan.FromMinutes(5);
		var options = new CacheOptions
		{
			Behavior = new CacheBehaviorOptions
			{
				DefaultExpiration = sameTime,
				CacheTimeout = sameTime,
				JitterRatio = 0.10,
			},
		};

		// Act
		var result = _validator.Validate("test", options);

		// Assert
		result.Failed.ShouldBeTrue();
	}

	[Theory]
	[InlineData(0.0)]
	[InlineData(0.5)]
	[InlineData(1.0)]
	public void Validate_Succeeds_WhenJitterRatioIsInValidRange(double ratio)
	{
		// Arrange
		var options = new CacheOptions
		{
			Behavior = new CacheBehaviorOptions
			{
				DefaultExpiration = TimeSpan.FromMinutes(10),
				CacheTimeout = TimeSpan.FromMilliseconds(200),
				JitterRatio = ratio,
			},
		};

		// Act
		var result = _validator.Validate("test", options);

		// Assert
		result.ShouldBe(ValidateOptionsResult.Success);
	}
}
