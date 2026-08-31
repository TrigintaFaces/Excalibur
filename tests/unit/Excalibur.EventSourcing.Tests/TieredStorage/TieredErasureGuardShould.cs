// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance;
using Excalibur.Dispatch;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Erasure;
using Excalibur.EventSourcing.TieredStorage;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using IEventStore = Excalibur.EventSourcing.IEventStore;

namespace Excalibur.EventSourcing.Tests.TieredStorage;

/// <summary>
/// Author≠implementer lock for the erasure-capability startup gate. The tiered decorator deliberately
/// refuses the <see cref="IEventStoreErasure"/> probe — it can erase the hot tier only, and the archived
/// range has no erase surface — so a host that composes cold-tier archival with event-store erasure has a
/// composition that can never honour a right-to-erasure request. That must be reported at host startup,
/// while the consumer can still change the composition, not at the first erasure request when a statutory
/// clock is already running.
/// </summary>
/// <remarks>
/// <para>
/// <b>SAFETY:</b> tiered storage + <c>UseEventStoreErasure</c> fails at startup validation
/// (<c>ValidateOnStart</c> → <see cref="OptionsValidationException"/>). <see cref="IStartupValidator"/> is
/// the host-free stand-in for host start — <c>IHost.StartAsync</c> runs this same validator.
/// </para>
/// <para>
/// <b>LIVENESS:</b> erasure WITHOUT tiered storage starts clean and the contributor still resolves.
/// Without this arm a guard that failed unconditionally would look green.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Feature", "TieredStorage")]
public sealed class TieredErasureGuardShould
{
    [Fact]
    public void FailAtHostStartup_WhenTieredStorageIsCombinedWithErasure()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddSingleton(A.Fake<IColdEventStore>());
        _ = services.AddExcaliburEventSourcing(b =>
        {
            _ = b.UseInMemory();
            _ = b.UseEventStoreErasure<SubjectHashIsAggregateIdMapping>();
            _ = b.UseTieredStorage(policy => policy.MaxAge = TimeSpan.FromDays(90));
        });

        using var provider = services.BuildServiceProvider();
        var validator = provider.GetRequiredService<IStartupValidator>();

        var thrown = Should.Throw<OptionsValidationException>(
            validator.Validate,
            "the tiered decorator cannot erase the archived range, so the composition must be rejected at host startup — not at the first right-to-erasure request.");

        thrown.Message.ShouldContain(
            nameof(TieredEventStoreDecorator),
            Case.Sensitive,
            "the failure must name the composed store the consumer has to change.");
    }

    [Fact]
    public void StartCleanly_WhenErasureWithoutTieredStorage()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddExcaliburEventSourcing(b =>
        {
            _ = b.UseInMemory();
            _ = b.UseEventStoreErasure<SubjectHashIsAggregateIdMapping>();
        });

        using var provider = services.BuildServiceProvider();

        Should.NotThrow(
            provider.GetRequiredService<IStartupValidator>().Validate,
            "an erasure host with no cold tier composes a store that answers the erasure probe, so the gate must stay silent.");
        provider.GetRequiredService<IErasureContributor>().ShouldNotBeNull();
    }

    private sealed class SubjectHashIsAggregateIdMapping : IAggregateDataSubjectMapping
    {
        public Task<IReadOnlyList<AggregateReference>> GetAggregatesForDataSubjectAsync(
            string dataSubjectIdHash,
            string? tenantId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AggregateReference>>(
                [new AggregateReference(dataSubjectIdHash, "Order")]);
    }
}
