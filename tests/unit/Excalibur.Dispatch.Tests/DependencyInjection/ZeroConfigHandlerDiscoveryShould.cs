// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Delivery;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Tests.DependencyInjection;

/// <summary>
/// Binds the behaviour of a bare <c>services.AddDispatch()</c> — the call every getting-started
/// document teaches — to the discovery the documentation promises.
/// </summary>
/// <remarks>
/// <para>
/// The two <c>AddDispatch</c> overloads previously disagreed. A bare call binds to the
/// <c>params Assembly[]?</c> overload, which scanned nothing and registered no handlers, while the
/// <c>Action&lt;IDispatchBuilder&gt;</c> overload — which a bare call cannot reach, because its parameter
/// has no default — carried an entry-assembly fallback. The failure was silent: no compile error, no
/// startup error, just a "no handler registered" fault on the consumer's first dispatch.
/// </para>
/// <para>
/// These arms assert emitted behaviour — the handler actually runs — not that a registration call was
/// made. A container that registers a descriptor nobody can dispatch to satisfies a registration-presence
/// assertion and still fails the consumer.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "DI-ZERO-CONFIG")]
public sealed class ZeroConfigHandlerDiscoveryShould
{
	// ---- LIVENESS. The arm the defect broke. ----

	[Fact]
	public async Task DispatchToAHandlerInTheEntryAssemblyAfterABareAddDispatch()
	{
		// The premise the assertion rests on: this test's handler really is in the entry assembly, so
		// "the entry assembly was scanned" and "this handler was found" are the same claim. Without this,
		// a green result would be ambiguous between a working fallback and a handler found some other way.
		Assembly.GetEntryAssembly().ShouldBe(
			typeof(ZeroConfigProbeAction).Assembly,
			"this arm proves entry-assembly discovery, so the probe handler must live in the entry assembly");

		var services = new ServiceCollection();

		// A real host (WebApplication.CreateBuilder) supplies logging. Without it the container cannot
		// activate the dispatch pipeline at all, and every arm below would fail for a reason that has
		// nothing to do with handler discovery.
		_ = services.AddLogging();

		// Exactly what docs-site/docs/getting-started/index.md tells a reader to write. Nothing else.
		_ = services.AddDispatch();

		using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var probe = new ZeroConfigProbeAction();
		var result = await dispatcher.DispatchAsync(probe, TestContext.Current.CancellationToken);

		result.IsSuccess.ShouldBeTrue(
			$"a bare AddDispatch() must reach a handler in the entry assembly, as every getting-started "
			+ $"document promises. Dispatch failed with: {result.ErrorMessage}");

		probe.Handled.ShouldBeTrue(
			"the dispatch reported success, but the handler never ran — a success result with no handler "
			+ "invocation is the same silent failure in a different costume");
	}

	// ---- SAFETY. Without these, a fallback that fired unconditionally, or one that trampled the ----
	// ---- consumer's own registration, would pass the liveness arm above.                        ----

	[Fact]
	public async Task NotOverrideAHandlerTheConsumerRegisteredExplicitly()
	{
		var services = new ServiceCollection();

		// A real host (WebApplication.CreateBuilder) supplies logging. Without it the container cannot
		// activate the dispatch pipeline at all, and every arm below would fail for a reason that has
		// nothing to do with handler discovery.
		_ = services.AddLogging();

		// The consumer names their own handler for this message. Zero-config discovery must yield to it:
		// an implicit scan must never outrank an explicit registration.
		_ = services.AddTransient<IActionHandler<ZeroConfigOverriddenAction>, ExplicitOverrideHandler>();
		_ = services.AddDispatch();

		// Non-vacuity, and it took two attempts to state correctly. The scan really did run in THIS
		// container — proved below by a handler it discovered for a message the consumer never claimed.
		// Without that, "the consumer's handler won" would also be true of a container where no scan
		// happened at all, which is precisely what this arm looked like before the fallback existed.
		//
		// Note what is NOT asserted: that ScannedLoserHandler was registered and lost. It is never
		// registered. RegisterMessageHandlers uses TryAdd, which matches on the service type alone, so the
		// consumer's prior claim on IActionHandler<ZeroConfigOverriddenAction> makes the scan skip every
		// competitor for that message rather than add one that loses. Stronger than deference on
		// resolution, and a different property than the one first written here.
		services.Any(static d => d.ServiceType == typeof(IActionHandler<ZeroConfigProbeAction>))
			.ShouldBeTrue(
				"the entry-assembly scan must have run in this container — otherwise this arm proves "
				+ "nothing about whose registration survives it");

		services.Count(static d => d.ServiceType == typeof(IActionHandler<ZeroConfigOverriddenAction>))
			.ShouldBe(
				1,
				"the consumer claimed this message type before the scan, so the scan must not have added a "
				+ "second registration for it");

		using var provider = services.BuildServiceProvider();

		var probe = new ZeroConfigOverriddenAction();
		var result = await provider
			.GetRequiredService<IDispatcher>()
			.DispatchAsync(probe, TestContext.Current.CancellationToken);

		result.IsSuccess.ShouldBeTrue($"dispatch failed with: {result.ErrorMessage}");
		probe.HandledBy.ShouldBe(
			nameof(ExplicitOverrideHandler),
			"the consumer registered ExplicitOverrideHandler by hand; the entry-assembly scan must not "
			+ "replace it with the type it happened to find first");
	}

