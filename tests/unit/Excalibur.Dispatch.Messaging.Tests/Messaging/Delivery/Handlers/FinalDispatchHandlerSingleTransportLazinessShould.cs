// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery.Handlers;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Routing;
using Excalibur.Dispatch.Transport;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery.Handlers;

[Trait("Category", "Unit")]
[Trait("Component", "Dispatch.Core")]
[Trait("Pattern", "Regression")]
public sealed class FinalDispatchHandlerSingleTransportLazinessShould
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ResolveTheSuccessfulSingleBusOnlyOnUseAndReuseIt(bool owned)
    {
        var bus = A.Fake<IMessageBus>();
        A.CallTo(() => bus.PublishAsync(A<IDispatchEvent>._, A<IMessageContext>._, A<CancellationToken>._))
            .Returns(Task.CompletedTask);
        var provider = new ControlledProvider(bus) { Armed = true };
        var handler = CreateHandler(provider);
        provider.RemoteCalls.ShouldBe(0, "construction must inspect names without forcing a remote bus");
        var message = A.Fake<IDispatchEvent>();
        var context = CreateContext();
        using var owner = owned ? RetryOwnershipScope.Enter(message, context) : null;

        (await handler.HandleAsync(message, context, CancellationToken.None)).Succeeded.ShouldBeTrue();
        (await handler.HandleAsync(message, context, CancellationToken.None)).Succeeded.ShouldBeTrue();

        provider.RemoteCalls.ShouldBe(1);
        A.CallTo(() => bus.PublishAsync(message, context, A<CancellationToken>._)).MustHaveHappenedTwiceExactly();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task RetryTheSelectedProviderAfterAnAbsentBusOrOriginalFault(bool owned, bool throws)
    {
        var bus = A.Fake<IMessageBus>();
        A.CallTo(() => bus.PublishAsync(A<IDispatchEvent>._, A<IMessageContext>._, A<CancellationToken>._))
            .Returns(Task.CompletedTask);
        var fault = new ResolutionException();
        var provider = new ControlledProvider(bus);
        // The OLD constructor probes once. Keep that probe absent/unarmed so this compatibility
        // control does not consume its deliberately first-use refusal or throw during construction.
        var handler = CreateHandler(provider);
        provider.Armed = true;
        provider.RefuseNext = true;
        provider.Fault = throws ? fault : null;
        var message = A.Fake<IDispatchEvent>();
        var context = CreateContext();
        using var owner = owned ? RetryOwnershipScope.Enter(message, context) : null;

        if (throws)
        {
            Exception? observed = null;
            try
            {
                _ = await handler.HandleAsync(message, context, CancellationToken.None);
            }
            catch (Exception exception)
            {
                observed = exception;
            }

            observed.ShouldBeSameAs(fault);
        }
        else
        {
            var absent = await handler.HandleAsync(message, context, CancellationToken.None);
            absent.Succeeded.ShouldBeFalse();
            absent.ProblemDetails.ShouldNotBeNull().Title.ShouldBe("Routing failed");
        }

        provider.RemoteCalls.ShouldBe(1);
        (await handler.HandleAsync(message, context, CancellationToken.None)).Succeeded.ShouldBeTrue();
        provider.RemoteCalls.ShouldBe(2, "a prior absence/fault is not a permanent local cache entry");
        A.CallTo(() => bus.PublishAsync(message, context, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    private static FinalDispatchHandler CreateHandler(IMessageBusProvider provider) => new(
        provider,
        NullLogger<FinalDispatchHandler>.Instance,
        retryPolicy: null,
        new Dictionary<string, MessageBusOptions>());

    private static MessageContext CreateContext()
    {
        var context = new MessageContext();
        context.GetOrCreateRoutingFeature().RoutingDecision = RoutingDecision.Success("orders", ["orders"]);
        return context;
    }

    private sealed class ControlledProvider(IMessageBus selectedBus) : IMessageBusProvider
    {
        public bool Armed { get; set; }
        public bool RefuseNext { get; set; }
        public Exception? Fault { get; set; }
        public int RemoteCalls { get; private set; }

        public IEnumerable<string> GetAllMessageBusNames() => ["orders"];
        public IEnumerable<IMessageBus> GetAllMessageBuses() => [selectedBus];
        public IMessageBus? GetMessageBus(string name) => TryGet(name, out var found) ? found : null;
        public IMessageBus? GetRemoteMessageBus(string name) => GetMessageBus(name);

        public bool TryGet(string name, out IMessageBus? bus)
        {
            bus = null;
            if (!string.Equals(name, "orders", StringComparison.OrdinalIgnoreCase) || !Armed)
            {
                return false;
            }

            RemoteCalls++;
            if (RefuseNext)
            {
                RefuseNext = false;
                if (Fault is not null)
                {
                    throw Fault;
                }

                return false;
            }

            bus = selectedBus;
            return true;
        }
    }

    private sealed class ResolutionException : Exception;
}
