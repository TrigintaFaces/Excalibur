// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Security;

namespace Excalibur.Dispatch.Security.Tests.Security.Authentication;

/// <summary>
/// Tests that <see cref="JwtAuthenticationOptions"/> backward-compatible shim properties
/// correctly delegate to the sub-options objects.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
[Trait("Feature", "Authentication")]
public sealed class JwtAuthenticationOptionsSubOptionsShould
{
    [Fact]
    public void DelegateValidateIssuerToValidationSubOptions()
    {
        var options = new JwtAuthenticationOptions();
        options.ValidateIssuer = false;
        options.Validation.ValidateIssuer.ShouldBeFalse();
    }

    [Fact]
    public void DelegateValidateAudienceToValidationSubOptions()
    {
        var options = new JwtAuthenticationOptions();
        options.ValidateAudience = false;
        options.Validation.ValidateAudience.ShouldBeFalse();
    }

    [Fact]
    public void DelegateValidateLifetimeToValidationSubOptions()
    {
        var options = new JwtAuthenticationOptions();
        options.ValidateLifetime = false;
        options.Validation.ValidateLifetime.ShouldBeFalse();
    }

    [Fact]
    public void DelegateValidateSigningKeyToValidationSubOptions()
    {
        var options = new JwtAuthenticationOptions();
        options.ValidateSigningKey = false;
        options.Validation.ValidateSigningKey.ShouldBeFalse();
    }

    [Fact]
    public void DelegateRequireExpirationTimeToValidationSubOptions()
    {
        var options = new JwtAuthenticationOptions();
        options.RequireExpirationTime = false;
        options.Validation.RequireExpirationTime.ShouldBeFalse();
    }

    [Fact]
    public void DelegateRequireSignedTokensToValidationSubOptions()
    {
        var options = new JwtAuthenticationOptions();
        options.RequireSignedTokens = false;
        options.Validation.RequireSignedTokens.ShouldBeFalse();
    }

    [Fact]
    public void DelegateValidIssuerToCredentialSubOptions()
    {
        var options = new JwtAuthenticationOptions();
        options.ValidIssuer = "test-issuer";
        options.Credentials.ValidIssuer.ShouldBe("test-issuer");
    }

    [Fact]
    public void DelegateValidIssuersToCredentialSubOptions()
    {
        var options = new JwtAuthenticationOptions();
        options.ValidIssuers = ["issuer-1", "issuer-2"];
        options.Credentials.ValidIssuers.ShouldNotBeNull();
        options.Credentials.ValidIssuers!.Length.ShouldBe(2);
    }

    [Fact]
    public void DelegateValidAudienceToCredentialSubOptions()
    {
        var options = new JwtAuthenticationOptions();
        options.ValidAudience = "test-audience";
        options.Credentials.ValidAudience.ShouldBe("test-audience");
    }

    [Fact]
    public void DelegateValidAudiencesToCredentialSubOptions()
    {
        var options = new JwtAuthenticationOptions();
        options.ValidAudiences = ["aud-1", "aud-2"];
        options.Credentials.ValidAudiences.ShouldNotBeNull();
        options.Credentials.ValidAudiences!.Length.ShouldBe(2);
    }

    [Fact]
    public void DelegateSigningKeyToCredentialSubOptions()
    {
        var options = new JwtAuthenticationOptions();
        options.SigningKey = "my-signing-key";
        options.Credentials.SigningKey.ShouldBe("my-signing-key");
    }

    [Fact]
    public void DelegateRsaPublicKeyToCredentialSubOptions()
    {
        var options = new JwtAuthenticationOptions();
        options.RsaPublicKey = "rsa-key-content";
        options.Credentials.RsaPublicKey.ShouldBe("rsa-key-content");
    }

    [Fact]
    public void DelegateUseAsyncKeyRetrievalToCredentialSubOptions()
    {
        var options = new JwtAuthenticationOptions();
        options.UseAsyncKeyRetrieval = true;
        options.Credentials.UseAsyncKeyRetrieval.ShouldBeTrue();
    }

    [Fact]
    public void DelegateSigningKeyCredentialNameToCredentialSubOptions()
    {
        var options = new JwtAuthenticationOptions();
        options.SigningKeyCredentialName = "cred-name";
        options.Credentials.SigningKeyCredentialName.ShouldBe("cred-name");
    }

    [Fact]
    public void ReadFromValidationSubOptionsWhenSetDirectly()
    {
        var options = new JwtAuthenticationOptions();
        options.Validation.ValidateIssuer = false;
        options.ValidateIssuer.ShouldBeFalse();
    }

    [Fact]
    public void ReadFromCredentialSubOptionsWhenSetDirectly()
    {
        var options = new JwtAuthenticationOptions();
        options.Credentials.ValidIssuer = "direct-issuer";
        options.ValidIssuer.ShouldBe("direct-issuer");
    }

    [Fact]
    public void DefaultValidationSubOptionsToNewInstance()
    {
        var options = new JwtAuthenticationOptions();
        options.Validation.ShouldNotBeNull();
    }

    [Fact]
    public void DefaultCredentialSubOptionsToNewInstance()
    {
        var options = new JwtAuthenticationOptions();
        options.Credentials.ShouldNotBeNull();
    }

    [Fact]
    public void AllowReplacingValidationSubOptions()
    {
        var options = new JwtAuthenticationOptions();
        var custom = new JwtTokenValidationOptions { ValidateIssuer = false };
        options.Validation = custom;
        options.ValidateIssuer.ShouldBeFalse();
    }

    [Fact]
    public void AllowReplacingCredentialSubOptions()
    {
        var options = new JwtAuthenticationOptions();
        var custom = new JwtTokenCredentialOptions { ValidIssuer = "replaced-issuer" };
        options.Credentials = custom;
        options.ValidIssuer.ShouldBe("replaced-issuer");
    }
}
