// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Configuration;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Tests.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Dispatch.Core")]
public sealed class DispatchOptionsValidatorShould
{
	private readonly DispatchOptionsValidator _sut = new();

	[Fact]
	public void SucceedWithDefaultOptions()
	{
		var result = _sut.Validate(null, new DispatchOptions());
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void FailWhenDefaultTimeoutIsZero()
	{
		var options = new DispatchOptions { DefaultTimeout = TimeSpan.Zero };
		var result = _sut.Validate(null, options);

		result.Succeeded.ShouldBeFalse();
		result.FailureMessage.ShouldContain("DefaultTimeout");
	}

	[Fact]
	public void FailWhenDefaultTimeoutIsNegative()
	{
		var options = new DispatchOptions { DefaultTimeout = TimeSpan.FromSeconds(-1) };
		var result = _sut.Validate(null, options);

		result.Succeeded.ShouldBeFalse();
		result.FailureMessage.ShouldContain("DefaultTimeout");
	}

	[Fact]
	public void FailWhenMaxConcurrencyIsZero()
	{
		var options = new DispatchOptions { MaxConcurrency = 0 };
		var result = _sut.Validate(null, options);

		result.Succeeded.ShouldBeFalse();
		result.FailureMessage.ShouldContain("MaxConcurrency");
	}

	[Fact]
	public void FailWhenMaxConcurrencyIsNegative()
	{
		var options = new DispatchOptions { MaxConcurrency = -1 };
		var result = _sut.Validate(null, options);

		result.Succeeded.ShouldBeFalse();
		result.FailureMessage.ShouldContain("MaxConcurrency");
	}

	[Fact]
	public void FailWhenMessageBufferSizeIsZero()
	{
		var options = new DispatchOptions { MessageBufferSize = 0 };
		var result = _sut.Validate(null, options);

		result.Succeeded.ShouldBeFalse();
		result.FailureMessage.ShouldContain("MessageBufferSize");
	}

	[Fact]
	public void ReportMultipleFailures()
	{
		var options = new DispatchOptions
		{
			DefaultTimeout = TimeSpan.Zero,
			MaxConcurrency = 0,
			MessageBufferSize = 0,
		};
		var result = _sut.Validate(null, options);

		result.Succeeded.ShouldBeFalse();
		result.FailureMessage.ShouldContain("DefaultTimeout");
		result.FailureMessage.ShouldContain("MaxConcurrency");
		result.FailureMessage.ShouldContain("MessageBufferSize");
	}

	[Fact]
	public void SucceedWithMinimumValidValues()
	{
		var options = new DispatchOptions
		{
			DefaultTimeout = TimeSpan.FromMilliseconds(1),
			MaxConcurrency = 1,
			MessageBufferSize = 1,
		};
		var result = _sut.Validate(null, options);

		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void ThrowWhenOptionsIsNull()
	{
		Should.Throw<ArgumentNullException>(() => _sut.Validate(null, null!));
	}
}
