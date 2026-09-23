// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics;

using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Options;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Messaging.Tests.Messaging;

/// <summary>
/// Baggage reaches a message context only when the application named the key, and only within the caps.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE DEFECT.</b> Every entry on the ambient activity's baggage was copied into the context with no
/// allowlist and no cap. The runtime parses an inbound <c>baggage</c> header into that activity without
/// being asked, so on any publicly reachable endpoint the keys and values are chosen by the caller. They
/// then rode every message the request produced — onto the wire for remote transports, and into the logs,
/// telemetry and stores of every service downstream.
/// </para>
/// <para>
/// <b>TWO HARMS, TWO FIXES, AND NEITHER COVERS THE OTHER.</b> An allowlist keeps untrusted values out of
/// trusted sinks and does nothing about size, because a permitted key can carry a large value. The caps
/// bound amplification — one cheap request riding every downstream message — and say nothing about trust.
/// The arms below are split the same way so that satisfying one cannot be mistaken for satisfying both.
/// </para>
/// <para>
/// <b>WHY THE LAST ARM IS THE ONE THAT MATTERS.</b> Every failure arm here is satisfied by deleting baggage
/// propagation outright. The arm that an allowed key with an ordinary value still arrives is what
/// distinguishes a policy from a deletion.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Dispatch")]
public sealed class BaggageIsDeniedByDefaultShould
{
    private const string AllowedKey = "tenant-hint";
    private const string AttackerKey = "x-injected";

    private static IServiceProvider ProviderWith(Action<BaggagePropagationOptions>? configure)
    {
        var services = new ServiceCollection();
        if (configure is not null)
        {
            _ = services.Configure(configure);
        }

        return services.BuildServiceProvider();
    }

    private static Activity StartActivityWithBaggage(params (string Key, string Value)[] entries)
    {
        var activity = new Activity("baggage-test");
        foreach (var (key, value) in entries)
        {
            _ = activity.AddBaggage(key, value);
        }

        return activity.Start();
    }

    /// <summary>
    /// SAFETY — the arm the defect is about. A key nobody allowed does not reach the context.
    /// </summary>
    [Fact]
    public void NotCopyABaggageKeyTheApplicationNeverAllowed()
    {
        using var activity = StartActivityWithBaggage((AttackerKey, "payload"));
        var provider = ProviderWith(o => _ = o.AllowedKeys.Add(AllowedKey));

        var context = DispatchContextInitializer.CreateDefaultContext(provider);

        context.Items.ContainsKey($"baggage.{AttackerKey}").ShouldBeFalse(
            "the key was supplied by whoever made the request, and copying it puts caller-chosen data on "
            + "the wire and into every downstream log and store");
    }

    /// <summary>
    /// SAFETY. With nothing configured, nothing propagates — the default a consumer gets without acting.
    /// </summary>
    [Fact]
    public void CopyNothingWhenNoAllowlistIsConfiguredAtAll()
    {
        using var activity = StartActivityWithBaggage((AllowedKey, "v"), (AttackerKey, "payload"));
        var provider = ProviderWith(configure: null);

        var context = DispatchContextInitializer.CreateDefaultContext(provider);

        context.Items.Keys.Any(k => k.StartsWith("baggage.", StringComparison.Ordinal)).ShouldBeFalse(
            "an unconfigured application has expressed no opt-in, and the default must be the safe one");
    }

    /// <summary>
    /// SAFETY — the growth harm, which the allowlist does not address. An oversized value is DROPPED, not
    /// truncated: a truncated value is read downstream as though it were whole.
    /// </summary>
    [Fact]
    public void DropAnAllowedValueThatExceedsThePerValueCapRatherThanTruncatingIt()
    {
        using var activity = StartActivityWithBaggage((AllowedKey, new string('x', 50)));
        var provider = ProviderWith(o =>
        {
            _ = o.AllowedKeys.Add(AllowedKey);
            o.MaxValueLength = 10;
        });

        var context = DispatchContextInitializer.CreateDefaultContext(provider);

        context.Items.ContainsKey($"baggage.{AllowedKey}").ShouldBeFalse(
            "a permitted key can still carry an oversized value, which is why the cap is not the allowlist. "
            + "Truncating would leave a value nothing downstream could tell was incomplete");
    }

