// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Compat.MediatR.Tests;

/// <summary>
/// bd-1hrrf1 — author≠impl conformance lock for AC-6 (CreateStream yields the handler's
/// sequence) via the C2 ISender.CreateStream entry point, plus the stream arm of EC-6
/// (cancellation). Binds the SA-ruled bridge (msg 17838): IStreamRequest&lt;T&gt; gets a
/// generated IDispatchDocument wrapper → IStreamingDispatcher. Non-vacuous: RED until
/// 3qlfwp wires the stream bridge (facade throws NotImplementedException).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compat")]
public sealed class StreamConformanceShould
{
    private static IMediator BuildMediator()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDispatch();
        services.AddMediatRCompat(cfg => cfg.RegisterServicesFromAssembly(typeof(CounterHandler).Assembly));
        return services.BuildServiceProvider().GetRequiredService<IMediator>();
    }

    [Fact] // AC-6: the async sequence yields 1,2,3 in order.
    public async Task YieldHandlerSequence_WhenCreateStreamIsCalled()
    {
        var mediator = BuildMediator();

        var results = new List<int>();
        await foreach (var value in mediator.CreateStream(new Counter()))
        {
            results.Add(value);
        }

        results.ShouldBe([1, 2, 3]);
    }

    [Fact] // EC-6 (stream arm): a pre-cancelled token surfaces OperationCanceledException.
    public async Task SurfaceOperationCanceled_WhenTokenAlreadyCancelled()
    {
        var mediator = BuildMediator();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in mediator.CreateStream(new Counter(), cts.Token))
            {
                // draining — cancellation must surface before/while enumerating.
            }
        });
    }
}
