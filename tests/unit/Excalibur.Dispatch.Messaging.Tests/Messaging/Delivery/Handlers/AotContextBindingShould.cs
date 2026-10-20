// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Handlers;
using Excalibur.Dispatch.Messaging;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery.Handlers;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "FACTORY")]
public sealed class AotContextBindingShould
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReplaceTheCurrentContextOnAResolvedInstance(bool aot)
    {
        using var provider = new ServiceCollection().AddSingleton<CountingPropertyHandler>().BuildServiceProvider();
        var activator = CreateActivator(aot);
        var first = new MessageContext();
        var second = new MessageContext();
        var handler = activator.ActivateHandler(typeof(CountingPropertyHandler), first, provider).ShouldBeOfType<CountingPropertyHandler>();
        handler.Context.ShouldBeSameAs(first);
        activator.ActivateHandler(typeof(CountingPropertyHandler), second, provider).ShouldBeSameAs(handler);
        handler.Context.ShouldBeSameAs(second);
        handler.SetterCalls.ShouldBe(2);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreservePrivateSettersAndInheritedProperties(bool aot)
    {
        using var provider = new ServiceCollection().AddSingleton<PrivateSetterHandler>().AddSingleton<InheritedPropertyHandler>().BuildServiceProvider();
        var context = new MessageContext();
        var activator = CreateActivator(aot);
        activator.ActivateHandler(typeof(PrivateSetterHandler), context, provider).ShouldBeOfType<PrivateSetterHandler>().Context.ShouldBeSameAs(context);
        activator.ActivateHandler(typeof(InheritedPropertyHandler), context, provider).ShouldBeOfType<InheritedPropertyHandler>().Context.ShouldBeSameAs(context);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreferExplicitInheritedInterfaceBindingOverRegistryAndProperties(bool aot)
    {
        HandlerActivatorRegistry.RegisterContextSetter<InheritedAwareHandler>((_, _) => throw new InvalidOperationException("Registry must not replace interface binding"));
        using var provider = new ServiceCollection().AddSingleton<InheritedAwareHandler>().BuildServiceProvider();
        var context = new MessageContext();
        var handler = CreateActivator(aot).ActivateHandler(typeof(InheritedAwareHandler), context, provider).ShouldBeOfType<InheritedAwareHandler>();
        handler.Bound.ShouldBeSameAs(context);
        handler.InterfaceCalls.ShouldBe(1);
        handler.PropertyContext.ShouldBeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreferRegisteredSettersOverProperties(bool aot)
    {
        HandlerActivatorRegistry.RegisterContextSetter<RegisteredPropertyHandler>((handler, context) => handler.Bound = context);
        using var provider = new ServiceCollection().AddSingleton<RegisteredPropertyHandler>().BuildServiceProvider();
        var context = new MessageContext();
        var handler = CreateActivator(aot).ActivateHandler(typeof(RegisteredPropertyHandler), context, provider).ShouldBeOfType<RegisteredPropertyHandler>();
        handler.Bound.ShouldBeSameAs(context);
        handler.Context.ShouldBeNull();
    }

    [Fact]
    public void ObserveLateAndReplacementRegisteredSettersAfterCachingAProperty()
    {
        using var provider = new ServiceCollection().AddSingleton<LatePropertyHandler>().BuildServiceProvider();
        var activator = new AotHandlerActivator();
        var first = new MessageContext();
        var second = new MessageContext();
        var third = new MessageContext();
        var handler = activator.ActivateHandler(typeof(LatePropertyHandler), first, provider).ShouldBeOfType<LatePropertyHandler>();
        handler.Context.ShouldBeSameAs(first);
        HandlerActivatorRegistry.RegisterContextSetter<LatePropertyHandler>((value, context) => { value.Bound = context; value.RegistryVersion = 1; });
        activator.ActivateHandler(typeof(LatePropertyHandler), second, provider).ShouldBeSameAs(handler);
        handler.Context.ShouldBeSameAs(first);
        handler.Bound.ShouldBeSameAs(second);
        handler.RegistryVersion.ShouldBe(1);
        HandlerActivatorRegistry.RegisterContextSetter<LatePropertyHandler>((value, context) => { value.Bound = context; value.RegistryVersion = 2; });
        activator.ActivateHandler(typeof(LatePropertyHandler), third, provider).ShouldBeSameAs(handler);
        handler.Bound.ShouldBeSameAs(third);
        handler.RegistryVersion.ShouldBe(2);
    }

    [Fact]
    public void ObserveLateRegisteredSettersAfterCachingNoProperty()
    {
        using var provider = new ServiceCollection().AddSingleton<LateNoPropertyHandler>().BuildServiceProvider();
        var activator = new AotHandlerActivator();
        var handler = activator.ActivateHandler(typeof(LateNoPropertyHandler), new MessageContext(), provider).ShouldBeOfType<LateNoPropertyHandler>();
        handler.Bound.ShouldBeNull();
        HandlerActivatorRegistry.RegisterContextSetter<LateNoPropertyHandler>((value, context) => value.Bound = context);
        var next = new MessageContext();
        activator.ActivateHandler(typeof(LateNoPropertyHandler), next, provider).ShouldBeSameAs(handler);
        handler.Bound.ShouldBeSameAs(next);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IgnoreStaticIndexedReadOnlyAndWrongTypeProperties(bool aot)
    {
        using var provider = new ServiceCollection().AddSingleton<UnsupportedPropertiesHandler>().BuildServiceProvider();
        var sentinel = new MessageContext();
        UnsupportedPropertiesHandler.StaticContext = sentinel;
        try
        {
            var handler = CreateActivator(aot).ActivateHandler(typeof(UnsupportedPropertiesHandler), new MessageContext(), provider).ShouldBeOfType<UnsupportedPropertiesHandler>();
            UnsupportedPropertiesHandler.StaticContext.ShouldBeSameAs(sentinel);
            handler.ConcreteContext.ShouldBeNull();
            handler.OtherContext.ShouldBeNull();
        }
        finally { UnsupportedPropertiesHandler.StaticContext = null; }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReflectOnlyTheRequestedTypeWhenTheContainerReturnsASubtype(bool aot)
    {
        var instance = new ShadowPropertyHandler();
        using var provider = new ServiceCollection().AddSingleton<BasePropertyHandler>(instance).BuildServiceProvider();
        var context = new MessageContext();
        CreateActivator(aot).ActivateHandler(typeof(BasePropertyHandler), context, provider).ShouldBeSameAs(instance);
        ((BasePropertyHandler)instance).Context.ShouldBeSameAs(context);
        instance.Context.ShouldBeNull();
    }

    [Fact]
    public void RetainInterfaceBindingOnAResolvedSubtype()
    {
        var instance = new AwareSubtypeHandler();
        using var provider = new ServiceCollection().AddSingleton<BasePropertyHandler>(instance).BuildServiceProvider();
        var context = new MessageContext();
        new AotHandlerActivator().ActivateHandler(typeof(BasePropertyHandler), context, provider).ShouldBeSameAs(instance);
        instance.Bound.ShouldBeSameAs(context);
        instance.Context.ShouldBeNull();
    }

    [Fact]
    public void MatchTheExpressionActivatorWhenSeveralPropertiesAreEligible()
    {
        using var firstProvider = new ServiceCollection().AddSingleton<TwoPropertiesHandler>().BuildServiceProvider();
        using var secondProvider = new ServiceCollection().AddSingleton<TwoPropertiesHandler>().BuildServiceProvider();
        var context = new MessageContext();
        var jit = new HandlerActivator().ActivateHandler(typeof(TwoPropertiesHandler), context, firstProvider).ShouldBeOfType<TwoPropertiesHandler>();
        var aot = new AotHandlerActivator().ActivateHandler(typeof(TwoPropertiesHandler), context, secondProvider).ShouldBeOfType<TwoPropertiesHandler>();
        (jit.First is not null ^ jit.Second is not null).ShouldBeTrue();
        aot.First.ShouldBeSameAs(jit.First);
        aot.Second.ShouldBeSameAs(jit.Second);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BindContextAfterUnregisteredConstructorInjection(bool aot)
    {
        var dependency = new ActivationDependency();
        using var provider = new ServiceCollection().AddSingleton(dependency).BuildServiceProvider();
        var context = new MessageContext();
        var handler = CreateActivator(aot).ActivateHandler(typeof(ConstructorHandler), context, provider).ShouldBeOfType<ConstructorHandler>();
        handler.Dependency.ShouldBeSameAs(dependency);
        handler.Context.ShouldBeSameAs(context);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PropagateTheOriginalPropertySetterException(bool aot)
    {
        using var provider = new ServiceCollection().AddSingleton<ThrowingPropertyHandler>().BuildServiceProvider();
        var handler = provider.GetRequiredService<ThrowingPropertyHandler>();
        Should.Throw<InvalidOperationException>(() => CreateActivator(aot).ActivateHandler(typeof(ThrowingPropertyHandler), new MessageContext(), provider)).ShouldBeSameAs(handler.Failure);
    }

    [Fact]
    public void PropagateInterfaceAndRegistryExceptionsWithoutReturningAHandler()
    {
        var registryFailure = new InvalidOperationException("Registry failure");
        HandlerActivatorRegistry.RegisterContextSetter<ThrowingRegistryHandler>((_, _) => throw registryFailure);
        using var provider = new ServiceCollection().AddSingleton<ThrowingAwareHandler>().AddSingleton<ThrowingRegistryHandler>().BuildServiceProvider();
        var activator = new AotHandlerActivator();
        Should.Throw<InvalidOperationException>(() => activator.ActivateHandler(typeof(ThrowingAwareHandler), new MessageContext(), provider)).ShouldBeSameAs(provider.GetRequiredService<ThrowingAwareHandler>().Failure);
        Should.Throw<InvalidOperationException>(() => activator.ActivateHandler(typeof(ThrowingRegistryHandler), new MessageContext(), provider)).ShouldBeSameAs(registryFailure);
    }

    [Fact]
    public void BindConcurrentTransientActivationsToTheirOwnContextExactlyOnce()
    {
        using var provider = new ServiceCollection().AddTransient<ConcurrentPropertyHandler>().BuildServiceProvider();
        var activator = new AotHandlerActivator();
        var handlers = new ConcurrentBag<ConcurrentPropertyHandler>();
        Parallel.For(0, 64, _ =>
        {
            var context = new MessageContext();
            var handler = activator.ActivateHandler(typeof(ConcurrentPropertyHandler), context, provider).ShouldBeOfType<ConcurrentPropertyHandler>();
            handler.Context.ShouldBeSameAs(context);
            handler.SetterCalls.ShouldBe(1);
            handlers.Add(handler);
        });
        handlers.Count.ShouldBe(64);
        handlers.Distinct(ReferenceEqualityComparer.Instance).Count().ShouldBe(64);
    }

    private static IHandlerActivator CreateActivator(bool aot) => aot ? new AotHandlerActivator() : new HandlerActivator();

#pragma warning disable CA1034 // Public nested types are activation fixtures.
    public class CountingPropertyHandler
    {
        private IMessageContext? _context;
        public IMessageContext? Context { get => _context; set { _context = value; SetterCalls++; } }
        public int SetterCalls { get; private set; }
    }
    public sealed class ConcurrentPropertyHandler : CountingPropertyHandler;
    public sealed class PrivateSetterHandler { public IMessageContext? Context { get; private set; } }
    public class BasePropertyHandler { public IMessageContext? Context { get; set; } }
    public sealed class InheritedPropertyHandler : BasePropertyHandler;
    public sealed class ShadowPropertyHandler : BasePropertyHandler { public new IMessageContext? Context { get; set; } }
    public class ExplicitAwareHandler : IMessageContextAware
    {
        public object? Bound { get; private set; }
        public int InterfaceCalls { get; private set; }
        public IMessageContext? PropertyContext { get; set; }
        void IMessageContextAware.SetContext(IMessageContext context) { Bound = context; InterfaceCalls++; }
    }
    public sealed class InheritedAwareHandler : ExplicitAwareHandler;
    public sealed class AwareSubtypeHandler : BasePropertyHandler, IMessageContextAware
    {
        public object? Bound { get; private set; }
        void IMessageContextAware.SetContext(IMessageContext context) => Bound = context;
    }
    public sealed class RegisteredPropertyHandler { public IMessageContext? Context { get; set; } public object? Bound { get; set; } }
    public sealed class LatePropertyHandler { public IMessageContext? Context { get; set; } public object? Bound { get; set; } public int RegistryVersion { get; set; } }
    public sealed class LateNoPropertyHandler { public object? Bound { get; set; } }
    public sealed class UnsupportedPropertiesHandler
    {
        public static IMessageContext? StaticContext { get; set; }
        public IMessageContext? this[int index] { get => null; set => throw new InvalidOperationException("Indexer must not run"); }
        public IMessageContext? ReadOnlyContext => null;
        public MessageContext? ConcreteContext { get; set; }
        public object? OtherContext { get; set; }
    }
    public sealed class TwoPropertiesHandler { public IMessageContext? First { get; set; } public IMessageContext? Second { get; set; } }
    public sealed class ActivationDependency;
    public sealed class ConstructorHandler(ActivationDependency dependency) { public ActivationDependency Dependency { get; } = dependency; public IMessageContext? Context { get; set; } }
    public sealed class ThrowingPropertyHandler
    {
        public InvalidOperationException Failure { get; } = new("Property failure");
        public IMessageContext? Context { get => null; set => throw Failure; }
    }
    public sealed class ThrowingAwareHandler : IMessageContextAware
    {
        public InvalidOperationException Failure { get; } = new("Interface failure");
        public void SetContext(IMessageContext context) => throw Failure;
    }
    public sealed class ThrowingRegistryHandler;
#pragma warning restore CA1034
}
