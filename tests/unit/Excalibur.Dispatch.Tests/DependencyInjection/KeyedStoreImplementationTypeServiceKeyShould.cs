// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Tests.DependencyInjection;

[Trait("Category", "Unit")]
[Trait("Component", "DependencyInjection")]
[Trait("Pattern", "Regression")]
public sealed class KeyedStoreImplementationTypeServiceKeyShould
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Baseline_ImplementationTypeReceivesActualServiceKeyDespiteAnUnrelatedObjectService(bool wildcard) =>
        Check(wildcard, decorate: false);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Diagnostic_HelperImplementationTypeMustNotReplaceServiceKeyWithAnUnrelatedObjectService(bool wildcard) =>
        Check(wildcard, decorate: true);

    private static void Check(bool wildcard, bool decorate)
    {
        var services = new ServiceCollection();
        var unrelated = new object();
        services.AddSingleton(unrelated);
        services.AddKeyedSingleton<IProbe, KeyedType>(wildcard ? KeyedService.AnyKey : "a");
        if (decorate) { services.DecorateKeyedStores<IProbe>((inner, _) => new Wrapper(inner)); }
        using var provider = services.BuildServiceProvider();
        var a = provider.GetRequiredKeyedService<IProbe>("a");
        a.Key.ShouldBe("a", "the built-in ServiceKey attribute names the requested registration, not a normal object dependency");
        a.Key.ShouldNotBeSameAs(unrelated);
        if (wildcard)
        {
            var b = provider.GetRequiredKeyedService<IProbe>("b");
            b.Key.ShouldBe("b");
            b.ShouldNotBeSameAs(a);
        }
    }

    private interface IProbe { object Key { get; } }
    private sealed class KeyedType : IProbe
    {
        public KeyedType([ServiceKey] object key) => Key = key;
        public object Key { get; }
    }
    private sealed class Wrapper(IProbe inner) : IProbe
    {
        public object Key => inner.Key;
    }
}
