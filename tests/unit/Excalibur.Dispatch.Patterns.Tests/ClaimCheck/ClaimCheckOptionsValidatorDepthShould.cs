// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Patterns.Tests.ClaimCheck;

/// <summary>
/// Depth coverage tests for <see cref="ClaimCheckOptionsValidator"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ClaimCheckOptionsValidatorDepthShould
{
	private readonly ClaimCheckOptionsValidator _validator = new();

	[Fact]
	public void Validate_DefaultOptions_Succeeds()
	{
		var options = new ClaimCheckOptions();
		var result = _validator.Validate(null, options);
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void Validate_ThrowsArgumentNull_WhenOptionsIsNull()
	{
		Should.Throw<ArgumentNullException>(() => _validator.Validate(null, null!));
	}

	[Fact]
	public void Validate_Fails_WhenPayloadThresholdIsZero()
	{
		var options = new ClaimCheckOptions { PayloadThreshold = 0 };
		var result = _validator.Validate(null, options);
		result.Succeeded.ShouldBeFalse();
		result.FailureMessage.ShouldContain("PayloadThreshold");
	}

	[Fact]
	public void Validate_Fails_WhenPayloadThresholdIsNegative()
	{
		var options = new ClaimCheckOptions { PayloadThreshold = -1 };
		var result = _validator.Validate(null, options);
		result.Succeeded.ShouldBeFalse();
	}

	[Fact]
	public void Validate_Fails_WhenCompressionThresholdIsNegative()
	{
		var options = new ClaimCheckOptions
		{
			EnableCompression = true,
			CompressionThreshold = -10,
		};
		var result = _validator.Validate(null, options);
		result.Succeeded.ShouldBeFalse();
		result.FailureMessage.ShouldContain("CompressionThreshold");
	}

	[Fact]
	public void Validate_Fails_WhenCompressionThresholdExceedsPayloadThreshold()
	{
		var options = new ClaimCheckOptions
		{
			PayloadThreshold = 1000,
			EnableCompression = true,
			CompressionThreshold = 2000,
		};
		var result = _validator.Validate(null, options);
		result.Succeeded.ShouldBeFalse();
		result.FailureMessage.ShouldContain("CompressionThreshold");
	}

	[Fact]
	public void Validate_Fails_WhenMinCompressionRatioIsNegative()
	{
		var options = new ClaimCheckOptions { MinCompressionRatio = -0.1 };
		var result = _validator.Validate(null, options);
		result.Succeeded.ShouldBeFalse();
		result.FailureMessage.ShouldContain("MinCompressionRatio");
	}

	[Fact]
	public void Validate_Fails_WhenMinCompressionRatioExceedsOne()
	{
		var options = new ClaimCheckOptions { MinCompressionRatio = 1.1 };
		var result = _validator.Validate(null, options);
		result.Succeeded.ShouldBeFalse();
		result.FailureMessage.ShouldContain("MinCompressionRatio");
	}

	[Fact]
	public void Validate_Fails_WhenCleanupIntervalIsNegative()
	{
		var options = new ClaimCheckOptions
		{
			EnableCleanup = true,
			CleanupInterval = TimeSpan.FromSeconds(-1),
		};
		var result = _validator.Validate(null, options);
		result.Succeeded.ShouldBeFalse();
		result.FailureMessage.ShouldContain("CleanupInterval");
	}

	[Fact]
	public void Validate_Fails_WhenDefaultTtlIsNegative()
	{
		var options = new ClaimCheckOptions
		{
			EnableCleanup = true,
			DefaultTtl = TimeSpan.FromSeconds(-1),
		};
		var result = _validator.Validate(null, options);
		result.Succeeded.ShouldBeFalse();
		result.FailureMessage.ShouldContain("DefaultTtl");
	}

	[Fact]
	public void Validate_Fails_WhenCleanupBatchSizeIsZero()
	{
		var options = new ClaimCheckOptions { EnableCleanup = true };
		options.Cleanup.CleanupBatchSize = 0;
		var result = _validator.Validate(null, options);
		result.Succeeded.ShouldBeFalse();
		result.FailureMessage.ShouldContain("CleanupBatchSize");
	}

	[Fact]
	public void Validate_Fails_WhenCleanupIntervalExceedsDefaultTtl()
	{
		var options = new ClaimCheckOptions
		{
			EnableCleanup = true,
			CleanupInterval = TimeSpan.FromDays(3),
			DefaultTtl = TimeSpan.FromDays(1),
		};
		var result = _validator.Validate(null, options);
		result.Succeeded.ShouldBeFalse();
		result.FailureMessage.ShouldContain("CleanupInterval");
	}

	[Fact]
	public void Validate_Succeeds_WhenCleanupDisabled()
	{
		var options = new ClaimCheckOptions
		{
			EnableCleanup = false,
			CleanupInterval = TimeSpan.FromSeconds(-1), // Would fail if cleanup enabled
		};
		var result = _validator.Validate(null, options);
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void Validate_Succeeds_WhenCompressionDisabled()
	{
		var options = new ClaimCheckOptions
		{
			EnableCompression = false,
			CompressionThreshold = -5, // Would fail if compression enabled
		};
		var result = _validator.Validate(null, options);
		result.Succeeded.ShouldBeTrue();
	}
}
