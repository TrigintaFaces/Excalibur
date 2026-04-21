// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Policy.Cedar;

using Microsoft.Extensions.Options;

namespace Excalibur.Tests.A3.Policy;

/// <summary>
/// Tests for <see cref="CedarOptionsValidator"/> -- IValidateOptions for PolicyStoreId in AVP mode
/// (Sprint 727 T.2 l0xjio).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class CedarOptionsValidatorShould
{
    // ──────────────────────────────────────────────
    // Validation via DI (ValidateOnStart)
    // ──────────────────────────────────────────────

    [Fact]
    public void FailWhenAvpModeAndPolicyStoreIdIsNull()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var builder = services.AddExcaliburA3()
            .UseCedarPolicy(opts =>
            {
                opts.Endpoint = "http://avp-test:443";
                opts.Mode = CedarMode.AwsVerifiedPermissions;
                // PolicyStoreId intentionally omitted (null)
            });

        using var provider = services.BuildServiceProvider();

        Should.Throw<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<CedarOptions>>().Value);
    }

    [Fact]
    public void FailWhenAvpModeAndPolicyStoreIdIsEmpty()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var builder = services.AddExcaliburA3()
            .UseCedarPolicy(opts =>
            {
                opts.Endpoint = "http://avp-test:443";
                opts.Mode = CedarMode.AwsVerifiedPermissions;
                opts.PolicyStoreId = "";
            });

        using var provider = services.BuildServiceProvider();

        Should.Throw<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<CedarOptions>>().Value);
    }

    [Fact]
    public void SucceedWhenAvpModeAndPolicyStoreIdIsSet()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var builder = services.AddExcaliburA3()
            .UseCedarPolicy(opts =>
            {
                opts.Endpoint = "http://avp-test:443";
                opts.Mode = CedarMode.AwsVerifiedPermissions;
                opts.PolicyStoreId = "ps-abc123";
            });

        using var provider = services.BuildServiceProvider();

        // Should not throw
        var options = provider.GetRequiredService<IOptions<CedarOptions>>().Value;
        options.PolicyStoreId.ShouldBe("ps-abc123");
    }

    [Fact]
    public void SucceedWhenLocalModeAndPolicyStoreIdIsNull()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var builder = services.AddExcaliburA3()
            .UseCedarPolicy(opts =>
            {
                opts.Endpoint = "http://cedar-test:8180";
                opts.Mode = CedarMode.Local;
                // PolicyStoreId intentionally null -- valid for Local mode
            });

        using var provider = services.BuildServiceProvider();

        // Should not throw
        var options = provider.GetRequiredService<IOptions<CedarOptions>>().Value;
        options.Mode.ShouldBe(CedarMode.Local);
    }

    [Fact]
    public void AcceptDefaultEndpoint_WhenConfigured()
    {
        // ValidateDataAnnotations removed in Sprint 750 AOT migration -- [Required] no longer enforced via DI
        var services = new ServiceCollection();
        services.AddLogging();

        var builder = services.AddExcaliburA3()
            .UseCedarPolicy(opts =>
            {
                opts.Mode = CedarMode.Local;
            });

        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<CedarOptions>>().Value;
        options.Mode.ShouldBe(CedarMode.Local);
    }
}
