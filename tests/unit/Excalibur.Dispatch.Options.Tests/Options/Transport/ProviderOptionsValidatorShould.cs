// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Tests.Options.Transport;

/// <summary>
/// Verifies cross-property validation in <see cref="ProviderOptionsValidator"/>.
/// Sprint 564 S564.55: Transport ProviderOptions IValidateOptions tests.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ProviderOptionsValidatorShould
{
	private readonly ProviderOptionsValidator _sut = new();

	[Fact]
	public void Succeed_WithValidDefaults()
	{
		var options = new ProviderOptions();
		var result = _sut.Validate(null, options);
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void Fail_WhenDefaultTimeoutMsIsZero()
	{
		var options = new ProviderOptions { DefaultTimeoutMs = 0 };
		var result = _sut.Validate(null, options);
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(ProviderOptions.DefaultTimeoutMs));
	}

	[Fact]
	public void Fail_WhenDefaultTimeoutMsIsNegative()
	{
		var options = new ProviderOptions { DefaultTimeoutMs = -1 };
		var result = _sut.Validate(null, options);
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(ProviderOptions.DefaultTimeoutMs));
	}

	[Fact]
	public void Fail_WhenBaseDelayExceedsMaxDelay()
	{
		var options = new ProviderOptions
		{
			RetryPolicy = new RetryPolicyOptions
			{
				BaseDelayMs = 5000,
				MaxDelayMs = 1000,
			},
		};
		var result = _sut.Validate(null, options);
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(RetryPolicyOptions.BaseDelayMs));
	}

	[Fact]
	public void Fail_WhenBaseDelayIsZero()
	{
		var options = new ProviderOptions
		{
			RetryPolicy = new RetryPolicyOptions { BaseDelayMs = 0 },
		};
		var result = _sut.Validate(null, options);
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(RetryPolicyOptions.BaseDelayMs));
	}

	[Fact]
	public void Fail_WhenMaxDelayIsZero()
	{
		var options = new ProviderOptions
		{
			RetryPolicy = new RetryPolicyOptions { MaxDelayMs = 0 },
		};
		var result = _sut.Validate(null, options);
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(RetryPolicyOptions.MaxDelayMs));
	}

	[Fact]
	public void Succeed_WhenRetryPolicyIsNull()
	{
		var options = new ProviderOptions { RetryPolicy = null! };
		var result = _sut.Validate(null, options);
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void Succeed_WhenBaseDelayEqualsMaxDelay()
	{
		var options = new ProviderOptions
		{
			RetryPolicy = new RetryPolicyOptions
			{
				BaseDelayMs = 1000,
				MaxDelayMs = 1000,
			},
		};
		var result = _sut.Validate(null, options);
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void CollectMultipleFailures()
	{
		var options = new ProviderOptions
		{
			DefaultTimeoutMs = 0,
			RetryPolicy = new RetryPolicyOptions
			{
				BaseDelayMs = 5000,
				MaxDelayMs = 0,
			},
		};
		var result = _sut.Validate(null, options);
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(ProviderOptions.DefaultTimeoutMs));
		result.FailureMessage.ShouldContain(nameof(RetryPolicyOptions.MaxDelayMs));
	}
}
