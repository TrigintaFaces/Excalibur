// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Security;

namespace Excalibur.Dispatch.Security.Tests.Security.Authentication;

/// <summary>
/// Unit tests for <see cref="JwtTokenValidationOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
[Trait("Feature", "Authentication")]
public sealed class JwtTokenValidationOptionsShould
{
    [Fact]
    public void DefaultValidateIssuerToTrue()
    {
        var options = new JwtTokenValidationOptions();
        options.ValidateIssuer.ShouldBeTrue();
    }

    [Fact]
    public void DefaultValidateAudienceToTrue()
    {
        var options = new JwtTokenValidationOptions();
        options.ValidateAudience.ShouldBeTrue();
    }

    [Fact]
    public void DefaultValidateLifetimeToTrue()
    {
        var options = new JwtTokenValidationOptions();
        options.ValidateLifetime.ShouldBeTrue();
    }

    [Fact]
    public void DefaultValidateSigningKeyToTrue()
    {
        var options = new JwtTokenValidationOptions();
        options.ValidateSigningKey.ShouldBeTrue();
    }

    [Fact]
    public void DefaultRequireExpirationTimeToTrue()
    {
        var options = new JwtTokenValidationOptions();
        options.RequireExpirationTime.ShouldBeTrue();
    }

    [Fact]
    public void DefaultRequireSignedTokensToTrue()
    {
        var options = new JwtTokenValidationOptions();
        options.RequireSignedTokens.ShouldBeTrue();
    }

    [Fact]
    public void AllowSettingValidateIssuerToFalse()
    {
        var options = new JwtTokenValidationOptions { ValidateIssuer = false };
        options.ValidateIssuer.ShouldBeFalse();
    }

    [Fact]
    public void AllowSettingValidateAudienceToFalse()
    {
        var options = new JwtTokenValidationOptions { ValidateAudience = false };
        options.ValidateAudience.ShouldBeFalse();
    }

    [Fact]
    public void AllowSettingValidateLifetimeToFalse()
    {
        var options = new JwtTokenValidationOptions { ValidateLifetime = false };
        options.ValidateLifetime.ShouldBeFalse();
    }

    [Fact]
    public void AllowSettingValidateSigningKeyToFalse()
    {
        var options = new JwtTokenValidationOptions { ValidateSigningKey = false };
        options.ValidateSigningKey.ShouldBeFalse();
    }

    [Fact]
    public void AllowSettingRequireExpirationTimeToFalse()
    {
        var options = new JwtTokenValidationOptions { RequireExpirationTime = false };
        options.RequireExpirationTime.ShouldBeFalse();
    }

    [Fact]
    public void AllowSettingRequireSignedTokensToFalse()
    {
        var options = new JwtTokenValidationOptions { RequireSignedTokens = false };
        options.RequireSignedTokens.ShouldBeFalse();
    }

    [Fact]
    public void BeSealed()
    {
        typeof(JwtTokenValidationOptions).IsSealed.ShouldBeTrue();
    }

    [Fact]
    public void BePublic()
    {
        typeof(JwtTokenValidationOptions).IsPublic.ShouldBeTrue();
    }
}
