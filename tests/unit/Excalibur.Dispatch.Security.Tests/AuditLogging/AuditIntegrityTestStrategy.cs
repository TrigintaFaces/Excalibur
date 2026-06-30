// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Excalibur.AuditLogging;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Security.Tests.AuditLogging;

/// <summary>
/// Test-only helper (qa71t5 F-5 sweep) that builds the <b>real</b> keyed-MAC
/// <see cref="IAuditIntegrityStrategy"/> for audit-store constructions in tests — via the public DI path
/// (<c>AddAuditIntegrity()</c> + a test signing key), per the SA/PM internal-first ruling (no public
/// production test-factory). Drives the genuine <c>HmacAuditIntegrityStrategy</c> so integrity-asserting
/// store tests stay honest (<c>verify-against-real-infra-not-mock</c>).
/// </summary>
internal static class AuditIntegrityTestStrategy
{
    // A fixed, non-secret test signing key (test fixture; not a real credential).
    private static readonly byte[] TestSigningKey =
        Encoding.UTF8.GetBytes("qa71t5-test-signing-key-0123456789abcdef");

    /// <summary>Builds the real keyed-MAC strategy over a deterministic test key.</summary>
    public static IAuditIntegrityStrategy Create()
    {
        var services = new ServiceCollection();
        // SigningKey is init-only → set via object-initializer + Options.Create (Configure(Action) can't set init).
        _ = services.AddSingleton<IOptions<AuditIntegrityOptions>>(
            Microsoft.Extensions.Options.Options.Create(
                new AuditIntegrityOptions { SigningKey = TestSigningKey, KeyId = "test" }));
        _ = services.AddAuditIntegrity();
        return services.BuildServiceProvider().GetRequiredService<IAuditIntegrityStrategy>();
    }
}
