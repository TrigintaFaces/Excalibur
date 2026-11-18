// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Delivery;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Tests.Delivery.ScheduleDurability;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "SCHEDULING")]
public sealed class DurableScheduleStoreActivationShould
{
    [Fact]
    public void HonorTheExplicitPreferredConstructorUnlikeOrdinaryTypeRegistration()
    {
        var services = Dependencies();
        services.AddDurableScheduleStore<PreferredStore>();
        using var durable = services.BuildServiceProvider();
        var selected = durable.GetRequiredService<IScheduleStore>().ShouldBeOfType<PreferredStore>();
        selected.ConstructorArity.ShouldBe(1);
        selected.Dependency.ShouldBeSameAs(durable.GetRequiredService<DependencyA>());

        var ordinaryServices = Dependencies();
        ordinaryServices.AddSingleton<IScheduleStore, PreferredStore>();
        using var ordinary = ordinaryServices.BuildServiceProvider();
        ordinary.GetRequiredService<IScheduleStore>().ShouldBeOfType<PreferredStore>()
            .ConstructorArity.ShouldBe(2, "ordinary type registration deliberately supplies the contrast");
    }

    [Fact]
    public void SelectTheLongestResolvableUnmarkedConstructorAndInjectTheSameDependencies()
    {
        var services = Dependencies();
        services.AddDurableScheduleStore<UnmarkedStore>();
        using var provider = services.BuildServiceProvider();
        var selected = provider.GetRequiredService<IScheduleStore>().ShouldBeOfType<UnmarkedStore>();
        selected.DependencyA.ShouldBeSameAs(provider.GetRequiredService<DependencyA>());
        selected.DependencyB.ShouldBeSameAs(provider.GetRequiredService<DependencyB>());
    }

    [Fact]
    public void RefuseEquallyResolvableAmbiguousConstructors()
    {
        var services = Dependencies();
        services.AddDurableScheduleStore<AmbiguousStore>();
        using var provider = services.BuildServiceProvider();
        Should.Throw<InvalidOperationException>(() => provider.GetRequiredService<IScheduleStore>());
    }

    [Fact]
    public void RefuseAMissingRequiredDependency()
    {
        var services = new ServiceCollection();
        services.AddDurableScheduleStore<PreferredStore>();
        using var provider = services.BuildServiceProvider();
        Should.Throw<InvalidOperationException>(() => provider.GetRequiredService<IScheduleStore>());
    }

    private static ServiceCollection Dependencies()
    {
        var services = new ServiceCollection();
        services.AddSingleton<DependencyA>();
        services.AddSingleton<DependencyB>();
        return services;
    }

    private sealed class DependencyA
    {
        public DependencyA() { }
    }

    private sealed class DependencyB
    {
        public DependencyB() { }
    }

    private abstract class EmptyStore : IScheduleStore
    {
        public Task<IEnumerable<IScheduledMessage>> GetAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IEnumerable<IScheduledMessage>>([]);
        public Task StoreAsync(IScheduledMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task CompleteAsync(Guid scheduleId, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class PreferredStore : EmptyStore
    {
        [ActivatorUtilitiesConstructor]
        public PreferredStore(DependencyA dependency)
        {
            Dependency = dependency;
            ConstructorArity = 1;
        }

        public PreferredStore(DependencyA dependency, DependencyB second)
        {
            ArgumentNullException.ThrowIfNull(second);
            Dependency = dependency;
            ConstructorArity = 2;
        }

        public DependencyA Dependency { get; }
        public int ConstructorArity { get; }
    }

    private sealed class UnmarkedStore : EmptyStore
    {
        public UnmarkedStore(DependencyA dependencyA) => DependencyA = dependencyA;
        public UnmarkedStore(DependencyA dependencyA, DependencyB dependencyB)
        {
            DependencyA = dependencyA;
            DependencyB = dependencyB;
        }

        public DependencyA DependencyA { get; }
        public DependencyB? DependencyB { get; }
    }

    private sealed class AmbiguousStore : EmptyStore
    {
        public AmbiguousStore(DependencyA dependency) => ArgumentNullException.ThrowIfNull(dependency);
        public AmbiguousStore(DependencyB dependency) => ArgumentNullException.ThrowIfNull(dependency);
    }
}
