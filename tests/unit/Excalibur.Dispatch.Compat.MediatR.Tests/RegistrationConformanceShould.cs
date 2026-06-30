// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Compat.MediatR.Tests;

/// <summary>
/// bd-49g2bz — author≠impl lock for source-gen registration fidelity: EC-2 (open-generic
/// handler registration) and EC-9 (value-type response resolves correctly / no-box on the
/// hot path). The "no reflection" / AOT-IL guarantee is enforced by the AOT gate (c37y1v);
/// this unit lock proves the open-generic + value-type *behavior* through the bridge.
/// Non-vacuous: RED until the request bridge (f4zy8k/g1r81z) lands (facade throws).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compat")]
public sealed class RegistrationConformanceShould
{
    private static IMediator BuildMediator()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDispatch();
        services.AddMediatRCompat(cfg => cfg.RegisterServicesFromAssembly(typeof(GenericQueryHandler<>).Assembly));
        return services.BuildServiceProvider().GetRequiredService<IMediator>();
    }

    [Fact] // EC-9: a value-type (int) response resolves type-safely through the generic Send.
    public async Task ResolveValueTypeResponse_WithoutBoxingOnTheHotPath()
    {
        var mediator = BuildMediator();

        var result = await mediator.Send(new NumberQuery());

        // Strongly-typed int result — the generic Send<int> path never boxes.
        result.ShouldBeOfType<int>();
        result.ShouldBe(42);
    }

    [Fact] // EC-2 / OS-1: open-generic handlers are NOT auto-bound (binding a closed bridge would
    // need MakeGenericType — AOT-hostile, violates NFR-1). Per OS-1 they are a *documented manual-
    // migration step*, surfaced as handler-not-found (not a silent miss). [Pending PM ruling on the
    // EC-2-vs-OS-1/NFR-1 conflict — see OPCOM; if PM rules open-generic must be supported, this flips.]
    public async Task NotAutoBindOpenGenericHandler_DocumentedManualStep()
    {
        var mediator = BuildMediator();

        await Should.ThrowAsync<HandlerNotFoundException>(
            async () => await mediator.Send(new GenericQuery<string> { Value = "echo" }));
    }
}
