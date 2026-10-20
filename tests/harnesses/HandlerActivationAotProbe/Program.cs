// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Handlers;
using Excalibur.Dispatch.Messaging;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Tests.Harnesses.NativeActivation;

internal static class Program
{
    private static int _checks;

    public static async Task Main()
    {
        Check(!RuntimeFeature.IsDynamicCodeSupported, "Probe must execute as actual NativeAOT");
        var property = new PropertyHandler();
        var privateSetter = new PrivateSetterHandler();
        var inherited = new InheritedHandler();
        var aware = new AwareHandler();
        var registered = new RegisteredHandler();
        var late = new LateHandler();
        var unsupported = new UnsupportedHandler();
        var throwing = new ThrowingHandler();
        var shadow = new ShadowHandler();
        var multiple = new MultipleHandler();
        var dependency = new Dependency();
        using var provider = new ServiceCollection().AddSingleton(property).AddSingleton(privateSetter).AddSingleton(inherited)
            .AddSingleton(aware).AddSingleton(registered).AddSingleton(late).AddSingleton(unsupported).AddSingleton(throwing)
            .AddSingleton<BaseHandler>(shadow).AddSingleton(multiple).AddSingleton(dependency).BuildServiceProvider();
        var first = new MessageContext();
        var second = new MessageContext();
        Check(ReferenceEquals(Activate<PropertyHandler>(provider, first), property) && ReferenceEquals(property.Context, first), "Plain preserved property");
        Activate<PropertyHandler>(provider, second);
        Check(ReferenceEquals(property.Context, second) && property.SetterCalls == 2, "Repeated binding replaces current context once");
        Check(ReferenceEquals(Activate<PrivateSetterHandler>(provider, first).Context, first), "Private setter of public property");
        Check(ReferenceEquals(Activate<InheritedHandler>(provider, first).Context, first), "Inherited property");

        HandlerActivatorRegistry.RegisterContextSetter<AwareHandler>((_, _) => throw new InvalidOperationException("Interface must win"));
        Activate<AwareHandler>(provider, first);
        Check(ReferenceEquals(aware.Bound, first) && aware.Calls == 1 && aware.PropertyContext is null, "Interface precedence");
        HandlerActivatorRegistry.RegisterContextSetter<RegisteredHandler>((handler, context) => handler.Bound = context);
        Activate<RegisteredHandler>(provider, first);
        Check(ReferenceEquals(registered.Bound, first) && registered.Context is null, "Registered setter precedence");

        Activate<LateHandler>(provider, first);
        Check(late.Bound is null, "Cached absence of property");
        HandlerActivatorRegistry.RegisterContextSetter<LateHandler>((handler, context) => { handler.Bound = context; handler.Version = 1; });
        Activate<LateHandler>(provider, first);
        Check(ReferenceEquals(late.Bound, first) && late.Version == 1, "Late registered setter after empty plan");
        HandlerActivatorRegistry.RegisterContextSetter<LateHandler>((handler, context) => { handler.Bound = context; handler.Version = 2; });
        Activate<LateHandler>(provider, second);
        Check(ReferenceEquals(late.Bound, second) && late.Version == 2, "Replacement registered setter");

        // This type has no generated context setter: it is a plain service, not a discovered message handler.
        Activate<PropertyHandler>(provider, first);
        HandlerActivatorRegistry.RegisterContextSetter<PropertyHandler>((handler, context) => handler.RegisteredContext = context);
        Activate<PropertyHandler>(provider, second);
        Check(ReferenceEquals(property.Context, first) && ReferenceEquals(property.RegisteredContext, second), "Late registry overrides cached property");

        UnsupportedHandler.StaticContext = first;
        Activate<UnsupportedHandler>(provider, second);
        Check(ReferenceEquals(UnsupportedHandler.StaticContext, first) && unsupported.ConcreteContext is null && unsupported.Other is null, "Static, indexer, readonly and wrong type excluded");
        Activate<BaseHandler>(provider, first);
        Check(ReferenceEquals(((BaseHandler)shadow).Context, first) && shadow.Context is null, "Requested type defines preserved properties");
        Activate<MultipleHandler>(provider, first);
        Check(ReferenceEquals(multiple.First, first) ^ ReferenceEquals(multiple.Second, first), "Only one eligible property bound");
        var constructed = Activate<ConstructorHandler>(provider, first);
        Check(ReferenceEquals(constructed.Dependency, dependency) && ReferenceEquals(constructed.Context, first), "Unregistered constructor injection and property binding");
        try { Activate<ThrowingHandler>(provider, first);throw new InvalidOperationException("Throwing setter was ignored"); }
        catch (InvalidOperationException exception) when (ReferenceEquals(exception, throwing.Failure)) { _checks++; }

        // A fresh transient and context on every iteration exercises the shared cached metadata plan.
        using var transientProvider = new ServiceCollection().AddTransient<TransientHandler>().BuildServiceProvider();
        Parallel.For(0, 64, _ =>
        {
            var context = new MessageContext();
            var handler = Activate<TransientHandler>(transientProvider, context);
            Check(ReferenceEquals(handler.Context, context) && handler.SetterCalls == 1, "Concurrent transient context binding");
        });
        await VerifyFullDispatchAsync().ConfigureAwait(false);
        await VerifyLateDispatchRegistrationAsync().ConfigureAwait(false);
        Console.WriteLine($"NativeAOT handler activation passed: checks={_checks}, dynamicCodeSupported={RuntimeFeature.IsDynamicCodeSupported}");
    }

