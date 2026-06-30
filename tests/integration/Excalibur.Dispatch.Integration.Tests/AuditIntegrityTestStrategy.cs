// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Excalibur.AuditLogging;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Integration.Tests;

/// <summary>
/// Test-only helper (qa71t5 F-5 sweep): the real keyed-MAC <see cref="IAuditIntegrityStrategy"/> via the
/// public DI path (internal-first; no public production test-factory).
/// </summary>
internal static class AuditIntegrityTestStrategy
{
    private static readonly byte[] TestSigningKey =
        Encoding.UTF8.GetBytes("qa71t5-test-signing-key-0123456789abcdef");

    public static IAuditIntegrityStrategy Create()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IOptions<AuditIntegrityOptions>>(
            Microsoft.Extensions.Options.Options.Create(
                new AuditIntegrityOptions { SigningKey = TestSigningKey, KeyId = "test" }));
        _ = services.AddAuditIntegrity();
        return services.BuildServiceProvider().GetRequiredService<IAuditIntegrityStrategy>();
    }
}