    /// <summary>
    /// SAFETY. The entry cap bounds how many permitted entries ride a single message.
    /// </summary>
    [Fact]
    public void StopCopyingOnceTheEntryCapIsReached()
    {
        using var activity = StartActivityWithBaggage(("a", "1"), ("b", "2"), ("c", "3"));
        var provider = ProviderWith(o =>
        {
            _ = o.AllowedKeys.Add("a");
            _ = o.AllowedKeys.Add("b");
            _ = o.AllowedKeys.Add("c");
            o.MaxEntries = 2;
        });

        var context = DispatchContextInitializer.CreateDefaultContext(provider);

        context.Items.Keys.Count(k => k.StartsWith("baggage.", StringComparison.Ordinal)).ShouldBe(2,
            "the cap bounds amplification — without it, N permitted pairs ride every downstream message");
    }

    /// <summary>
    /// SAFETY. The total-size cap catches the shape the entry cap alone does not: few entries, large values.
    /// </summary>
    [Fact]
    public void StopCopyingOnceTheTotalSizeCapIsReached()
    {
        using var activity = StartActivityWithBaggage(("a", new string('x', 40)), ("b", new string('y', 40)));
        var provider = ProviderWith(o =>
        {
            _ = o.AllowedKeys.Add("a");
            _ = o.AllowedKeys.Add("b");
            o.MaxTotalLength = 45;
        });

        var context = DispatchContextInitializer.CreateDefaultContext(provider);

        context.Items.Keys.Count(k => k.StartsWith("baggage.", StringComparison.Ordinal)).ShouldBe(1,
            "two entries within the entry cap and the per-value cap can still exceed the total, which is "
            + "the case the entry cap cannot see");
    }

    /// <summary>
    /// SAFETY. The validator is reached through the registration a consumer actually writes, so a cap that
    /// could never admit an entry fails at startup instead of silently dropping every allowed key.
    /// </summary>
    /// <remarks>
    /// This arm exists because the first version of this fix shipped the validator and never registered it.
    /// A validator nobody resolves is inert, <c>ValidateOnStart</c> has nothing to run, and the failure is
    /// invisible: the configuration reads as though propagation were on while every entry is discarded. The
    /// arm therefore drives the <b>real</b> <c>AddDispatch()</c> path rather than registering the validator
    /// by hand — registering it here would prove only that the validator works, which was never in doubt.
    /// </remarks>
    [Fact]
    public void RefuseACapThatCouldNeverAdmitAnEntry_ThroughTheRealRegistration()
    {
        var services = new ServiceCollection();
        _ = services.AddDispatch();
        _ = services.Configure<BaggagePropagationOptions>(o => o.MaxEntries = 0);
        var provider = services.BuildServiceProvider();

        var thrown = Should.Throw<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<BaggagePropagationOptions>>().Value,
            "a non-positive cap drops every entry while the allowlist still reads as though keys were "
            + "permitted, so it must fail loudly at configuration time rather than quietly at run time");

        thrown.Message.ShouldContain(nameof(BaggagePropagationOptions.MaxEntries));
    }

    /// <summary>
    /// LIVENESS for the arm above. Without it, "the validator is wired" is satisfied by a validator that
    /// rejects every configuration, including the default one every consumer gets.
    /// </summary>
    [Fact]
    public void StillResolveTheOptionsWhenTheConfigurationIsValid()
    {
        var services = new ServiceCollection();
        _ = services.AddDispatch();
        var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<BaggagePropagationOptions>>().Value;

        options.AllowedKeys.ShouldBeEmpty("the default is deny, so an unconfigured application allows nothing");
        options.MaxEntries.ShouldBeGreaterThan(0, "a validator that rejected its own defaults would make "
            + "every consumer fail at startup, which is the opposite failure and just as broken");
    }

    /// <summary>
    /// LIVENESS, and the control the whole set rests on. Without it, every arm above is satisfied by
    /// deleting baggage propagation entirely.
    /// </summary>
    [Fact]
    public void StillPropagateAnAllowedKeyCarryingAnOrdinaryValue()
    {
        using var activity = StartActivityWithBaggage((AllowedKey, "acme"), (AttackerKey, "payload"));
        var provider = ProviderWith(o => _ = o.AllowedKeys.Add(AllowedKey));

        var context = DispatchContextInitializer.CreateDefaultContext(provider);

        context.Items[$"baggage.{AllowedKey}"].ShouldBe("acme",
            "this is a policy, not a removal: a key the application asked for must still arrive, or the "
            + "safety arms above are all satisfied by propagating nothing at all");
    }
}