    private static async Task VerifyFullDispatchAsync()
    {
        // Explicit closed registrations avoid generated context setters masking the property fallback.
        Check(!HandlerActivatorRegistry.HasContextSetter(typeof(DispatchPropertyHandler)), "No generated action context setter");
        Check(!HandlerActivatorRegistry.HasContextSetter(typeof(DispatchQueryHandler)), "No generated query context setter");
        Check(!HandlerActivatorRegistry.HasContextSetter(typeof(DispatchEventHandler)), "No generated event context setter");
        Check(!HandlerActivatorRegistry.HasContextSetter(typeof(SecondDispatchEventHandler)), "No generated second event context setter");
        Check(!HandlerActivatorRegistry.HasContextSetter(typeof(DispatchDocumentHandler)), "No generated document context setter");
        HandlerInvokerRegistry.RegisterInvoker<DispatchPropertyHandler, DispatchCommand>(
            static (handler, message, token) => handler.HandleAsync(message, token));
        HandlerInvokerRegistry.RegisterInvoker<DispatchQueryHandler, DispatchQuery, int>(
            static (handler, message, token) => handler.HandleAsync(message, token));
        HandlerInvokerRegistry.RegisterInvoker<DispatchEventHandler, DispatchEvent>(
            static (handler, message, token) => handler.HandleAsync(message, token));
        HandlerInvokerRegistry.RegisterInvoker<SecondDispatchEventHandler, DispatchEvent>(
            static (handler, message, token) => handler.HandleAsync(message, token));
        HandlerInvokerRegistry.RegisterInvoker<DispatchDocumentHandler, DispatchDocument>(
            static (handler, message, token) => handler.HandleAsync(message, token));
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddActionHandler<DispatchCommand, DispatchPropertyHandler>(ServiceLifetime.Singleton);
        services.AddAotEventSerializer(ProbeJsonContext.Default);
        services.AddDispatch(static _ => { });
        // The deferred descriptor census must also see typed registrations after AddDispatch.
        services.AddActionHandler<DispatchQuery, int, DispatchQueryHandler>(ServiceLifetime.Singleton);
        services.AddEventHandler<DispatchEvent, DispatchEventHandler>(ServiceLifetime.Singleton);
        services.AddEventHandler<DispatchEvent, SecondDispatchEventHandler>(ServiceLifetime.Singleton);
        services.AddDocumentHandler<DispatchDocument, DispatchDocumentHandler>(ServiceLifetime.Singleton);
        await using var provider = services.BuildServiceProvider();
        Check(provider.GetRequiredService<IHandlerActivator>() is AotHandlerActivator, "Native default activator selection");
        var dispatcher = provider.GetRequiredService<IDispatcher>();
        var handler = provider.GetRequiredService<DispatchPropertyHandler>();
        for (var index = 0; index < 3; index++)
        {
            var command = new DispatchCommand();
            var context = new MessageContext(command, provider);
            command.ExpectedContext = context;
            var result = await dispatcher.DispatchAsync(command, context, CancellationToken.None).ConfigureAwait(false);
            Check(result.Succeeded && command.Calls == 1 && ReferenceEquals(handler.Context, context),
                "Full dispatch binds fresh context on a singleton property handler");
            var query = new DispatchQuery();
            var queryContext = new MessageContext(query, provider);
            query.ExpectedContext = queryContext;
            var queryResult = await dispatcher.DispatchAsync<DispatchQuery, int>(query, queryContext, CancellationToken.None).ConfigureAwait(false);
            Check(queryResult.Succeeded && queryResult.ReturnValue == 42 && query.Calls == 1, "Typed response and query context");
            var notification = new DispatchEvent();
            var eventContext = new MessageContext(notification, provider);
            notification.ExpectedContext = eventContext;
            var eventResult = await dispatcher.DispatchAsync(notification, eventContext, CancellationToken.None).ConfigureAwait(false);
            Check(eventResult.Succeeded && notification.FirstCalls == 1 && notification.SecondCalls == 1, "Event fan-out exactly once per handler");
            var document = new DispatchDocument();
            var documentContext = new MessageContext(document, provider);
            document.ExpectedContext = documentContext;
            var documentResult = await dispatcher.DispatchAsync(document, documentContext, CancellationToken.None).ConfigureAwait(false);
            Check(documentResult.Succeeded && document.Calls == 1, "Document context and invocation");
        }
        Check(handler.SetterCalls == 3, "Singleton dispatch never bypasses context binding");
        Check(provider.GetRequiredService<DispatchQueryHandler>().SetterCalls == 3, "Query setter once per dispatch");
        Check(provider.GetRequiredService<DispatchEventHandler>().SetterCalls == 3 && provider.GetRequiredService<SecondDispatchEventHandler>().SetterCalls == 3,
            "Event setters once per handler per dispatch");
        Check(provider.GetRequiredService<DispatchDocumentHandler>().SetterCalls == 3, "Document setter once per dispatch");
    }

