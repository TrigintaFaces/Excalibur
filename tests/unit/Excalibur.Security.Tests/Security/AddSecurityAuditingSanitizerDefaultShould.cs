// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Telemetry;

using Microsoft.Extensions.Configuration;

namespace Excalibur.Security.Tests.Security;

/// <summary>
/// Review-fix lock (S862 REVIEW B2, OWASP A02) — <c>AddSecurityAuditing</c> must resolve a
/// <b>safe-by-default</b> <see cref="ITelemetrySanitizer"/> that masks PII, never a raw pass-through.
/// </summary>
/// <remarks>
/// Pre-fix the default was <c>NullTelemetrySanitizer.Instance</c> (no-op → raw <c>UserId</c>/<c>SourceIp</c>
/// to the sink). The fix registers <c>MaskingTelemetrySanitizer</c> via <c>TryAddSingleton</c>. These assert
/// the <b>resolved behavior</b> (a sensitive tag comes back masked, not raw), and that opting into raw is an
/// explicit, deliberate registration (never a silent default). RED on the pre-fix no-op default.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
public sealed class AddSecurityAuditingSanitizerDefaultShould
{
    private const string SensitiveValue = "alice@example.com";

    private static IConfiguration EmptyConfig() =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();

    [Fact]
    public void ResolveAMaskingSanitizer_ByDefault_NotRawPassThrough()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        _ = services.AddSecurityAuditing(EmptyConfig());

        using var provider = services.BuildServiceProvider();
        var sanitizer = provider.GetRequiredService<ITelemetrySanitizer>();

        var masked = sanitizer.SanitizeTag("UserId", SensitiveValue);

        // Safe-by-default: the raw PII value must NOT be emitted; it is replaced by a stable fingerprint.
        masked.ShouldNotBe(SensitiveValue, "AddSecurityAuditing must mask PII by default (OWASP A02), not pass it through raw");
        masked.ShouldNotBeNullOrEmpty();
        sanitizer.ShouldNotBeOfType<NullTelemetrySanitizer>("the no-op sanitizer must not be the audit default");
    }

    [Fact]
    public void HonorExplicitRawOptIn_WhenCallerRegistersNullSanitizerFirst()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        // Deliberate, explicit raw opt-in registered BEFORE AddSecurityAuditing wins over the TryAdd default.
        services.AddSingleton<ITelemetrySanitizer>(NullTelemetrySanitizer.Instance);
        _ = services.AddSecurityAuditing(EmptyConfig());

        using var provider = services.BuildServiceProvider();
        var sanitizer = provider.GetRequiredService<ITelemetrySanitizer>();

        // Raw is only reachable via this explicit choice — passthrough returns the value unchanged.
        sanitizer.SanitizeTag("UserId", SensitiveValue).ShouldBe(SensitiveValue);
    }
}
