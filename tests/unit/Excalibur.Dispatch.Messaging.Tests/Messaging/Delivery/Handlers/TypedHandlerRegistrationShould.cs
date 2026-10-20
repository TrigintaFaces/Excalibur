// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Handlers;
using Excalibur.Dispatch.Messaging;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery.Handlers;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "FACTORY")]
public sealed class TypedHandlerRegistrationShould
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DispatchAllFourShapesBeforeOrAfterComposition(bool registerAfter)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        if (registerAfter) { services.AddDispatch(static _ => { }); }
        services.AddActionHandler<Command, CommandHandler>().ShouldBeSameAs(services);
        services.AddActionHandler<RegistrationQuery, int, QueryHandler>().ShouldBeSameAs(services);
        services.AddEventHandler<Notification, NotificationHandler>().ShouldBeSameAs(services);
        services.AddDocumentHandler<Document, DocumentHandler>().ShouldBeSameAs(services);
        if (!registerAfter) { services.AddDispatch(static _ => { }); }
        await using var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<IDispatcher>();
        for (var index = 0; index < 2; index++)
        {
            var command = new Command();
            var context = new MessageContext(command, provider);
            (await dispatcher.DispatchAsync(command, context, CancellationToken.None)).Succeeded.ShouldBeTrue();
            command.Observed.ShouldBeSameAs(context);
            command.Calls.ShouldBe(1);
            var query = new RegistrationQuery();
            context = new MessageContext(query, provider);
            var result = await dispatcher.DispatchAsync<RegistrationQuery, int>(query, context, CancellationToken.None);
            result.Succeeded.ShouldBeTrue();
            result.ReturnValue.ShouldBe(42);
            query.Observed.ShouldBeSameAs(context);
            query.Calls.ShouldBe(1);
            var notification = new Notification();
            context = new MessageContext(notification, provider);
            (await dispatcher.DispatchAsync(notification, context, CancellationToken.None)).Succeeded.ShouldBeTrue();
            notification.Observed.ShouldBeSameAs(context);
            notification.Calls.ShouldBe(1);
            var document = new Document();
            context = new MessageContext(document, provider);
            (await dispatcher.DispatchAsync(document, context, CancellationToken.None)).Succeeded.ShouldBeTrue();
            document.Observed.ShouldBeSameAs(context);
            document.Calls.ShouldBe(1);
        }
    }

    [Theory]
    [InlineData(ServiceLifetime.Transient)]
    [InlineData(ServiceLifetime.Scoped)]
    [InlineData(ServiceLifetime.Singleton)]
    public void PreserveRequestedConcreteLifetime(ServiceLifetime lifetime)
    {
        var services = new ServiceCollection().AddActionHandler<Command, CommandHandler>(lifetime);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        var handler = first.ServiceProvider.GetRequiredService<CommandHandler>();
        var repeated = first.ServiceProvider.GetRequiredService<CommandHandler>();
        var anotherScope = second.ServiceProvider.GetRequiredService<CommandHandler>();
        ReferenceEquals(handler, repeated).ShouldBe(lifetime != ServiceLifetime.Transient);
        ReferenceEquals(handler, anotherScope).ShouldBe(lifetime == ServiceLifetime.Singleton);
        // Implementation-type descriptors preserve the existing independent interface/concrete semantics.
        first.ServiceProvider.GetRequiredService<IActionHandler<Command>>().ShouldNotBeSameAs(handler);
    }

    [Fact]
    public async Task HonorAnExistingConcreteRegistrationForDispatch()
    {
        var expected = new CommandHandler();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(expected);
        services.AddActionHandler<Command, CommandHandler>(ServiceLifetime.Transient);
        services.AddDispatch(static _ => { });
        await using var provider = services.BuildServiceProvider();
        var message = new Command();
        var context = new MessageContext(message, provider);
        (await provider.GetRequiredService<IDispatcher>().DispatchAsync(message, context, CancellationToken.None)).Succeeded.ShouldBeTrue();
        expected.Context.ShouldBeSameAs(context);
        message.Calls.ShouldBe(1);
        provider.GetRequiredService<CommandHandler>().ShouldBeSameAs(expected);
    }

    [Fact]
    public async Task RetainMixedEventRegistrationsAndDeduplicateRepeatedHandlerTypes()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventHandler<Notification, NotificationHandler>();
        services.AddEventHandler<Notification, NotificationHandler>();
        services.AddTransient<SecondNotificationHandler>();
        services.AddTransient<IEventHandler<Notification>, SecondNotificationHandler>();
        services.AddDispatch(static _ => { });
        await using var provider = services.BuildServiceProvider();
        var message = new Notification();
        var context = new MessageContext(message, provider);
        (await provider.GetRequiredService<IDispatcher>().DispatchAsync(message, context, CancellationToken.None)).Succeeded.ShouldBeTrue();
        message.Calls.ShouldBe(1);
        message.SecondCalls.ShouldBe(1);
        message.Observed.ShouldBeSameAs(context);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreserveLastRegistrationForActions(bool typedLast)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        if (!typedLast) { services.AddActionHandler<Command, CommandHandler>(); }
        services.AddTransient<IActionHandler<Command>, OtherCommandHandler>();
        services.AddTransient<OtherCommandHandler>();
        if (typedLast) { services.AddActionHandler<Command, CommandHandler>(); }
        services.AddDispatch(static _ => { });
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IHandlerRegistry>().TryGetHandler(typeof(Command), out var entry).ShouldBeTrue();
        entry.HandlerType.ShouldBe(typedLast ? typeof(CommandHandler) : typeof(OtherCommandHandler));
    }

    [Fact]
    public void RejectInvalidArgumentsWithoutMutatingServices()
    {
        Should.Throw<ArgumentNullException>(() => TypedHandlerServiceCollectionExtensions.AddActionHandler<Command, CommandHandler>(null!));
        var services = new ServiceCollection();
        Should.Throw<ArgumentOutOfRangeException>(() => services.AddActionHandler<Command, CommandHandler>((ServiceLifetime)99));
        services.Count.ShouldBe(0);
    }

