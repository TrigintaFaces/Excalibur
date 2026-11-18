// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using Excalibur.Dispatch.Benchmarks.Core;
using Excalibur.Dispatch.Delivery.Handlers;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Excalibur.Dispatch.Tests.Benchmarks;

[Trait("Category", "Unit")]
[Trait("Component", "Dispatch.Core")]
[Trait("Pattern", "Behavior")]
public sealed class MiddlewareBenchmarkCompositionShould
{
    [Fact]
    public async Task ExecuteEveryDeclaredArmWithItsOwnProviderAndPermitRepeatedCleanup()
    {
        var fixture = new MiddlewareChainBenchmarks();
        try
        {
            fixture.GlobalSetup();
            for (var i = 0; i < 3; i++)
            {
                (await fixture.NoMiddleware()).Succeeded.ShouldBeTrue();
                (await fixture.OneMiddleware()).Succeeded.ShouldBeTrue();
                (await fixture.FiveMiddleware()).Succeeded.ShouldBeTrue();
                (await fixture.TenMiddleware()).Succeeded.ShouldBeTrue();
                var result = await fixture.MiddlewareShortCircuit();
                result.Succeeded.ShouldBeTrue();
                result.GetType().DeclaringType.ShouldBe(typeof(MiddlewareChainBenchmarks));
            }
        }
        finally
        {
            fixture.GlobalCleanup();
            fixture.GlobalCleanup();
        }
    }
    [Fact]
    public async Task ExecuteDistinctSingleTenAndFiftyHandlerPopulations()
    {
        var fixture = new HandlerInvocationBenchmarks();
        try
        {
            fixture.GlobalSetup();
            AssertRegistryPopulation(fixture, "_singleEventProvider", "TestEvent", 1);
            AssertRegistryPopulation(fixture, "_serviceProvider", "TestEvent", 10);
            AssertRegistryPopulation(fixture, "_serviceProvider", "BatchTestEvent", 50);
            for (var i = 0; i < 3; i++)
            {
                (await fixture.InvokeSingleEventHandler()).Succeeded.ShouldBeTrue();
                (await fixture.InvokeMultipleEventHandlers()).Succeeded.ShouldBeTrue();
                (await fixture.InvokeBatchHandlers()).Succeeded.ShouldBeTrue();
            }
        }
        finally
        {
            fixture.GlobalCleanup();
        }
    }
    private static void AssertRegistryPopulation(object fixture, string providerField, string messageName, int count)
    {
        var type = fixture.GetType();
        var provider = (IServiceProvider)type.GetField(providerField, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixture)!;
        var message = type.GetNestedType(messageName, BindingFlags.NonPublic)!;
        provider.GetRequiredService<IHandlerRegistry>().TryGetHandlers(message, out var handlers).ShouldBeTrue();
        handlers.Count.ShouldBe(count);
        handlers.Select(entry => entry.HandlerType).Distinct().Count().ShouldBe(count);
    }
}
