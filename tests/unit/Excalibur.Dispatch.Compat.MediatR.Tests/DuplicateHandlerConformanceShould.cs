// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compat.MediatR.Tests.DupFixtures;

namespace Excalibur.Dispatch.Compat.MediatR.Tests;

/// <summary>
/// bd-nvzo4h — author≠impl lock for AC-8 (FR-8): when more than one IRequestHandler is
/// registered for a single request type within the registered-assembly scope, registration
/// fails fast with a diagnostic naming the duplicate (97kfev). Per SA C1 the dup-fail-fast is
/// PER-CONTAINER over the RegisterServicesFromAssembly set, so the duplicate handlers live in a
/// dedicated fixture assembly (DupFixtures) registered ONLY here — it never poisons other tests.
/// Non-vacuous: asserts the specific DuplicateRequestHandlerException (distinct from the
/// HandlerNotFoundException that the empty-registry path throws). RED until 97kfev wires.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compat")]
public sealed class DuplicateHandlerConformanceShould
{
    [Fact] // AC-8: two handlers for one request type -> fail-fast naming the duplicate.
    public void FailFast_WhenARequestTypeHasTwoHandlers()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        // Registration of the dup-fixture assembly must fail fast (per-container dup-fail-fast).
        var ex = Should.Throw<DuplicateRequestHandlerException>(() =>
        {
            services.AddMediatRCompat(cfg =>
                cfg.RegisterServicesFromAssembly(typeof(DupFixtureMarker).Assembly));
            using var provider = services.BuildServiceProvider();
            // Resolve to force any deferred validation/registry population to run.
            _ = provider.GetService<IMediator>();
        });

        // Diagnostic must name the duplicated request type (AC-8 "naming the duplicate").
        ex.Message.ShouldContain(nameof(DupRequest));
    }
}