    private static async Task VerifyLateDispatchRegistrationAsync()
    {
        Check(!HandlerActivatorRegistry.HasContextSetter(typeof(LateDispatchHandler)), "Late dispatch starts without a registered setter");
        HandlerInvokerRegistry.RegisterInvoker<LateDispatchHandler, LateDispatchCommand>(
            static (handler, message, token) => handler.HandleAsync(message, token));
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddActionHandler<LateDispatchCommand, LateDispatchHandler>(ServiceLifetime.Singleton);
        services.AddAotEventSerializer(ProbeJsonContext.Default);
        services.AddDispatch(static _ => { });
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var dispatcher = provider.GetRequiredService<IDispatcher>();
        var handler = provider.GetRequiredService<LateDispatchHandler>();
        for (var version = 0; version <= 2; version++)
        {
            if (version > 0)
            {
                var publishedVersion = version;
                HandlerActivatorRegistry.RegisterContextSetter<LateDispatchHandler>((instance, context) => instance.Bind(context, publishedVersion));
            }
            var message = new LateDispatchCommand();
            var context = new MessageContext(message, provider);
            message.ExpectedContext = version == 0 ? null : context;
            message.ExpectedVersion = version;
            var result = await dispatcher.DispatchAsync(message, context, CancellationToken.None).ConfigureAwait(false);
            Check(result.Succeeded && message.Calls == 1, "Late registration dispatch executes exactly once");
        }
        Check(handler.SetterCalls == 2, "First and replacement native dispatch setters execute once each");
    }

    public sealed class LateDispatchCommand : IDispatchAction
    {
        public IMessageContext? ExpectedContext { get; set; }
        public int ExpectedVersion { get; set; }
        public int Calls { get; set; }
    }

    public sealed class LateDispatchHandler : IActionHandler<LateDispatchCommand>
    {
        private IMessageContext? _bound;
        private int _version;
        public int SetterCalls { get; private set; }
        public void Bind(IMessageContext context, int version) { _bound = context; _version = version; SetterCalls++; }
        public Task HandleAsync(LateDispatchCommand message, CancellationToken cancellationToken)
        {
            Check(ReferenceEquals(_bound, message.ExpectedContext) && _version == message.ExpectedVersion,
                "Warmed native dispatcher observes first and replacement setter with the caller context");
            message.Calls++;
            return Task.CompletedTask;
        }
    }

    private static void VerifyDispatchContext(IMessageContext? actual, IMessageContext? expected)
    {
        if (expected is null || !ReferenceEquals(actual, expected))
        {
            throw new InvalidOperationException("Dispatch did not bind the message's context.");
        }
    }

