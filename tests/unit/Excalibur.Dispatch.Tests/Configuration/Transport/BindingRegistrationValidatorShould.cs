// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Configuration.Transport;
using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Tests.Configuration.Transport;

/// <summary>
/// bd-dta2vt (S859) — regression lock for the binding-aware, default-on, fail-loud transport validation
/// (already delivered by <see cref="BindingRegistrationValidator"/>; this is a test-only guard, no impl change).
/// </summary>
/// <remarks>
/// The load-bearing product invariant (CLAUDE.md "simple by default / sensible defaults without configuration"):
/// the pure in-process / MediatR-replacement app — handlers, NO transport, zero config — MUST start clean. The
/// validator fires fail-loud ONLY when a binding explicitly references a transport that was never registered;
/// it must NEVER false-fail the no-binding app, and must clear once the referenced transport is wired. These
/// three cases guard against either regression direction (a silently-degraded guard that stops failing on an
/// unwired binding, OR an over-eager guard that breaks the simple app). Non-vacuous: a validator mutated to
/// always-Succeed reds case 2; always-Fail reds cases 1 and 3.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Feature", "Transport")]
public sealed class BindingRegistrationValidatorShould
{
    private static BindingRegistrationValidator CreateValidator(
        TransportBindingRegistry bindingRegistry,
        ITransportRegistry transportRegistry) =>
        new(bindingRegistry, transportRegistry);

    [Fact]
    public void Succeed_WhenNoBindingReferencesATransport()
    {
        // Arrange — the simple in-process app: handlers only, no binding, no transport. (The inviolable case.)
        using var bindingRegistry = new TransportBindingRegistry();
        var transportRegistry = A.Fake<ITransportRegistry>();

        // Act
        var result = CreateValidator(bindingRegistry, transportRegistry)
            .Validate(name: null, new BindingRegistrationValidationOptions());

        // Assert — MUST start clean; a false-fail here breaks the MediatR-replacement scenario.
        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void FailLoud_WhenBindingReferencesAnUnregisteredTransport()
    {
        // Arrange — a binding explicitly expects "rabbitmq", but nobody wired it.
        using var bindingRegistry = new TransportBindingRegistry();
        bindingRegistry.RegisterPendingTransportReference("rabbitmq");
        var transportRegistry = A.Fake<ITransportRegistry>();
        A.CallTo(() => transportRegistry.GetTransportAdapter("rabbitmq")).Returns(null);

        // Act
        var result = CreateValidator(bindingRegistry, transportRegistry)
            .Validate(null, new BindingRegistrationValidationOptions());

        // Assert — fail-loud at startup naming the missing transport, never a silent non-delivery.
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldNotBeNull();
        result.FailureMessage.ShouldContain("rabbitmq");
        result.FailureMessage.ShouldContain("not registered");
    }

    [Fact]
    public void Succeed_WhenTheReferencedTransportIsRegistered()
    {
        // Arrange — the binding referenced "rabbitmq" at binding time; a later AddRabbitMQTransport wired it.
        using var bindingRegistry = new TransportBindingRegistry();
        bindingRegistry.RegisterPendingTransportReference("rabbitmq");
        var transportRegistry = A.Fake<ITransportRegistry>();
        A.CallTo(() => transportRegistry.GetTransportAdapter("rabbitmq")).Returns(A.Fake<ITransportAdapter>());

        // Act
        var result = CreateValidator(bindingRegistry, transportRegistry)
            .Validate(null, new BindingRegistrationValidationOptions());

        // Assert — the pending reference resolved against the live registry; no false-fail.
        result.Succeeded.ShouldBeTrue();
    }
}
