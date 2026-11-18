// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Tests.DependencyInjection;

[Trait("Category", "Unit")]
[Trait("Component", "DependencyInjection")]
[Trait("Pattern", "Regression")]
public sealed class KeyedStoreImplementationTypeDependencyGraphShould
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DiagnoseEarlierMalformedUnselectedGraphPrecedence(bool decorate)
    {
        Outer.Reset();
        using var provider = Build(decorate, registerBad: true);
        IOuter? value = null;
        InvalidOperationException? error = null;
        try
        {
            value = provider.GetRequiredKeyedService<IOuter>("a");
        }
        catch (InvalidOperationException exception)
        {
            error = exception;
        }

        // Native DI's recursive callsite builder diagnoses Bad first. The helper is
        // deliberately not required to reproduce failures in an unselected graph.
        if (error is not null)
        {
            error.Message.ShouldContain(nameof(IMissingInsideBad));
            Outer.ShortCalls.ShouldBe(0);
        }
        else
        {
            decorate.ShouldBeTrue("the stock native baseline diagnoses the earlier malformed graph");
            value.ShouldNotBeNull().Selection.ShouldBe("short");
            Outer.ShortCalls.ShouldBe(1);
        }

        Bad.BodyCalls.ShouldBe(0);
        Outer.GreedyCalls.ShouldBe(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PropagateSelectedDependencyPrimaryFaultWithoutTryingShorterConstructor(bool decorate)
    {
        SelectedOuter.BodyCalls = 0;
        SelectedOuter.ShortCalls = 0;
        var primary = new IOException("selected-dependency-primary");
        var factoryCalls = 0;
        var services = new ServiceCollection();
        services.AddSingleton<Bad>(_ => { factoryCalls++; throw primary; });
        services.AddKeyedSingleton<IOuter, SelectedOuter>("a");
        if (decorate)
        {
            services.DecorateKeyedStores<IOuter>((inner, _) => new View(inner));
        }

        using var provider = services.BuildServiceProvider();
        var error = Should.Throw<IOException>(() => provider.GetRequiredKeyedService<IOuter>("a"));
        error.ShouldBeSameAs(primary);
        factoryCalls.ShouldBe(1);
        SelectedOuter.BodyCalls.ShouldBe(0);
        SelectedOuter.ShortCalls.ShouldBe(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreserveShorterConstructorWhenEarlierDependencyIsNotRegistered(bool decorate)
    {
        Outer.Reset();
        using var provider = Build(decorate, registerBad: false);
        var value = provider.GetRequiredKeyedService<IOuter>("a");
        value.Selection.ShouldBe("short");
        Bad.BodyCalls.ShouldBe(0);
        Outer.GreedyCalls.ShouldBe(0);
        Outer.ShortCalls.ShouldBe(1);
    }

    private static ServiceProvider Build(bool decorate, bool registerBad)
    {
        var services = new ServiceCollection();
        if (registerBad)
        {
            services.AddSingleton<Bad>();
        }

        services.AddKeyedSingleton<IOuter, Outer>("a");
        if (decorate)
        {
            services.DecorateKeyedStores<IOuter>((inner, _) => new View(inner));
        }

        return services.BuildServiceProvider();
    }

    private interface IMissingInsideBad;
    private interface ILaterMissing;
    private interface IOuter
    {
        string Selection { get; }
    }

    private sealed class Bad
    {
        public Bad(IMissingInsideBad missing)
        {
            missing.ShouldNotBeNull();
            BodyCalls++;
        }

        public static int BodyCalls { get; set; }
    }

    private sealed class Outer : IOuter
    {
        public Outer()
        {
            ShortCalls++;
            Selection = "short";
        }

        public Outer(Bad bad, ILaterMissing later)
        {
            bad.ShouldNotBeNull();
            later.ShouldNotBeNull();
            GreedyCalls++;
            Selection = "greedy";
        }

        public static int GreedyCalls { get; private set; }
        public static int ShortCalls { get; private set; }
        public string Selection { get; }

        public static void Reset()
        {
            Bad.BodyCalls = 0;
            GreedyCalls = 0;
            ShortCalls = 0;
        }
    }

    private sealed class SelectedOuter : IOuter
    {
        public SelectedOuter()
        {
            ShortCalls++;
            Selection = "short";
        }

        public SelectedOuter(Bad bad)
        {
            bad.ShouldNotBeNull();
            BodyCalls++;
            Selection = "selected";
        }

        public static int BodyCalls { get; set; }
        public static int ShortCalls { get; set; }
        public string Selection { get; }
    }

    private sealed class View(IOuter inner) : IOuter
    {
        public string Selection => inner.Selection;
    }
}
