// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Options.Core;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Tests.Options.Core;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Configuration)]
public sealed class CompressionOptionsValidatorShould
{
	private readonly CompressionOptionsValidator _sut = new();

	#region Happy Path

	[Fact]
	public void Succeed_with_default_options()
	{
		var result = _sut.Validate(null, new CompressionOptions());
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void Succeed_with_valid_custom_options()
	{
		var result = _sut.Validate(null, new CompressionOptions
		{
			CompressionLevel = 9,
			MinimumSizeThreshold = 4096,
			CompressionType = CompressionType.Gzip
		});
		result.Succeeded.ShouldBeTrue();
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(5)]
	[InlineData(9)]
	public void Succeed_when_compression_level_is_in_range(int level)
	{
		var result = _sut.Validate(null, new CompressionOptions { CompressionLevel = level });
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void Succeed_when_minimum_size_threshold_is_zero()
	{
		var result = _sut.Validate(null, new CompressionOptions { MinimumSizeThreshold = 0 });
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void Succeed_with_none_compression_type()
	{
		var result = _sut.Validate(null, new CompressionOptions { CompressionType = CompressionType.None });
		result.Succeeded.ShouldBeTrue();
	}

	#endregion

	#region Failure Paths

	[Fact]
	public void Fail_when_compression_level_is_negative()
	{
		var result = _sut.Validate(null, new CompressionOptions { CompressionLevel = -1 });
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(CompressionOptions.CompressionLevel));
	}

	[Fact]
	public void Fail_when_compression_level_exceeds_nine()
	{
		var result = _sut.Validate(null, new CompressionOptions { CompressionLevel = 10 });
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(CompressionOptions.CompressionLevel));
	}

	[Fact]
	public void Fail_when_minimum_size_threshold_is_negative()
	{
		var result = _sut.Validate(null, new CompressionOptions { MinimumSizeThreshold = -1 });
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(CompressionOptions.MinimumSizeThreshold));
	}

	[Fact]
	public void Fail_when_compression_type_is_invalid_enum()
	{
		var result = _sut.Validate(null, new CompressionOptions { CompressionType = (CompressionType)999 });
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(CompressionOptions.CompressionType));
	}

	[Fact]
	public void Fail_with_multiple_errors_when_all_invalid()
	{
		var result = _sut.Validate(null, new CompressionOptions
		{
			CompressionLevel = -1,
			MinimumSizeThreshold = -100,
			CompressionType = (CompressionType)999
		});
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(CompressionOptions.CompressionLevel));
		result.FailureMessage.ShouldContain(nameof(CompressionOptions.MinimumSizeThreshold));
		result.FailureMessage.ShouldContain(nameof(CompressionOptions.CompressionType));
	}

	#endregion

	#region Null Input

	[Fact]
	public void Throw_when_options_is_null()
	{
		Should.Throw<ArgumentNullException>(() => _sut.Validate(null, null!));
	}

	#endregion
}
