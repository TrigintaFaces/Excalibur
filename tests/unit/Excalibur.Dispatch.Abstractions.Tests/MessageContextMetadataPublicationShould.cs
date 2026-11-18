// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Abstractions.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Abstractions")]
[Trait("Pattern", "Concurrency")]
public sealed class MessageContextMetadataPublicationShould
{
    [Fact]
    public async Task PreserveDeferredMetadataDuringConcurrentFirstMaterialization()
    {
        const int iterations = 20000;
        using var barrier = new Barrier(2);
        MessageContext? current = null;
        var missing = 0;
        var writer = Task.Factory.StartNew(() =>
        {
            for (var i = 0; i < iterations; i++)
            {
                var context = new MessageContext();
                context.SetMessageType("query");
                context.MarkMessageTypeAsRoutingDefault();
                current = context;
                if (!barrier.SignalAndWait(TimeSpan.FromSeconds(10)))
                {
                    throw new TimeoutException("Reader did not reach start barrier.");
                }

                _ = context.Items;
                if (!barrier.SignalAndWait(TimeSpan.FromSeconds(10)))
                {
                    throw new TimeoutException("Reader did not reach completion barrier.");
                }
            }
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        var reader = Task.Factory.StartNew(() =>
        {
            for (var i = 0; i < iterations; i++)
            {
                if (!barrier.SignalAndWait(TimeSpan.FromSeconds(10)))
                {
                    throw new TimeoutException("Writer did not reach start barrier.");
                }

                var context = current!;
                for (var read = 0; read < 16; read++)
                {
                    if (context.GetMessageType() != "query" || !context.IsMessageTypeRoutingDefault())
                    {
                        missing++;
                    }
                }

                if (!barrier.SignalAndWait(TimeSpan.FromSeconds(10)))
                {
                    throw new TimeoutException("Writer did not reach completion barrier.");
                }
            }
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

        await Task.WhenAll(writer, reader);
        missing.ShouldBe(0);
    }
}