#pragma warning disable CA1034 // Public fixture types are required for normal DI activation.
    public abstract class ObservedMessage { public IMessageContext? Observed { get; set; } public int Calls { get; set; } }
    public sealed class Command : ObservedMessage, IDispatchAction;
    public sealed class RegistrationQuery : ObservedMessage, IDispatchAction<int>;
    public sealed class Notification : ObservedMessage, IDispatchEvent { public int SecondCalls { get; set; } }
    public sealed class Document : ObservedMessage, IDispatchDocument;
    public abstract class ContextHandler { public IMessageContext? Context { get; set; } }
    public sealed class CommandHandler : ContextHandler, IActionHandler<Command>
    {
        public Task HandleAsync(Command message, CancellationToken cancellationToken) { message.Observed = Context; message.Calls++; return Task.CompletedTask; }
    }
    public sealed class OtherCommandHandler : IActionHandler<Command>
    {
        public Task HandleAsync(Command message, CancellationToken cancellationToken) => Task.CompletedTask;
    }
    public sealed class QueryHandler : ContextHandler, IActionHandler<RegistrationQuery, int>
    {
        public Task<int> HandleAsync(RegistrationQuery message, CancellationToken cancellationToken) { message.Observed = Context; message.Calls++; return Task.FromResult(42); }
    }
    public sealed class NotificationHandler : ContextHandler, IEventHandler<Notification>
    {
        public Task HandleAsync(Notification message, CancellationToken cancellationToken) { message.Observed = Context; message.Calls++; return Task.CompletedTask; }
    }
    public sealed class SecondNotificationHandler : ContextHandler, IEventHandler<Notification>
    {
        public Task HandleAsync(Notification message, CancellationToken cancellationToken) { Context.ShouldBeSameAs(message.Observed); message.SecondCalls++; return Task.CompletedTask; }
    }
    public sealed class DocumentHandler : ContextHandler, IDocumentHandler<Document>
    {
        public Task HandleAsync(Document message, CancellationToken cancellationToken) { message.Observed = Context; message.Calls++; return Task.CompletedTask; }
    }
#pragma warning restore CA1034
}