    public sealed class DispatchQuery : IDispatchAction<int> { public IMessageContext? ExpectedContext { get; set; } public int Calls { get; set; } }
    public sealed class DispatchEvent : IDispatchEvent
    {
        public IMessageContext? ExpectedContext { get; set; }
        public int FirstCalls { get; set; }
        public int SecondCalls { get; set; }
    }
    public sealed class DispatchDocument : IDispatchDocument { public IMessageContext? ExpectedContext { get; set; } public int Calls { get; set; } }
    public sealed class DispatchQueryHandler : IActionHandler<DispatchQuery, int>
    {
        private IMessageContext? _context;
        public int SetterCalls { get; private set; }
        public IMessageContext? Context { get => _context; set { _context = value; SetterCalls++; } }
        public Task<int> HandleAsync(DispatchQuery message, CancellationToken cancellationToken)
        {
            VerifyDispatchContext(Context, message.ExpectedContext);
            message.Calls++;
            return Task.FromResult(42);
        }
    }
    public sealed class DispatchEventHandler : IEventHandler<DispatchEvent>
    {
        private IMessageContext? _context;
        public int SetterCalls { get; private set; }
        public IMessageContext? Context { get => _context; set { _context = value; SetterCalls++; } }
        public Task HandleAsync(DispatchEvent message, CancellationToken cancellationToken)
        {
            VerifyDispatchContext(Context, message.ExpectedContext);
            message.FirstCalls++;
            return Task.CompletedTask;
        }
    }
    public sealed class SecondDispatchEventHandler : IEventHandler<DispatchEvent>
    {
        private IMessageContext? _context;
        public int SetterCalls { get; private set; }
        public IMessageContext? Context { get => _context; set { _context = value; SetterCalls++; } }
        public Task HandleAsync(DispatchEvent message, CancellationToken cancellationToken)
        {
            VerifyDispatchContext(Context, message.ExpectedContext);
            message.SecondCalls++;
            return Task.CompletedTask;
        }
    }
    public sealed class DispatchDocumentHandler : IDocumentHandler<DispatchDocument>
    {
        private IMessageContext? _context;
        public int SetterCalls { get; private set; }
        public IMessageContext? Context { get => _context; set { _context = value; SetterCalls++; } }
        public Task HandleAsync(DispatchDocument message, CancellationToken cancellationToken)
        {
            VerifyDispatchContext(Context, message.ExpectedContext);
            message.Calls++;
            return Task.CompletedTask;
        }
    }

    public sealed class DispatchCommand : IDispatchAction
    {
        public IMessageContext? ExpectedContext { get; set; }
        public int Calls { get; set; }
    }

    public sealed class DispatchPropertyHandler : IActionHandler<DispatchCommand>
    {
        private IMessageContext? _context;
        public int SetterCalls { get; private set; }
        public IMessageContext? Context { get => _context; set { _context = value; SetterCalls++; } }

        public Task HandleAsync(DispatchCommand message, CancellationToken cancellationToken)
        {
            if (!ReferenceEquals(Context, message.ExpectedContext))
            {
                throw new InvalidOperationException("Full dispatch omitted or reused the handler context.");
            }
            message.Calls++;
            return Task.CompletedTask;
        }
    }

    private static T Activate<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors)] T>(IServiceProvider provider, IMessageContext context) where T : class =>
        (T)new AotHandlerActivator().ActivateHandler(typeof(T), context, provider);

    private static void Check(bool passed, string name)
    {
        if (!passed) { throw new InvalidOperationException(name); }
        Interlocked.Increment(ref _checks);
    }

    public class PropertyHandler
    {
        private IMessageContext? _context;
        public IMessageContext? Context { get => _context; set { _context = value; SetterCalls++; } }
        public int SetterCalls { get; private set; }
        public object? RegisteredContext { get; set; }
    }
    public sealed class TransientHandler : PropertyHandler;
    public sealed class PrivateSetterHandler { public IMessageContext? Context { get; private set; } }
    public class BaseHandler { public IMessageContext? Context { get; set; } }
    public sealed class InheritedHandler : BaseHandler;
    public sealed class ShadowHandler : BaseHandler { public new IMessageContext? Context { get; set; } }
    public sealed class AwareHandler : IMessageContextAware
    {
        public object? Bound { get; private set; }
        public int Calls { get; private set; }
        public IMessageContext? PropertyContext { get; set; }
        public void SetContext(IMessageContext context) { Bound = context; Calls++; }
    }
    public sealed class RegisteredHandler { public object? Bound { get; set; } public IMessageContext? Context { get; set; } }
    public sealed class LateHandler { public object? Bound { get; set; } public int Version { get; set; } }
    public sealed class UnsupportedHandler
    {
        public static IMessageContext? StaticContext { get; set; }
        public IMessageContext? this[int index] { get => null; set => throw new InvalidOperationException("Indexer must not be bound"); }
        public IMessageContext? ReadOnly => null;
        public MessageContext? ConcreteContext { get; set; }
        public object? Other { get; set; }
    }
    public sealed class MultipleHandler { public IMessageContext? First { get; set; } public IMessageContext? Second { get; set; } }
    public sealed class Dependency;
    public sealed class ConstructorHandler(Dependency dependency) { public Dependency Dependency { get; } = dependency; public IMessageContext? Context { get; set; } }
    public sealed class ThrowingHandler
    {
        public InvalidOperationException Failure { get; } = new("Expected context setter rejection");
        public IMessageContext? Context { get => null; set => throw Failure; }
    }
}

// Dispatch startup requires an explicit source-generated event serializer even though this probe sends only actions.
[JsonSerializable(typeof(string))]
internal partial class ProbeJsonContext : JsonSerializerContext;
