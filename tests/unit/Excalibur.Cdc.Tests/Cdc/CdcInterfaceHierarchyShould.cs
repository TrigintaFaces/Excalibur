// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Cdc.InMemory;

using Tests.Shared.Categories;

namespace Excalibur.Cdc.Tests;

/// <summary>
/// Verifies the CDC unified interface hierarchy introduced in Sprint 820.
/// <see cref="ICdcProcessor{TEvent}"/> is the base for all providers.
/// <see cref="ICdcStreamProcessor{TEvent, TPosition}"/> extends it for streaming providers.
/// </summary>
[Trait("Category", "Unit")]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait(TraitNames.Feature, TestFeatures.Abstractions)]
public sealed class CdcInterfaceHierarchyShould
{
    // ========================================
    // ICdcProcessor<T> — Base Interface Shape
    // ========================================

    [Fact]
    public void BeAnInterface()
    {
        // Assert
        typeof(ICdcProcessor<>).IsInterface.ShouldBeTrue();
    }

    [Fact]
    public void ExtendIAsyncDisposable()
    {
        // Assert
        typeof(IAsyncDisposable).IsAssignableFrom(typeof(ICdcProcessor<object>)).ShouldBeTrue();
    }

    [Fact]
    public void ExtendIDisposable()
    {
        // Assert
        typeof(IDisposable).IsAssignableFrom(typeof(ICdcProcessor<object>)).ShouldBeTrue();
    }

    [Fact]
    public void HaveExactlyOneDeclaredMethod()
    {
        // Act — only declared methods, not inherited from IDisposable/IAsyncDisposable
        var declaredMethods = typeof(ICdcProcessor<object>)
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly);

        // Assert
        declaredMethods.Length.ShouldBe(1);
        declaredMethods[0].Name.ShouldBe("ProcessBatchAsync");
    }

    [Fact]
    public void HaveOneGenericTypeParameter()
    {
        // Assert
        typeof(ICdcProcessor<>).GetGenericArguments().Length.ShouldBe(1);
    }

    // ========================================
    // ICdcStreamProcessor<T, TPos> — Streaming Extension
    // ========================================

    [Fact]
    public void StreamProcessor_BeAnInterface()
    {
        // Assert
        typeof(ICdcStreamProcessor<,>).IsInterface.ShouldBeTrue();
    }

    [Fact]
    public void StreamProcessor_ExtendICdcProcessor()
    {
        // Assert — ICdcStreamProcessor<T, TPos> : ICdcProcessor<T>
        var streamType = typeof(ICdcStreamProcessor<object, int>);
        var baseType = typeof(ICdcProcessor<object>);
        baseType.IsAssignableFrom(streamType).ShouldBeTrue();
    }

    [Fact]
    public void StreamProcessor_HaveTwoGenericTypeParameters()
    {
        // Assert
        typeof(ICdcStreamProcessor<,>).GetGenericArguments().Length.ShouldBe(2);
    }

    [Fact]
    public void StreamProcessor_HaveThreeDeclaredMethods()
    {
        // Act — StartAsync, GetCurrentPositionAsync, ConfirmPositionAsync
        var declaredMethods = typeof(ICdcStreamProcessor<object, int>)
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly);

        // Assert
        declaredMethods.Length.ShouldBe(3);
        var methodNames = declaredMethods.Select(m => m.Name).OrderBy(n => n).ToArray();
        methodNames.ShouldBe(new[] { "ConfirmPositionAsync", "GetCurrentPositionAsync", "StartAsync" });
    }

    // ========================================
    // InMemory — Poll-Only Provider Hierarchy
    // ========================================

    [Fact]
    public void InMemoryProcessor_ImplementICdcProcessor()
    {
        // Assert — IInMemoryCdcProcessor : ICdcProcessor<InMemoryCdcChange>
        typeof(ICdcProcessor<InMemoryCdcChange>)
            .IsAssignableFrom(typeof(IInMemoryCdcProcessor))
            .ShouldBeTrue();
    }

    [Fact]
    public void InMemoryProcessor_NotImplementICdcStreamProcessor()
    {
        // Assert — InMemory is poll-only, NOT streaming
        var interfaces = typeof(IInMemoryCdcProcessor).GetInterfaces();
        interfaces.ShouldNotContain(i =>
            i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICdcStreamProcessor<,>));
    }

    // ========================================
    // Inheritance Transitivity
    // ========================================

    [Fact]
    public void StreamProcessors_AlsoBeDisposable()
    {
        // Assert — through ICdcProcessor<T> base
        typeof(IAsyncDisposable)
            .IsAssignableFrom(typeof(ICdcStreamProcessor<object, int>))
            .ShouldBeTrue();
        typeof(IDisposable)
            .IsAssignableFrom(typeof(ICdcStreamProcessor<object, int>))
            .ShouldBeTrue();
    }

    [Fact]
    public void StreamProcessors_InheritProcessBatchAsync()
    {
        // Assert — ProcessBatchAsync is accessible via the base ICdcProcessor<T> interface
        var interfaceMap = typeof(ICdcStreamProcessor<object, int>).GetInterfaces();
        var baseProcessorType = typeof(ICdcProcessor<object>);
        interfaceMap.ShouldContain(baseProcessorType);

        // The base interface declares ProcessBatchAsync
        var batchMethod = baseProcessorType.GetMethod("ProcessBatchAsync");
        batchMethod.ShouldNotBeNull();
    }
}
