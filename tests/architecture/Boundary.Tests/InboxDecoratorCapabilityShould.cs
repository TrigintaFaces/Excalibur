// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;
using System.Runtime.CompilerServices;
using Excalibur.Dispatch;

namespace Boundary.Tests;

/// <summary>Decorating an inbox must not hide its scoped transactional capability.</summary>
/// <remarks>
/// This checks exposed capabilities, not forwarding behavior. The decorator integration tests
/// separately verify middleware selection, commit visibility, and rollback.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Architecture")]
[Trait("Pattern", "Regression")]
public sealed class InboxDecoratorCapabilityShould
{
    [Fact]
    public void PreserveScopedCapabilityOnEveryShippedRelationalDecorator()
    {
        var assemblies = BoundaryFrameworkAssemblyFixture.GetExpectedAssemblies(AppContext.BaseDirectory,
            Path.Combine(AppContext.BaseDirectory, "Boundary.Tests.deps.json"));
        // Load/type-resolution failures must fail, never silently reduce the population.
        var types = assemblies.SelectMany(a => Assembly.LoadFrom(a.Path).GetTypes()).ToArray();
        foreach (var name in new[]
        {
            "Excalibur.Compliance.Encryption.Decorators.EncryptingInboxStoreDecorator",
            "Excalibur.Inbox.Diagnostics.TelemetryInboxStoreDecorator",
        })
        {
            var decorator = types.Single(t => t.FullName == name);
            typeof(IInboxStore).IsAssignableFrom(decorator).ShouldBeTrue(name);
            WrapsInbox(decorator, typeof(IInboxStore)).ShouldBeTrue(name);
        }
        FindViolations(types, typeof(IInboxStore), typeof(ITransactionalInboxStore),
            typeof(IScopedTransactionalInboxStore)).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(typeof(MissingScoped), true)]
    [InlineData(typeof(InheritedMissingScoped), true)]
    [InlineData(typeof(PartialMissingScoped), true)]
    [InlineData(typeof(CompleteDecorator), false)]
    [InlineData(typeof(InheritedComplete), false)]
    [InlineData(typeof(PlainProvider), false)]
    [InlineData(typeof(PlainRecordProvider), false)]
    [InlineData(typeof(RecordDecorator), true)]
    [InlineData(typeof(GeneratedConstructorDecorator), true)]
    [InlineData(typeof(FactoryDecorator), true)]
    [InlineData(typeof(LazyDecorator), true)]
    [InlineData(typeof(Unrelated), false)]
    public void ClassifyCompiledTypesWithoutDependingOnSourceSpelling(Type candidate, bool violation)
    {
        // A known good decorator keeps the population nonempty for the provider/noise controls.
        var violations = FindViolations([candidate, typeof(CompleteDecorator)],
            typeof(IInbox), typeof(IRelational), typeof(IScoped));
        violations.Contains(candidate).ShouldBe(violation);
        violations.Count.ShouldBe(violation ? 1 : 0);
    }

    [Fact]
    public void RefuseAnEmptyDecoratorPopulation()
    {
        Should.Throw<InvalidOperationException>(() => FindViolations([], typeof(IInbox), typeof(IRelational), typeof(IScoped)));
        Should.Throw<InvalidOperationException>(() => FindViolations([typeof(PlainProvider)], typeof(IInbox), typeof(IRelational), typeof(IScoped)));
    }

    private static List<Type> FindViolations(IEnumerable<Type> types, Type inbox, Type relational, Type scoped)
    {
        var decorators = types.Distinct().Where(t => !t.IsInterface && inbox.IsAssignableFrom(t) && WrapsInbox(t, inbox)).ToArray();
        if (decorators.Length == 0)
        {
            throw new InvalidOperationException("No inbox decorators were found in the compiled population.");
        }

        return decorators.Where(t => relational.IsAssignableFrom(t) && !scoped.IsAssignableFrom(t)).ToList();
    }

    private static bool WrapsInbox(Type type, Type inbox)
    {
        const BindingFlags members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        for (Type? current = type; current is not null; current = current.BaseType)
        {
            // A record's generated copy constructor receives the provider itself, not an inner store.
            if (current.GetConstructors(members).Where(c => !(c.IsDefined(typeof(CompilerGeneratedAttribute)) &&
                    c.GetParameters() is [{ ParameterType: var parameterType }] && parameterType == current))
                    .SelectMany(c => c.GetParameters()).Any(p => ContainsInbox(p.ParameterType, inbox)) ||
                current.GetFields(members).Any(f => ContainsInbox(f.FieldType, inbox)) ||
                current.GetProperties(members).Any(p => ContainsInbox(p.PropertyType, inbox)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsInbox(Type type, Type inbox) =>
        inbox.IsAssignableFrom(type) ||
        (type.HasElementType && ContainsInbox(type.GetElementType()!, inbox)) ||
        (type.IsGenericType && type.GetGenericArguments().Any(argument => ContainsInbox(argument, inbox)));

    private interface IInbox;
    private interface IRelational : IInbox;
    private interface IScoped : IInbox;

    private class MissingScoped(IInbox inner) : IRelational
    {
        // A private base member also verifies inherited wrapping, not just constructor spelling.
        private readonly IInbox _inner = inner;
        public IInbox Inner => _inner;
    }

    private sealed class InheritedMissingScoped() : MissingScoped(new PlainProvider());

    private partial class PartialMissingScoped : IRelational;
    private partial class PartialMissingScoped
    {
        public IInbox Inner { get; } = new PlainProvider();
    }

    private class CompleteDecorator(IInbox inner) : IRelational, IScoped
    {
        public IInbox Inner { get; } = inner;
    }

    private sealed class InheritedComplete() : CompleteDecorator(new PlainProvider());
    private sealed class PlainProvider : IRelational;
    private sealed record PlainRecordProvider : IRelational;
    private sealed record RecordDecorator(IInbox Inner) : IRelational;
    private sealed record FactoryDecorator(Func<IInbox> Factory) : IRelational;
    private sealed record LazyDecorator(Lazy<IInbox> Inner) : IRelational;

    private sealed class GeneratedConstructorDecorator : IRelational
    {
        [CompilerGenerated]
        public GeneratedConstructorDecorator(IInbox inner) => _ = inner;
    }

    // Mentions of IInbox/IRelational/IScoped, or an unrelated class holding an inbox,
    // must not turn PlainProvider above into a decorator merely by sharing its file.
    private sealed class Unrelated
    {
        public IInbox Inner { get; } = new PlainProvider();
    }
}
