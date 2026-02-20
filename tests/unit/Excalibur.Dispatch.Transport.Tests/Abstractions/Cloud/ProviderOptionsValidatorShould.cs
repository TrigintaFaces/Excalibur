// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Cloud;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class ProviderOptionsValidatorShould
{
    private readonly ProviderOptionsValidator _sut = new();

    [Fact]
    public void Succeed_With_Valid_Options()
    {
        var options = new ProviderOptions
        {
            DefaultTimeoutMs = 5000,
            RetryPolicy = new RetryPolicyOptions
            {
                BaseDelayMs = 100,
                MaxDelayMs = 5000,
            },
        };

        var result = _sut.Validate(null, options);
        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Fail_When_DefaultTimeoutMs_Is_Zero()
    {
        var options = new ProviderOptions { DefaultTimeoutMs = 0 };

        var result = _sut.Validate(null, options);
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("DefaultTimeoutMs");
    }

    [Fact]
    public void Fail_When_DefaultTimeoutMs_Is_Negative()
    {
        var options = new ProviderOptions { DefaultTimeoutMs = -1 };

        var result = _sut.Validate(null, options);
        result.Failed.ShouldBeTrue();
    }

    [Fact]
    public void Fail_When_BaseDelayMs_Greater_Than_MaxDelayMs()
    {
        var options = new ProviderOptions
        {
            DefaultTimeoutMs = 5000,
            RetryPolicy = new RetryPolicyOptions
            {
                BaseDelayMs = 10000,
                MaxDelayMs = 5000,
            },
        };

        var result = _sut.Validate(null, options);
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("BaseDelayMs");
        result.FailureMessage.ShouldContain("MaxDelayMs");
    }

    [Fact]
    public void Fail_When_BaseDelayMs_Is_Zero()
    {
        var options = new ProviderOptions
        {
            DefaultTimeoutMs = 5000,
            RetryPolicy = new RetryPolicyOptions
            {
                BaseDelayMs = 0,
                MaxDelayMs = 5000,
            },
        };

        var result = _sut.Validate(null, options);
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("BaseDelayMs");
    }

    [Fact]
    public void Fail_When_MaxDelayMs_Is_Zero()
    {
        var options = new ProviderOptions
        {
            DefaultTimeoutMs = 5000,
            RetryPolicy = new RetryPolicyOptions
            {
                BaseDelayMs = 0,
                MaxDelayMs = 0,
            },
        };

        var result = _sut.Validate(null, options);
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("MaxDelayMs");
    }

    [Fact]
    public void Succeed_When_RetryPolicy_Is_Null()
    {
        var options = new ProviderOptions
        {
            DefaultTimeoutMs = 5000,
            RetryPolicy = null,
        };

        var result = _sut.Validate(null, options);
        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Report_Multiple_Failures()
    {
        var options = new ProviderOptions
        {
            DefaultTimeoutMs = -1,
            RetryPolicy = new RetryPolicyOptions
            {
                BaseDelayMs = -1,
                MaxDelayMs = -2,
            },
        };

        var result = _sut.Validate(null, options);
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("DefaultTimeoutMs");
        result.FailureMessage.ShouldContain("BaseDelayMs");
        result.FailureMessage.ShouldContain("MaxDelayMs");
    }

    [Fact]
    public void Throw_When_Options_Is_Null()
    {
        Should.Throw<ArgumentNullException>(() => _sut.Validate(null, null!));
    }

    [Fact]
    public void Implement_IValidateOptions()
    {
        _sut.ShouldBeAssignableTo<IValidateOptions<ProviderOptions>>();
    }
}
