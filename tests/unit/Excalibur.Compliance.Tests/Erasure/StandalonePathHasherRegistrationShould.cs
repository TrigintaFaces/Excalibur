// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance;
using Excalibur.Compliance.Erasure;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.Tests.Erasure;

/// <summary>
/// Review-fix lock (S862 REVIEW B3) — the standalone legal-hold and data-inventory registration paths must
/// register their required <see cref="IDataSubjectHasher"/> dependency.
/// </summary>
/// <remarks>
/// wrht38 made <c>IDataSubjectHasher</c> a required ctor param of <c>LegalHoldService</c> and
/// <c>DataInventoryService</c>, but the hasher was only registered by the erasure paths — so wiring
/// <c>AddLegalHoldService()</c> or <c>AddDataInventoryService()</c> <b>standalone</b> (without
/// <c>AddGdprErasure*</c>) threw <c>Unable to resolve IDataSubjectHasher</c> at resolve. The fix has each
/// standalone extension call <c>AddDataSubjectHashing()</c> (idempotent TryAdd). RED on the pre-fix wiring,
/// GREEN once the hasher is registered on the standalone paths.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance.Erasure")]
public sealed class StandalonePathHasherRegistrationShould
{
    // Pepper ≥ 32 chars so the fail-closed HMAC hasher validates (bd-9so1s5).
    private const string Pepper = "test-pepper-0123456789abcdef0123456789ab";

    private static ServiceProvider BuildProvider(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.Configure<DataSubjectHashingOptions>(o => o.Pepper = Pepper);
        configure(services);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void RegisterHasher_FromStandaloneAddLegalHoldService()
    {
        // The B3 concern is precisely that the hasher is registered on the standalone path (pre-fix it was
        // not → LegalHoldService's required IDataSubjectHasher was unresolvable). LegalHoldService's other
        // deps (store, logger) are registered here so the full service also resolves.
        using var provider = BuildProvider(s =>
        {
            s.AddInMemoryLegalHoldStore();
            s.AddLegalHoldService();
        });

        provider.GetService<IDataSubjectHasher>().ShouldNotBeNull(
            "AddLegalHoldService must register IDataSubjectHasher on the standalone path (bd-B3).");
        using var scope = provider.CreateScope();
        Should.NotThrow(() => scope.ServiceProvider.GetRequiredService<ILegalHoldService>());
    }

    [Fact]
    public void RegisterHasher_FromStandaloneAddDataInventoryService()
    {
        // Scoped to the B3 fix: AddDataInventoryService must register IDataSubjectHasher. (DataInventoryService
        // also requires IKeyManagementProvider — a separate dependency outside B3's scope — so this asserts the
        // hasher registration directly rather than full-service construction.)
        using var provider = BuildProvider(s => s.AddDataInventoryService());

        provider.GetService<IDataSubjectHasher>().ShouldNotBeNull(
            "AddDataInventoryService must register IDataSubjectHasher on the standalone path (bd-B3).");
    }
}
