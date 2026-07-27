// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Resilience.Polly;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Regression lock (bead p2g4fo) for the Polly-path <see cref="BackoffStrategy.FullJitter"/> wiring at the
/// <b>DI-registration</b> seam (<c>AddPollyResilienceAdapters</c> → <c>IBackoffCalculator</c>).
/// </summary>
/// <remarks>
/// <para>
/// Complements the factory-path lock (bead ufbn29, <c>PollyFullJitterWiringShould</c> in the same folder,
/// which binds <c>RetryPolicy.CreateBackoffCalculator</c>). The <c>FullJitter</c> forcing lives at three
/// sites sharing the same one-token expression; this lock covers the DI-registration site
/// (<c>DispatchBuilderResilienceExtensions</c>), a distinct code path the factory-path lock does not exercise.
/// The third site (<c>PollyRetryPolicyAdapter</c>) consumes the flag into a Polly pipeline and is not
/// unit-observable without timing — left to integration coverage; its mapping is identical to the two locked
/// sites.
/// </para>
/// <para>
/// <b>The bug this catches:</b> selecting <c>FullJitter</c> while leaving <c>UseJitter=false</c> must still
/// produce a jittered exponential calculator. RED if the DI lambda drops the
/// <c>|| BackoffStrategy == FullJitter</c> clause (advertised-but-inert). The adapter is non-public so
/// reflection is used rather than widening production visibility (internal-first); a rename fails the lock
/// loudly (field-not-found) rather than passing vacuously.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Feature", "Resilience")]
public sealed class PollyFullJitterDiRegistrationShould
{
    private static readonly TimeSpan BaseDelay = TimeSpan.FromMilliseconds(100);

    private static IBackoffCalculator ResolveCalculator(BackoffStrategy strategy, bool useJitter)
    {
        var services = new ServiceCollection();
        _ = services.AddPollyResilienceAdapters(o => o.RetryOptions = new RetryOptions
        {
            BackoffStrategy = strategy,
            UseJitter = useJitter,
            BaseDelay = BaseDelay,
        });

        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IBackoffCalculator>();
    }

    [Fact]
    public void ForcePollyJitterOn_ForFullJitter_EvenWhenCallerUseJitterIsFalse()
    {
        // Caller explicitly disables jitter; the DI registration must still force it on for FullJitter.
        var calculator = ResolveCalculator(BackoffStrategy.FullJitter, useJitter: false);

        var adapterType = calculator.GetType();
        adapterType.Name.ShouldBe(
            "PollyBackoffCalculatorAdapter",
            "FullJitter must route through the Polly backoff adapter (not the Fibonacci adapter).");

        var useJitter = (bool)GetPrivateField(adapterType, calculator, "_useJitter");
        var backoffType = GetPrivateField(adapterType, calculator, "_backoffType");

        useJitter.ShouldBeTrue(
            "p2g4fo — the DI registration must force Polly's jitter ON for FullJitter independent of the caller's UseJitter flag (advertised-but-inert otherwise).");
        backoffType.ToString().ShouldBe(
            "Exponential",
            "p2g4fo — FullJitter maps to Polly DelayBackoffType.Exponential (Polly v8 has no distinct FullJitter member).");
    }

    [Fact]
    public void NotForceJitter_ForExponentialWithoutJitter_ViaDiRegistration()
    {
        // Control — proves the assertion is non-vacuous: without FullJitter and UseJitter=false jitter stays off.
        var calculator = ResolveCalculator(BackoffStrategy.Exponential, useJitter: false);

        var useJitter = (bool)GetPrivateField(calculator.GetType(), calculator, "_useJitter");
        useJitter.ShouldBeFalse(
            "control: Exponential with UseJitter=false must NOT enable jitter (otherwise the FullJitter assertion is vacuous).");
    }

    private static object GetPrivateField(Type type, object instance, string name)
    {
        var field = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException(
                $"p2g4fo — PollyBackoffCalculatorAdapter.{name} not found (field renamed; update the lock).");
        return field.GetValue(instance)
            ?? throw new InvalidOperationException($"p2g4fo — {name} was null.");
    }
}
