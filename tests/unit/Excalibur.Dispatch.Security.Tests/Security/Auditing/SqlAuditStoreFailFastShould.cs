// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Security;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Security.Tests.Security.Auditing;

/// <summary>
/// bd-kitw4i (S840, AC-1/AC-2) — independent regression lock (author≠impl, TestsDeveloper).
/// <para>
/// The prior <c>SqlSecurityEventStore</c> placeholder, wired to <c>StoreType=SQL</c>, ACCEPTED then
/// silently DISCARDED every audit event (validated, logged a warning, persisted nothing) and returned
/// empty queries — a catastrophic compliance/forensics data-loss landmine.
/// </para>
/// <para>
/// Fork B (fail-fast, SoftwareArchitect-ruled): registering security auditing with <c>StoreType=SQL</c>
/// MUST throw at composition (registration) time with a diagnostic naming the missing SQL store, so the
/// silent-discard behavior is structurally unreachable. This lock is RED on the pre-fix code (which
/// registered the placeholder and did NOT throw) and GREEN after the fix.
/// </para>
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Security)]
public sealed class SqlAuditStoreFailFastShould
{
    [Fact]
    public void ThrowAtRegistrationWhenStoreTypeIsSql()
    {
        // Arrange — StoreType=SQL selects a SQL-backed audit store that Excalibur.Security does not ship.
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["Security:Auditing:StoreType"] = "SQL",
            })
            .Build();

        // Act & Assert — fail fast at registration; the silent-discard placeholder is unreachable.
        var ex = Should.Throw<InvalidOperationException>(() => services.AddSecurityAuditing(configuration));

        // The diagnostic MUST name the missing SQL store (AC-1: observable + actionable, not a Warning).
        ex.Message.ShouldContain("SQL");
    }

    [Fact]
    public void NotThrowForDefaultInMemoryStore()
    {
        // Control case — proves the lock binds the SQL fail-fast gate specifically, not a general
        // registration failure. Omitting StoreType uses the in-memory development store, which must
        // register cleanly (no accept-then-discard, no throw).
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal))
            .Build();

        Should.NotThrow(() => services.AddSecurityAuditing(configuration));
    }
}