	[Fact]
	public async Task StillHonourAnExplicitlySuppliedAssembly()
	{
		var services = new ServiceCollection();

		// A real host (WebApplication.CreateBuilder) supplies logging. Without it the container cannot
		// activate the dispatch pipeline at all, and every arm below would fail for a reason that has
		// nothing to do with handler discovery.
		_ = services.AddLogging();

		// The existing, documented opt-in path. The fallback must not disturb it.
		_ = services.AddDispatch(typeof(ZeroConfigProbeAction).Assembly);

		using var provider = services.BuildServiceProvider();

		var probe = new ZeroConfigProbeAction();
		var result = await provider
			.GetRequiredService<IDispatcher>()
			.DispatchAsync(probe, TestContext.Current.CancellationToken);

		result.IsSuccess.ShouldBeTrue($"dispatch failed with: {result.ErrorMessage}");
		probe.Handled.ShouldBeTrue("an explicitly supplied assembly must still be scanned");
	}

	[Fact]
	public void NotRegisterABuilderSentinelFromTheBareCall()
	{
		// The bare call must remain distinguishable from AddDispatch(configure): the sentinel is what
		// makes a later builder-based configuration win. A fallback implemented by quietly delegating to
		// the builder overload would set it, and would silently change the ordering contract.
		var services = new ServiceCollection();

		// A real host (WebApplication.CreateBuilder) supplies logging. Without it the container cannot
		// activate the dispatch pipeline at all, and every arm below would fail for a reason that has
		// nothing to do with handler discovery.
		_ = services.AddLogging();

		_ = services.AddDispatch();

		services.Any(static d => d.ServiceType.Name == "DispatchBuilderSentinel").ShouldBeFalse(
			"a bare AddDispatch() must not materialise the builder pipeline");
	}

	// ---- Probes. Top-level internal types so the assembly scan sees them exactly as it would see a ----
	// ---- consumer's own handlers.                                                                  ----

	internal sealed class ZeroConfigProbeAction : IDispatchAction
	{
		public bool Handled { get; set; }
	}

	internal sealed class ZeroConfigProbeHandler : IActionHandler<ZeroConfigProbeAction>
	{
		public Task HandleAsync(ZeroConfigProbeAction message, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(message);
			message.Handled = true;
			return Task.CompletedTask;
		}
	}

	internal sealed class ZeroConfigOverriddenAction : IDispatchAction
	{
		public string? HandledBy { get; set; }
	}

	/// <summary>The handler the consumer registers by hand.</summary>
	internal sealed class ExplicitOverrideHandler : IActionHandler<ZeroConfigOverriddenAction>
	{
		public Task HandleAsync(ZeroConfigOverriddenAction message, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(message);
			message.HandledBy = nameof(ExplicitOverrideHandler);
			return Task.CompletedTask;
		}
	}

	/// <summary>
	/// A competing handler for the same message, discoverable only by the assembly scan. Its presence is
	/// what gives the override arm something to fail against.
	/// </summary>
	internal sealed class ScannedLoserHandler : IActionHandler<ZeroConfigOverriddenAction>
	{
		public Task HandleAsync(ZeroConfigOverriddenAction message, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(message);
			message.HandledBy = nameof(ScannedLoserHandler);
			return Task.CompletedTask;
		}
	}
}
