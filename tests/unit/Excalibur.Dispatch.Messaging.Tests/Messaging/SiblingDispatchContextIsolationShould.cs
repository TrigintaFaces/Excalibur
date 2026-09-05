// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Handlers;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Tests.Messaging;

/// <summary>
/// What two CONCURRENT nested dispatches observe of each other's ambient context.
/// </summary>
/// <remarks>
/// <para>
/// The ultra-local fast path publishes an ambient context so a nested dispatch can find its parent.
/// Sibling isolation is the property that publication is paying for and the one nothing else asserts:
/// when a handler fans out with <c>Task.WhenAll(dispatch(a), dispatch(b))</c>, neither child may
/// observe the other's context. If it could, a child would inherit the wrong causation -- and, worse,
/// the wrong tenant.
/// </para>
/// <para>
/// The two children are held at a barrier so they are genuinely in flight at the same time. A test
/// that let one finish before the other started would pass without ever exercising the property.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class SiblingDispatchContextIsolationShould(ITestOutputHelper output)
{
	private sealed record FanOutAction : IDispatchAction;

	private sealed record ChildAction(string Label) : IDispatchAction;

	private sealed class Probe
	{
		/// <summary>Both children must reach the barrier before either reads its ambient.</summary>
		public CountdownEvent Arrived { get; } = new(2);

		public ConcurrentDictionary<string, string?> AmbientMessageIdByLabel { get; } = new(StringComparer.Ordinal);

		public ConcurrentDictionary<string, string?> AmbientTenantByLabel { get; } = new(StringComparer.Ordinal);

		public ConcurrentBag<string> Ran { get; } = [];
	}

	/// <summary>Declares no context, so it stays ultra-local fast-path eligible.</summary>
	private sealed class FanOutHandler(IDispatcher dispatcher, Probe probe) : IActionHandler<FanOutAction>
	{
		public async Task HandleAsync(FanOutAction action, CancellationToken cancellationToken)
		{
			var a = DispatcherContextExtensions.DispatchAsync(dispatcher, new ChildAction("A"), cancellationToken);
			var b = DispatcherContextExtensions.DispatchAsync(dispatcher, new ChildAction("B"), cancellationToken);
			_ = await Task.WhenAll(a, b).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Declares no context either, so it reads the ambient the framework published for it -- which is
	/// exactly the value a nested dispatch from here would inherit.
	/// </summary>
	private sealed class ChildHandler(Probe probe) : IActionHandler<ChildAction>
	{
		public async Task HandleAsync(ChildAction action, CancellationToken cancellationToken)
		{
			probe.Ran.Add(action.Label);

			// Park both children inside their handlers simultaneously. Without this the second
			// dispatch would not begin until the first had completed and popped.
			_ = probe.Arrived.Signal();
			await Task.Run(() => probe.Arrived.Wait(TimeSpan.FromSeconds(10)), cancellationToken).ConfigureAwait(false);

			var ambient = MessageContextHolder.Current;
			probe.AmbientMessageIdByLabel[action.Label] = ambient?.MessageId;
			probe.AmbientTenantByLabel[action.Label] = ambient?.GetTenantId();
		}
	}

	[Fact]
	public async Task NotLetOneConcurrentChildObserveTheOtherSContext()
	{
		var probe = new Probe();
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch();
		_ = services.AddSingleton(probe);
		_ = services.AddTransient<IActionHandler<FanOutAction>, FanOutHandler>();
		_ = services.AddTransient<IActionHandler<ChildAction>, ChildHandler>();

		await using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var outer = new MessageContext { MessageId = "outer-fanout-id", CorrelationId = "correlation-FANOUT" };
		var identity = outer.GetOrCreateIdentityFeature();
		identity.TenantId = "tenant-FANOUT";
		outer.Initialize(provider);

		_ = await dispatcher.DispatchAsync(new FanOutAction(), outer, TestContext.Current.CancellationToken)
			.ConfigureAwait(false);

		var ambientA = probe.AmbientMessageIdByLabel.GetValueOrDefault("A");
		var ambientB = probe.AmbientMessageIdByLabel.GetValueOrDefault("B");
		output.WriteLine($"children that ran        = {string.Join(",", probe.Ran.Order(StringComparer.Ordinal))}");
		output.WriteLine($"ambient MessageId in A   = {ambientA ?? "<null>"}");
		output.WriteLine($"ambient MessageId in B   = {ambientB ?? "<null>"}");
		output.WriteLine($"ambient TenantId in A    = {probe.AmbientTenantByLabel.GetValueOrDefault("A") ?? "<null>"}");
		output.WriteLine($"ambient TenantId in B    = {probe.AmbientTenantByLabel.GetValueOrDefault("B") ?? "<null>"}");

		// Liveness: both children really were in flight together, so the assertions below mean something.
		probe.Ran.Order(StringComparer.Ordinal).ShouldBe(["A", "B"]);

		// Safety: each child sees an ambient, and it is not the same one.
		ambientA.ShouldNotBeNull();
		ambientB.ShouldNotBeNull();
		ambientA.ShouldNotBe(ambientB);

		// The outer context is the parent, never what a child observes as its own.
		ambientA.ShouldNotBe("outer-fanout-id");
		ambientB.ShouldNotBe("outer-fanout-id");
	}
}
