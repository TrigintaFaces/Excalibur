// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Transport.RabbitMQ;

using RabbitMQ.Client;

namespace Excalibur.Dispatch.Transport.Tests.RabbitMQ;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
[Trait("Pattern", "Regression")]
public sealed class RabbitMqDispatcherLazinessShould
{
    [Fact]
    public async Task ResolveDispatcherAndDispatchLocallyWithoutOpeningTheSingleRemoteConnection()
    {
        var factory = A.Fake<IConnectionFactory>();
        var fault = new ConnectionAttemptException();
        A.CallTo(() => factory.CreateConnectionAsync(A<CancellationToken>._))
            .Returns(Task.FromException<IConnection>(fault));
        var counter = new HandlerCounter();
        var services = CreateServices(factory, counter);
        await using var provider = services.BuildServiceProvider();

        var dispatcher = provider.GetRequiredService<IDispatcher>();
        A.CallTo(() => factory.CreateConnectionAsync(A<CancellationToken>._)).MustNotHaveHappened();
        var result = await dispatcher.DispatchAsync(new LocalAction(), new MessageContext(), CancellationToken.None);

        result.Succeeded.ShouldBeTrue();
        counter.Calls.ShouldBe(1);
        A.CallTo(() => factory.CreateConnectionAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task ContactTheSelectedRemoteFactoryOnlyWhenDispatchActuallyUsesIt()
    {
        var factory = A.Fake<IConnectionFactory>();
        var fault = new ConnectionAttemptException();
        A.CallTo(() => factory.CreateConnectionAsync(A<CancellationToken>._))
            .Returns(Task.FromException<IConnection>(fault));
        var counter = new HandlerCounter();
        var services = CreateServices(factory, counter);
        await using var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<IDispatcher>();
        A.CallTo(() => factory.CreateConnectionAsync(A<CancellationToken>._)).MustNotHaveHappened();

        Exception? observed = null;
        try
        {
            _ = await dispatcher.DispatchAsync(new RemoteEvent(), new MessageContext(), CancellationToken.None);
        }
        catch (Exception exception)
        {
            observed = exception;
        }

        observed.ShouldBeSameAs(fault);
        counter.Calls.ShouldBe(0);
        A.CallTo(() => factory.CreateConnectionAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    private static ServiceCollection CreateServices(IConnectionFactory factory, HandlerCounter counter)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(counter);
        services.AddSingleton<IActionHandler<LocalAction>, LocalHandler>();
        services.AddDispatch(dispatch =>
        {
            dispatch.WithSerialization(s => s.RegisterSystemTextJson().UseSystemTextJson());
            dispatch.AddHandlersFromAssembly(typeof(RabbitMqDispatcherLazinessShould).Assembly);
            dispatch.UseRouting(routing =>
            {
                routing.Transport.Route<RemoteEvent>().To("orders");
                routing.Endpoints.Route<RemoteEvent>().To("orders");
            });
        });
        services.AddRabbitMQTransport("orders", rabbit => rabbit.ConnectionString("amqps://app:secret@broker.invalid/"));
        services.AddSingleton(factory);
        return services;
    }

    internal sealed class LocalAction : IDispatchAction;
    internal sealed class RemoteEvent : IIntegrationEvent;
    internal sealed class HandlerCounter
    {
        public int Calls { get; set; }
    }

    internal sealed class LocalHandler(HandlerCounter counter) : IActionHandler<LocalAction>
    {
        public Task HandleAsync(LocalAction action, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(action);
            cancellationToken.ThrowIfCancellationRequested();
            counter.Calls++;
            return Task.CompletedTask;
        }
    }

    private sealed class ConnectionAttemptException : Exception;
}
