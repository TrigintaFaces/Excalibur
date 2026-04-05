// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Security;

namespace Excalibur.Dispatch.Security.Tests.Security.Authentication;

/// <summary>
/// Tests that <see cref="JwtAuthenticationOptions"/> sub-options objects
/// (<see cref="JwtTokenValidationOptions"/> and <see cref="JwtTokenCredentialOptions"/>)
/// are properly initialized and configurable.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Security)]
[Trait("Feature", "Authentication")]
public sealed class JwtAuthenticationOptionsSubOptionsShould
{
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
        options.Validation.ValidateIssuer.ShouldBeFalse();
    }

    [Fact]
    public void AllowReplacingCredentialSubOptions()
    {
        var options = new JwtAuthenticationOptions();
        var custom = new JwtTokenCredentialOptions { ValidIssuer = "replaced-issuer" };
        options.Credentials = custom;
        options.Credentials.ValidIssuer.ShouldBe("replaced-issuer");
    }

    [Fact]
    public void ValidationSubOptionsDefaultsToAllTrue()
    {
        var options = new JwtAuthenticationOptions();
        options.Validation.ValidateIssuer.ShouldBeTrue();
        options.Validation.ValidateAudience.ShouldBeTrue();
        options.Validation.ValidateLifetime.ShouldBeTrue();
        options.Validation.ValidateSigningKey.ShouldBeTrue();
        options.Validation.RequireExpirationTime.ShouldBeTrue();
        options.Validation.RequireSignedTokens.ShouldBeTrue();
    }

    [Fact]
    public void CredentialSubOptionsDefaultsToNull()
    {
        var options = new JwtAuthenticationOptions();
        options.Credentials.ValidIssuer.ShouldBeNull();
        options.Credentials.ValidIssuers.ShouldBeNull();
        options.Credentials.ValidAudience.ShouldBeNull();
        options.Credentials.ValidAudiences.ShouldBeNull();
        options.Credentials.SigningKey.ShouldBeNull();
        options.Credentials.RsaPublicKey.ShouldBeNull();
        options.Credentials.SigningKeyCredentialName.ShouldBeNull();
        options.Credentials.UseAsyncKeyRetrieval.ShouldBeFalse();
    }

    [Fact]
    public void AllowSettingAllValidationProperties()
    {
        var options = new JwtAuthenticationOptions();
        options.Validation.ValidateIssuer = false;
        options.Validation.ValidateAudience = false;
        options.Validation.ValidateLifetime = false;
        options.Validation.ValidateSigningKey = false;
        options.Validation.RequireExpirationTime = false;
        options.Validation.RequireSignedTokens = false;

        options.Validation.ValidateIssuer.ShouldBeFalse();
        options.Validation.ValidateAudience.ShouldBeFalse();
        options.Validation.ValidateLifetime.ShouldBeFalse();
        options.Validation.ValidateSigningKey.ShouldBeFalse();
        options.Validation.RequireExpirationTime.ShouldBeFalse();
        options.Validation.RequireSignedTokens.ShouldBeFalse();
    }

    [Fact]
    public void AllowSettingAllCredentialProperties()
    {
        var options = new JwtAuthenticationOptions();
        options.Credentials.ValidIssuer = "test-issuer";
        options.Credentials.ValidIssuers = ["issuer-1", "issuer-2"];
        options.Credentials.ValidAudience = "test-audience";
        options.Credentials.ValidAudiences = ["aud-1", "aud-2"];
        options.Credentials.SigningKey = "my-signing-key";
        options.Credentials.RsaPublicKey = "rsa-key-content";
        options.Credentials.UseAsyncKeyRetrieval = true;
        options.Credentials.SigningKeyCredentialName = "cred-name";

        options.Credentials.ValidIssuer.ShouldBe("test-issuer");
        options.Credentials.ValidIssuers.ShouldNotBeNull();
        options.Credentials.ValidIssuers!.Length.ShouldBe(2);
        options.Credentials.ValidAudience.ShouldBe("test-audience");
        options.Credentials.ValidAudiences.ShouldNotBeNull();
        options.Credentials.ValidAudiences!.Length.ShouldBe(2);
        options.Credentials.SigningKey.ShouldBe("my-signing-key");
        options.Credentials.RsaPublicKey.ShouldBe("rsa-key-content");
        options.Credentials.UseAsyncKeyRetrieval.ShouldBeTrue();
        options.Credentials.SigningKeyCredentialName.ShouldBe("cred-name");
    }
}
