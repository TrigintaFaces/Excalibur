// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

namespace Excalibur.Saga.Tests.DependencyInjection;

/// <summary>
/// Verifies that advanced saga interfaces were internalized per bd-uu6j5a (ADR-333 post-cleanup).
/// These interfaces are implementation details not intended for consumer use.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga.DependencyInjection")]
public sealed class SagaInterfaceInternalizationShould
{
    private static readonly Assembly SagaAssembly = typeof(SagaOptions).Assembly;



    [Fact]
    public void NotExposeAnyDeletedModelBInterfaces()
    {
        // Verify Model B interfaces are fully gone (ADR-333)
        var deletedTypes = new[]
        {
            "Excalibur.Saga.ISagaDefinition",
            "Excalibur.Saga.ISagaStep",
            "Excalibur.Saga.ISagaContext",
            "Excalibur.Saga.ISagaRetryPolicy",
            "Excalibur.Saga.ISagaOrchestrator",
            "Excalibur.Saga.ISagaStateStore",
            "Excalibur.Saga.ISagaDefinitionLifecycle",
        };

        foreach (var typeName in deletedTypes)
        {
            var type = SagaAssembly.GetType(typeName);
            type.ShouldBeNull($"{typeName} should have been deleted with Model B (ADR-333)");
        }
    }
}
