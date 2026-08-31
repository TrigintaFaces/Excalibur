// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing;
using Excalibur.Testing.Conformance;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Workflows.Tests;

/// <summary>
/// InMemory derivation of <see cref="WorkflowConformanceTestKit"/> — the fast, always-run non-vacuity proof
/// that the fault-injection scenario bodies exercise real durable-execution behaviour (crash-recovery +
/// exactly-once) over the shipping in-memory event store. The real-infra provider derivations
/// (SqlServer / Postgres / Mongo, <c>DockerAvailable.ShouldBeTrue</c>) are authored separately once their
/// host test project + fixtures are pinned (13ffsk co-own).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Component", "Workflows")]
public sealed class InMemoryWorkflowConformanceTests : WorkflowConformanceTestKit
{
    /// <summary>
    /// Returns a fresh in-memory event store as the durable journal. The store instance persists across the
    /// kit's two hosts (a simulated process restart) because the kit reuses this single instance.
    /// </summary>
    /// <returns>A fresh in-memory <see cref="IEventStore"/>.</returns>
    protected override IEventStore CreateEventStore()
    {
        var services = new ServiceCollection();
        services.AddInMemoryEventStore();
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredKeyedService<IEventStore>("inmemory");
    }

    [Fact]
    public override Task CrashMidStep_ResumesFromLastCompletedStep_ExactlyOnce() =>
        base.CrashMidStep_ResumesFromLastCompletedStep_ExactlyOnce();

    [Fact]
    public override Task DuplicateDelivery_AppliesEachActivityExactlyOnce() =>
        base.DuplicateDelivery_AppliesEachActivityExactlyOnce();

    [Fact]
    public override Task DelayedRestart_ResumesAndCompletesExactlyOnce() =>
        base.DelayedRestart_ResumesAndCompletesExactlyOnce();

    [Fact]
    public override Task ConformanceSuite_ShouldWireEveryArm() =>
    	base.ConformanceSuite_ShouldWireEveryArm();
}
