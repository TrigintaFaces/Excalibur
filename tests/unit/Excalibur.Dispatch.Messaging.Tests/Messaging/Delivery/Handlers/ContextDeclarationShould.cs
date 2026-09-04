// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Delivery.Handlers;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery.Handlers;

/// <summary>
/// The dispatcher skips publishing an ambient message context on the ultra-local fast path, and it
/// decides whether that is safe by asking whether the handler DECLARED that it reads the context.
/// A handler declares that two ways: a settable <see cref="IMessageContext"/> property, or an
/// <see cref="IMessageContextAccessor"/> constructor parameter. If either goes undetected the
/// handler is routed onto the no-ambient path and handed a silent null, so this is the check that
/// keeps the optimisation honest.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Dispatch.Core")]
public sealed class ContextDeclarationShould
{
    private sealed record Ping : IDispatchAction;

    /// <summary>Declares nothing: safe to run without an ambient context.</summary>
    private sealed class UndeclaredHandler : IActionHandler<Ping>
    {
        public Task HandleAsync(Ping action, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    /// <summary>Declares by constructor-injecting the accessor.</summary>
    private sealed class AccessorInjectingHandler(IMessageContextAccessor contextAccessor) : IActionHandler<Ping>
    {
        public Task HandleAsync(Ping action, CancellationToken cancellationToken)
        {
            _ = contextAccessor.MessageContext;
            return Task.CompletedTask;
        }
    }

    /// <summary>Declares by exposing a settable context property.</summary>
    private sealed class ContextPropertyHandler : IActionHandler<Ping>
    {
        public IMessageContext? MessageContext { get; set; }

        public Task HandleAsync(Ping action, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [Fact]
    public void TreatAccessorInjectionAsDeclaringContext()
    {
        // Safety arm. Without this the handler takes the fast path, which publishes no ambient
        // context, and every read of contextAccessor.MessageContext inside it returns null --
        // silently, with no exception to notice.
        HandlerActivator.RequiresContextInjection(typeof(AccessorInjectingHandler))
            .ShouldBeTrue(
                "a handler that injects IMessageContextAccessor has declared it reads the context, " +
                "so it must not be routed onto the fast path that publishes none");
    }

    [Fact]
    public void TreatAContextPropertyAsDeclaringContext()
    {
        // Regression guard on the pre-existing detection, so a change to the accessor rule cannot
        // quietly break the property rule.
        HandlerActivator.RequiresContextInjection(typeof(ContextPropertyHandler))
            .ShouldBeTrue("a settable IMessageContext property has always been a declaration");
    }

    [Fact]
    public void NotTreatAnUndeclaredHandlerAsRequiringContext()
    {
        // Liveness arm: without it, a check that simply returned true would pass both arms above
        // and silently disable the optimisation for everyone.
        HandlerActivator.RequiresContextInjection(typeof(UndeclaredHandler))
            .ShouldBeFalse(
                "a handler that declares no context must stay eligible for the fast path -- " +
                "otherwise the ambient push is never skipped and the optimisation is inert");
    }
}
