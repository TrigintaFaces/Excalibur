// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Security;

namespace Excalibur.Dispatch.Security.Tests.Security.Signing;

/// <summary>
/// Tests for the <see cref="SigningOptions.TenantAlgorithms"/> dictionary property,
/// validating multi-tenant signing algorithm configuration.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
[Trait("Feature", "Signing")]
public sealed class SigningOptionsTenantAlgorithmsShould
{
    [Fact]
    public void DefaultToEmptyDictionary()
    {
        var options = new SigningOptions();
        options.TenantAlgorithms.ShouldNotBeNull();
        options.TenantAlgorithms.Count.ShouldBe(0);
    }

    [Fact]
    public void AllowAddingSingleTenantAlgorithm()
    {
        var options = new SigningOptions();
        options.TenantAlgorithms["tenant-a"] = SigningAlgorithm.HMACSHA512;

        options.TenantAlgorithms.Count.ShouldBe(1);
        options.TenantAlgorithms["tenant-a"].ShouldBe(SigningAlgorithm.HMACSHA512);
    }

    [Fact]
    public void AllowAddingMultipleTenantAlgorithms()
    {
        var options = new SigningOptions();
        options.TenantAlgorithms["tenant-a"] = SigningAlgorithm.HMACSHA256;
        options.TenantAlgorithms["tenant-b"] = SigningAlgorithm.HMACSHA512;
        options.TenantAlgorithms["tenant-c"] = SigningAlgorithm.Ed25519;

        options.TenantAlgorithms.Count.ShouldBe(3);
    }

    [Fact]
    public void AllowOverwritingExistingTenantAlgorithm()
    {
        var options = new SigningOptions();
        options.TenantAlgorithms["tenant-a"] = SigningAlgorithm.HMACSHA256;
        options.TenantAlgorithms["tenant-a"] = SigningAlgorithm.RSASHA256;

        options.TenantAlgorithms["tenant-a"].ShouldBe(SigningAlgorithm.RSASHA256);
    }

    [Fact]
    public void SupportRemovingTenantAlgorithm()
    {
        var options = new SigningOptions();
        options.TenantAlgorithms["tenant-a"] = SigningAlgorithm.HMACSHA256;

        options.TenantAlgorithms.Remove("tenant-a").ShouldBeTrue();
        options.TenantAlgorithms.Count.ShouldBe(0);
    }

    [Fact]
    public void SupportContainsKeyCheck()
    {
        var options = new SigningOptions();
        options.TenantAlgorithms["tenant-a"] = SigningAlgorithm.HMACSHA256;

        options.TenantAlgorithms.ContainsKey("tenant-a").ShouldBeTrue();
        options.TenantAlgorithms.ContainsKey("tenant-b").ShouldBeFalse();
    }

    [Fact]
    public void SupportTryGetValue()
    {
        var options = new SigningOptions();
        options.TenantAlgorithms["tenant-a"] = SigningAlgorithm.ECDSASHA256;

        options.TenantAlgorithms.TryGetValue("tenant-a", out var algorithm).ShouldBeTrue();
        algorithm.ShouldBe(SigningAlgorithm.ECDSASHA256);

        options.TenantAlgorithms.TryGetValue("missing", out _).ShouldBeFalse();
    }

    [Fact]
    public void UseOrdinalStringComparer()
    {
        // The dictionary should use ordinal string comparison (case-sensitive)
        var options = new SigningOptions();
        options.TenantAlgorithms["Tenant-A"] = SigningAlgorithm.HMACSHA256;

        options.TenantAlgorithms.ContainsKey("Tenant-A").ShouldBeTrue();
        options.TenantAlgorithms.ContainsKey("tenant-a").ShouldBeFalse();
    }

    [Fact]
    public void SupportAllSigningAlgorithms()
    {
        var options = new SigningOptions();

        options.TenantAlgorithms["t-unknown"] = SigningAlgorithm.Unknown;
        options.TenantAlgorithms["t-hmac256"] = SigningAlgorithm.HMACSHA256;
        options.TenantAlgorithms["t-hmac512"] = SigningAlgorithm.HMACSHA512;
        options.TenantAlgorithms["t-rsa256"] = SigningAlgorithm.RSASHA256;
        options.TenantAlgorithms["t-rsapss"] = SigningAlgorithm.RSAPSSSHA256;
        options.TenantAlgorithms["t-ecdsa"] = SigningAlgorithm.ECDSASHA256;
        options.TenantAlgorithms["t-ed25519"] = SigningAlgorithm.Ed25519;

        options.TenantAlgorithms.Count.ShouldBe(7);
    }

    [Fact]
    public void CoexistWithDefaultAlgorithm()
    {
        var options = new SigningOptions
        {
            DefaultAlgorithm = SigningAlgorithm.HMACSHA256,
        };

        options.TenantAlgorithms["premium-tenant"] = SigningAlgorithm.Ed25519;

        options.DefaultAlgorithm.ShouldBe(SigningAlgorithm.HMACSHA256);
        options.TenantAlgorithms["premium-tenant"].ShouldBe(SigningAlgorithm.Ed25519);
    }
}
