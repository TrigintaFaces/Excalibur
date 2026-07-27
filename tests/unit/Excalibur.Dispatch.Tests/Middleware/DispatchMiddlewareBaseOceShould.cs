// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Middleware;

namespace Excalibur.Dispatch.Tests.Middleware;

/// <summary>
/// qkvslo (REVIEW_CODE carve-out) — author≠impl lock (TestsDeveloper) for the
/// <see cref="DispatchMiddlewareBase"/> cooperative-cancellation contract. A handler-thrown
/// <see cref="OperationCanceledException"/> is INTENTIONAL control flow and MUST propagate UNWRAPPED —
/// never wrapped in the diagnostic <see cref="InvalidOperationException"/> — or callers can no longer
/// catch OCE / observe cancellation.
/// </summary>
/// <remarks>
/// NON-VACUITY: removing the <c>ex is OperationCanceledException</c> carve-out routes the OCE through
/// <c>OnErrorAsync</c> (default null) → <c>throw new InvalidOperationException(...)</c>, so
/// <c>Should.ThrowAsync&lt;OperationCanceledException&gt;</c> would receive an
/// <see cref="InvalidOperationException"/> instead → RED.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Feature", "Middleware")]
public sealed class DispatchMiddlewareBaseOceShould
{
    [Fact]
    public async Task PropagateOperationCanceledException_Unwrapped_WhenHandlerThrowsOce()
    {
        var middleware = new PassThroughMiddleware();
        DispatchRequestDelegate handlerThrowsOce =
            (_, _, _) => throw new OperationCanceledException("handler observed cancellation");

        var thrown = await Should.ThrowAsync<OperationCanceledException>(async () => await middleware.InvokeAsync(
            A.Fake<IDispatchMessage>(),
            A.Fake<IMessageContext>(),
            handlerThrowsOce,
            CancellationToken.None));

        // The OCE family (incl. TaskCanceledException) must reach the caller intact — NOT wrapped in the
        // diagnostic InvalidOperationException. (Should.ThrowAsync above is the non-vacuous proof: a removed
        // carve-out wraps the OCE in InvalidOperationException, which is NOT an OperationCanceledException → RED.)
        _ = thrown.ShouldBeAssignableTo<OperationCanceledException>(
            "qkvslo: a handler-thrown OperationCanceledException must propagate unwrapped (cooperative cancellation).");
        thrown.ShouldNotBeOfType<InvalidOperationException>();
    }

    // Empty concrete subclass — every DispatchMiddlewareBase member is virtual with a default; the default
    // ProcessAsync just invokes the next delegate, so the handler-thrown OCE reaches the base's catch.
    private sealed class PassThroughMiddleware : DispatchMiddlewareBase;
}
