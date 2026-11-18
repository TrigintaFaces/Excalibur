// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Tests.DependencyInjection;

[Trait("Category", "Unit")]
[Trait("Component", "DependencyInjection")]
[Trait("Pattern", "Regression")]
public sealed class KeyedStoreImplementationTypeActivationShould
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreserveInheritedKeyDependency(bool decorate) => CheckDependency<InheritedProbe>(decorate, "inherited");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreserveExplicitKeyDependency(bool decorate) => CheckDependency<ExplicitProbe>(decorate, "explicit");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreserveExplicitNullUnkeyedDependency(bool decorate) => CheckDependency<NullProbe>(decorate, "unkeyed");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreserveNativeKeyedGreedySelectionAndOrdinarySameTypeDependency(bool decorate)
    {
        var state = new State();
        var ordinary = new object();
        var services = Setup(state, ordinary);
        services.AddSingleton(_ => { state.DependencyCalls++; return new Extra(); });
        using var provider = Build<ChoiceProbe>(services, decorate);
        var value = provider.GetRequiredKeyedService<IProbe>("a");
        value.Key.ShouldBe("a");
        value.Dependency.ShouldBeSameAs(ordinary);
        value.Selection.ShouldBe("greedy");
        state.GreedyCalls.ShouldBe(1);
        state.PreferredCalls.ShouldBe(0);
        state.DependencyCalls.ShouldBe(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreserveExistingUnkeyedActivatorUtilitiesPreferredConstructorPolicy(bool decorate)
    {
        var state = new State();
        var ordinary = new object();
        var services = Setup(state, ordinary);
        services.AddSingleton(_ => { state.DependencyCalls++; return new Extra(); });
        using var provider = Build<ChoiceProbe>(services, decorate, keyed: false);
        // The baseline is deliberately ActivatorUtilities, the existing unkeyed helper policy.
        var value = decorate ? provider.GetRequiredService<IProbe>() : ActivatorUtilities.CreateInstance<ChoiceProbe>(provider);
        value.Key.ShouldBeSameAs(ordinary);
        value.Dependency.ShouldBeSameAs(ordinary);
        value.Selection.ShouldBe("preferred");
        state.PreferredCalls.ShouldBe(1);
        state.GreedyCalls.ShouldBe(0);
        state.DependencyCalls.ShouldBe(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreserveOptionalDependencyAndValueDefaults(bool decorate)
    {
        var state = new State();
        using var provider = Build<OptionalProbe>(Setup(state, new object()), decorate);
        var value = provider.GetRequiredKeyedService<IProbe>("a");
        value.Key.ShouldBe("a");
        value.Dependency.ShouldBeNull();
        value.Number.ShouldBe(17);
        state.BodyCalls.ShouldBe(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RejectAmbiguityWithoutActivatingCandidateDependencies(bool decorate)
    {
        var state = new State();
        var services = Setup(state, new object());
        services.AddSingleton(_ => { state.DependencyCalls++; return new Left(); });
        services.AddSingleton(_ => { state.DependencyCalls++; return new Right(); });
        using var provider = Build<AmbiguousProbe>(services, decorate);
        var error = Should.Throw<InvalidOperationException>(() => provider.GetRequiredKeyedService<IProbe>("a"));
        error.Message.ShouldContain(nameof(AmbiguousProbe));
        state.DependencyCalls.ShouldBe(0);
        state.BodyCalls.ShouldBe(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RejectMissingDependencyBeforeAnyConstructorOrOrdinaryKeyFallback(bool decorate)
    {
        var state = new State();
        var services = new ServiceCollection();
        services.AddSingleton(state);
        services.AddSingleton<object>(_ => { state.DependencyCalls++; return new object(); });
        using var provider = Build<MissingProbe>(services, decorate);
        var error = Should.Throw<InvalidOperationException>(() => provider.GetRequiredKeyedService<IProbe>("a"));
        error.Message.ShouldContain(nameof(Missing));
        state.DependencyCalls.ShouldBe(0);
        state.BodyCalls.ShouldBe(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreserveConstructorPrimaryExceptionIdentity(bool decorate)
    {
        var state = new State();
        using var provider = Build<ThrowingProbe>(Setup(state, new object()), decorate);
        var error = Should.Throw<IOException>(() => provider.GetRequiredKeyedService<IProbe>("a"));
        error.ShouldBeSameAs(state.Primary);
        state.BodyCalls.ShouldBe(1);
    }

    private static ServiceCollection Setup(State state, object ordinary)
    {
        var services = new ServiceCollection();
        services.AddSingleton(state);
        services.AddSingleton(ordinary);
        return services;
    }

    private static void CheckDependency<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(bool decorate, string expected)
        where T : class, IProbe
    {
        var services = Setup(new State(), new object());
        services.AddSingleton(new Dependency("unkeyed"));
        services.AddKeyedSingleton("a", new Dependency("inherited"));
        services.AddKeyedSingleton("fixed", new Dependency("explicit"));
        using var provider = Build<T>(services, decorate);
        var value = provider.GetRequiredKeyedService<IProbe>("a");
        value.Key.ShouldBe("a");
        value.Dependency.ShouldBeOfType<Dependency>().ShouldNotBeNull().Name.ShouldBe(expected);
    }

    private static ServiceProvider Build<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(ServiceCollection services, bool decorate, bool keyed = true)
        where T : class, IProbe
    {
        if (keyed)
        {
            services.AddKeyedSingleton<IProbe, T>("a");
        }
        else
        {
            services.AddSingleton<IProbe, T>();
        }

        if (decorate)
        {
            services.DecorateKeyedStores<IProbe>((inner, _) => new View(inner));
        }

        return services.BuildServiceProvider();
    }

    private interface IProbe
    {
        object? Key { get; }
        object? Dependency { get; }
        int Number { get; }
        string Selection { get; }
    }

    private abstract class Probe(object? key, object? dependency = null, int number = 0, string selection = "ordinary") : IProbe
    {
        public object? Key { get; } = key;
        public object? Dependency { get; } = dependency;
        public int Number { get; } = number;
        public string Selection { get; } = selection;
    }

    private sealed class View(IProbe inner) : IProbe
    {
        public object? Key => inner.Key;
        public object? Dependency => inner.Dependency;
        public int Number => inner.Number;
        public string Selection => inner.Selection;
    }

    private sealed class State
    {
        public IOException Primary { get; } = new("constructor-primary");
        public int BodyCalls { get; set; }
        public int DependencyCalls { get; set; }
        public int GreedyCalls { get; set; }
        public int PreferredCalls { get; set; }
    }

    private sealed record Dependency(string Name);
    private sealed class Extra;
    private sealed class Left;
    private sealed class Right;
    private sealed class Missing;
    private sealed class Optional;

    private sealed class InheritedProbe : Probe
    {
        public InheritedProbe([ServiceKey] object key, [FromKeyedServices] Dependency dependency) : base(key, dependency) { }
    }

    private sealed class ExplicitProbe : Probe
    {
        public ExplicitProbe([ServiceKey] object key, [FromKeyedServices("fixed")] Dependency dependency) : base(key, dependency) { }
    }

    private sealed class NullProbe : Probe
    {
        public NullProbe([ServiceKey] object key, [FromKeyedServices(null)] Dependency dependency) : base(key, dependency) { }
    }

    private sealed class ChoiceProbe : Probe
    {
        [ActivatorUtilitiesConstructor]
        public ChoiceProbe(State state, object ordinary, [ServiceKey] object key) : base(key, ordinary, selection: "preferred")
        {
            state.PreferredCalls++;
        }

        public ChoiceProbe(State state, object ordinary, [ServiceKey] object key, Extra extra) : base(key, ordinary, selection: "greedy")
        {
            extra.ShouldNotBeNull();
            state.GreedyCalls++;
        }
    }

    private sealed class OptionalProbe : Probe
    {
        public OptionalProbe([ServiceKey] object key, State state, Optional? dependency = null, int number = 17) : base(key, dependency, number)
        {
            state.BodyCalls++;
        }
    }

    private sealed class AmbiguousProbe : Probe
    {
        public AmbiguousProbe([ServiceKey] object key, State state, Left dependency) : base(key, dependency) { state.BodyCalls++; }
        public AmbiguousProbe([ServiceKey] object key, State state, Right dependency) : base(key, dependency) { state.BodyCalls++; }
    }

    private sealed class MissingProbe : Probe
    {
        public MissingProbe([ServiceKey] object key, Missing dependency, State state) : base(key, dependency) { state.BodyCalls++; }
    }

    private sealed class ThrowingProbe : Probe
    {
        public ThrowingProbe(State state, [ServiceKey] object key) : base(key)
        {
            state.BodyCalls++;
            throw state.Primary;
        }
    }
}
