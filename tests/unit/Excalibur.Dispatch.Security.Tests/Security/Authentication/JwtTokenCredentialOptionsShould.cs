// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Security;

namespace Excalibur.Dispatch.Security.Tests.Security.Authentication;

/// <summary>
/// Unit tests for <see cref="JwtTokenCredentialOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
[Trait("Feature", "Authentication")]
public sealed class JwtTokenCredentialOptionsShould
{
    [Fact]
    public void DefaultValidIssuerToNull()
    {
        var options = new JwtTokenCredentialOptions();
        options.ValidIssuer.ShouldBeNull();
    }

    [Fact]
    public void DefaultValidIssuersToNull()
    {
        var options = new JwtTokenCredentialOptions();
        options.ValidIssuers.ShouldBeNull();
    }

    [Fact]
    public void DefaultValidAudienceToNull()
    {
        var options = new JwtTokenCredentialOptions();
        options.ValidAudience.ShouldBeNull();
    }

    [Fact]
    public void DefaultValidAudiencesToNull()
    {
        var options = new JwtTokenCredentialOptions();
        options.ValidAudiences.ShouldBeNull();
    }

    [Fact]
    public void DefaultSigningKeyToNull()
    {
        var options = new JwtTokenCredentialOptions();
        options.SigningKey.ShouldBeNull();
    }

    [Fact]
    public void DefaultRsaPublicKeyToNull()
    {
        var options = new JwtTokenCredentialOptions();
        options.RsaPublicKey.ShouldBeNull();
    }

    [Fact]
    public void DefaultUseAsyncKeyRetrievalToFalse()
    {
        var options = new JwtTokenCredentialOptions();
        options.UseAsyncKeyRetrieval.ShouldBeFalse();
    }

    [Fact]
    public void DefaultSigningKeyCredentialNameToNull()
    {
        var options = new JwtTokenCredentialOptions();
        options.SigningKeyCredentialName.ShouldBeNull();
    }

    [Fact]
    public void AllowSettingValidIssuer()
    {
        var options = new JwtTokenCredentialOptions { ValidIssuer = "https://issuer.example.com" };
        options.ValidIssuer.ShouldBe("https://issuer.example.com");
    }

    [Fact]
    public void AllowSettingValidIssuers()
    {
        var options = new JwtTokenCredentialOptions
        {
            ValidIssuers = ["https://issuer1.example.com", "https://issuer2.example.com"],
        };

        options.ValidIssuers.ShouldNotBeNull();
        options.ValidIssuers.Length.ShouldBe(2);
    }

    [Fact]
    public void AllowSettingValidAudience()
    {
        var options = new JwtTokenCredentialOptions { ValidAudience = "my-api" };
        options.ValidAudience.ShouldBe("my-api");
    }

    [Fact]
    public void AllowSettingValidAudiences()
    {
        var options = new JwtTokenCredentialOptions
        {
            ValidAudiences = ["api-1", "api-2"],
        };

        options.ValidAudiences.ShouldNotBeNull();
        options.ValidAudiences.Length.ShouldBe(2);
    }

    [Fact]
    public void AllowSettingSigningKey()
    {
        var options = new JwtTokenCredentialOptions { SigningKey = "my-super-secret-key-1234567890" };
        options.SigningKey.ShouldBe("my-super-secret-key-1234567890");
    }

    [Fact]
    public void AllowSettingRsaPublicKey()
    {
        var options = new JwtTokenCredentialOptions { RsaPublicKey = "-----BEGIN PUBLIC KEY-----" };
        options.RsaPublicKey.ShouldBe("-----BEGIN PUBLIC KEY-----");
    }

    [Fact]
    public void AllowSettingUseAsyncKeyRetrieval()
    {
        var options = new JwtTokenCredentialOptions { UseAsyncKeyRetrieval = true };
        options.UseAsyncKeyRetrieval.ShouldBeTrue();
    }

    [Fact]
    public void AllowSettingSigningKeyCredentialName()
    {
        var options = new JwtTokenCredentialOptions { SigningKeyCredentialName = "jwt-signing-key" };
        options.SigningKeyCredentialName.ShouldBe("jwt-signing-key");
    }

    [Fact]
    public void BeSealed()
    {
        typeof(JwtTokenCredentialOptions).IsSealed.ShouldBeTrue();
    }

    [Fact]
    public void BePublic()
    {
        typeof(JwtTokenCredentialOptions).IsPublic.ShouldBeTrue();
    }
}
