// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Security;

namespace Excalibur.Dispatch.Security.Tests.Security.RateLimiting;

/// <summary>
/// Unit tests for <see cref="RateLimitKeyPrefixes"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
[Trait("Feature", "RateLimiting")]
public sealed class RateLimitKeyPrefixesShould
{
    [Fact]
    public void HaveTenantPrefix()
    {
        RateLimitKeyPrefixes.Tenant.ShouldBe("tenant:");
    }

    [Fact]
    public void HaveUserPrefix()
    {
        RateLimitKeyPrefixes.User.ShouldBe("user:");
    }

    [Fact]
    public void HaveApiKeyPrefix()
    {
        RateLimitKeyPrefixes.ApiKey.ShouldBe("api:");
    }

    [Fact]
    public void HaveIpPrefix()
    {
        RateLimitKeyPrefixes.Ip.ShouldBe("ip:");
    }

    [Fact]
    public void HaveMessageTypePrefix()
    {
        RateLimitKeyPrefixes.MessageType.ShouldBe("type:");
    }

    [Fact]
    public void HaveTierPrefix()
    {
        RateLimitKeyPrefixes.Tier.ShouldBe("tier:");
    }

    [Fact]
    public void HaveGlobalKey()
    {
        RateLimitKeyPrefixes.Global.ShouldBe("global");
    }

    [Fact]
    public void BeStaticClass()
    {
        typeof(RateLimitKeyPrefixes).IsAbstract.ShouldBeTrue();
        typeof(RateLimitKeyPrefixes).IsSealed.ShouldBeTrue();
    }

    [Fact]
    public void HaveUniqueValues()
    {
        var values = new[]
        {
            RateLimitKeyPrefixes.Tenant,
            RateLimitKeyPrefixes.User,
            RateLimitKeyPrefixes.ApiKey,
            RateLimitKeyPrefixes.Ip,
            RateLimitKeyPrefixes.MessageType,
            RateLimitKeyPrefixes.Tier,
            RateLimitKeyPrefixes.Global,
        };

        values.Distinct().Count().ShouldBe(values.Length);
    }
}
