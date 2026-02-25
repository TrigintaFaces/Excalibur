// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Caching;

namespace Excalibur.Dispatch.Caching.Tests;

/// <summary>
/// Unit tests for <see cref="CacheOptionsValidator"/>.
/// Sprint 563 S563.56: IValidateOptions validator tests.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Caching")]
public sealed class CacheOptionsValidatorShould
{
	private readonly CacheOptionsValidator _validator = new();

	[Fact]
	public void SucceedForDefaultOptions()
	{
		// Arrange
		var options = new CacheOptions();

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void ThrowArgumentNullException_WhenOptionsIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => _validator.Validate(null, null!));
	}

	[Fact]
	public void FailWhenDefaultExpirationIsZero()
	{
		// Arrange
		var options = new CacheOptions();
		options.Behavior.DefaultExpiration = TimeSpan.Zero;

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(CacheBehaviorOptions.DefaultExpiration));
	}

	[Fact]
	public void FailWhenDefaultExpirationIsNegative()
	{
		// Arrange
		var options = new CacheOptions();
		options.Behavior.DefaultExpiration = TimeSpan.FromSeconds(-1);

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(CacheBehaviorOptions.DefaultExpiration));
	}

	[Fact]
	public void FailWhenCacheTimeoutIsZero()
	{
		// Arrange
		var options = new CacheOptions();
		options.Behavior.CacheTimeout = TimeSpan.Zero;

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(CacheBehaviorOptions.CacheTimeout));
	}

	[Fact]
	public void FailWhenJitterRatioExceedsOne()
	{
		// Arrange
		var options = new CacheOptions();
		options.Behavior.JitterRatio = 1.5;

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(CacheBehaviorOptions.JitterRatio));
	}

	[Fact]
	public void FailWhenJitterRatioIsNegative()
	{
		// Arrange
		var options = new CacheOptions();
		options.Behavior.JitterRatio = -0.1;

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(CacheBehaviorOptions.JitterRatio));
	}

	[Fact]
	public void FailWhenCacheTimeoutExceedsDefaultExpiration()
	{
		// Arrange
		var options = new CacheOptions();
		options.Behavior.CacheTimeout = TimeSpan.FromMinutes(20);
		options.Behavior.DefaultExpiration = TimeSpan.FromMinutes(10);

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(CacheBehaviorOptions.CacheTimeout));
		result.FailureMessage.ShouldContain(nameof(CacheBehaviorOptions.DefaultExpiration));
	}

	[Fact]
	public void FailWhenCacheTimeoutEqualsDefaultExpiration()
	{
		// Arrange
		var options = new CacheOptions();
		options.Behavior.CacheTimeout = TimeSpan.FromMinutes(10);
		options.Behavior.DefaultExpiration = TimeSpan.FromMinutes(10);

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(CacheBehaviorOptions.CacheTimeout));
		result.FailureMessage.ShouldContain(nameof(CacheBehaviorOptions.DefaultExpiration));
	}

	[Fact]
	public void SucceedWithValidCustomOptions()
	{
		// Arrange
		var options = new CacheOptions();
		options.Behavior.DefaultExpiration = TimeSpan.FromMinutes(30);
		options.Behavior.CacheTimeout = TimeSpan.FromMilliseconds(500);
		options.Behavior.JitterRatio = 0.15;

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void SucceedWithZeroJitterRatio()
	{
		// Arrange - zero jitter ratio is valid (disables jitter)
		var options = new CacheOptions();
		options.Behavior.JitterRatio = 0.0;

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void SucceedWithMaxJitterRatio()
	{
		// Arrange - exactly 1.0 is valid
		var options = new CacheOptions();
		options.Behavior.JitterRatio = 1.0;

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void ReportMultipleFailures()
	{
		// Arrange
		var options = new CacheOptions();
		options.Behavior.DefaultExpiration = TimeSpan.Zero;
		options.Behavior.CacheTimeout = TimeSpan.Zero;
		options.Behavior.JitterRatio = 2.0;

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(CacheBehaviorOptions.DefaultExpiration));
		result.FailureMessage.ShouldContain(nameof(CacheBehaviorOptions.CacheTimeout));
		result.FailureMessage.ShouldContain(nameof(CacheBehaviorOptions.JitterRatio));
	}
}
