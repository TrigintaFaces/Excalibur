// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.ZeroAlloc;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Tests.Messaging.ZeroAlloc;

[Trait("Category", "Unit")]
[Trait("Component", "Performance")]
[Trait("Pattern", "Contract")]
public sealed class PooledContextMetadataShould
{
    [Fact]
    public async Task KeepMetadataDeferredUntilItemsIsRequested()
    {
        // A fresh thread has no contexts retained by earlier tests in the thread-local pool.
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                using var provider = new ServiceCollection().BuildServiceProvider();
                var pool = new MessageContextPool(provider);
                var context = pool.Rent();
                try
                {
                    var items = typeof(MessageContext).GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic)!;
                    items.GetValue(context).ShouldBeNull();
                    context.GetMessageType().ShouldBeNull();
                    context.SetMessageType("query");
                    context.MarkMessageTypeAsRoutingDefault();
                    context.GetMessageType().ShouldBe("query");
                    context.IsMessageTypeRoutingDefault().ShouldBeTrue();
                    context.GetItem<string>("__MessageType").ShouldBe("query");
                    context.ContainsItem("__MessageType").ShouldBeTrue();
                    context.GetItem("__MessageType", 42).ShouldBe(42);
                    context.GetItem("absent", "fallback").ShouldBe("fallback");
                    context.SetMessageType(null);
                    context.ContainsItem("__MessageType").ShouldBeTrue();
                    context.GetItem("__MessageType", "fallback").ShouldBe("fallback");
                    context.RemoveItem("__MessageType");
                    context.ContainsItem("__MessageType").ShouldBeFalse();
                    context.IsMessageTypeRoutingDefault().ShouldBeTrue();
                    context.SetMessageType("query");
                    context.RemoveItem("__MessageTypeIsRoutingDefault");
                    context.IsMessageTypeRoutingDefault().ShouldBeFalse();
                    context.GetMessageType().ShouldBe("query");
                    context.MarkMessageTypeAsRoutingDefault();
                    items.GetValue(context).ShouldBeNull();

                    context.Items["__MessageType"].ShouldBe("query");
                    context.Items["__MessageTypeIsRoutingDefault"].ShouldBe(true);
                    context.Items.Remove("__MessageType");
                    context.GetMessageType().ShouldBeNull();
                    context.Items["__MessageTypeIsRoutingDefault"] = false;
                    context.IsMessageTypeRoutingDefault().ShouldBeFalse();
                    context.SetMessageType(null);
                    context.Items.ContainsKey("__MessageType").ShouldBeTrue();
                    context.Items["__MessageType"].ShouldBeNull();
                }
                finally
                {
                    pool.ReturnToPool(context);
                }

                var recycled = pool.Rent();
                try
                {
                    recycled.ShouldBeSameAs(context);
                    recycled.GetMessageType().ShouldBeNull();
                    recycled.IsMessageTypeRoutingDefault().ShouldBeFalse();
                    recycled.Items.ShouldBeEmpty();
                }
                finally
                {
                    pool.ReturnToPool(recycled);
                }

                completion.SetResult();
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        }) { IsBackground = true };
        thread.Start();
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(30));
    }
}
