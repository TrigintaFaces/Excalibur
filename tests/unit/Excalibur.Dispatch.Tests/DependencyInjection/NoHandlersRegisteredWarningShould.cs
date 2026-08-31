// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Handlers;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Tests.Shared.Helpers;

namespace Excalibur.Dispatch.Tests.DependencyInjection;

/// <summary>
/// Binds the start-up diagnostic that replaced the entry-assembly scan inside
/// <c>AddDispatch(Action&lt;IDispatchBuilder&gt;)</c>.
/// </summary>
/// <remarks>
/// <para>
/// That overload used to scan the entry assembly whenever its lambda named no handler. The scan is gone —
/// one branch in the body made the trim analyser treat every caller as reflective, including callers who
/// reflect over nothing — and a composition that names no handler now starts with nothing registered.
/// </para>
/// <para>
/// Silence there would be worse than the warning it replaced. An action or a query with no handler throws
/// on the first dispatch, but an event with no handler only logs, so an empty composition can run for a
/// long time discarding events. Hence a warning at start-up naming both remedies.
/// </para>
/// <para>
/// Both arms are required, and the second is the one that matters most. A diagnostic that fires on every
/// composition is noise, and noise gets filtered — after which the first arm still passes and the warning
/// no longer reaches anyone.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "DI-ZERO-CONFIG")]
public sealed class NoHandlersRegisteredWarningShould
{
	// ---- LIVENESS. A composition that registered nothing must say so, and must still start. ----

	[Fact]
	public async Task WarnNamingBothRemediesWhenTheConfigureLambdaRegistersNoHandler()
	{
		var captured = new CapturingLoggerProvider();
		var services = new ServiceCollection();
		_ = services.AddLogging(b => b.AddProvider(captured).SetMinimumLevel(LogLevel.Trace));

		// The composition this diagnostic exists for: a lambda that configures the pipeline and never
		// names a handler. It used to be rescued by an implicit entry-assembly scan.
		_ = services.AddDispatch(static _ => { });

		using var provider = services.BuildServiceProvider();

		// Non-vacuity for the arm below: the registry really is empty here, so "warned" is a claim about
		// an empty composition rather than about a start-up hook that fires unconditionally.
		provider.GetRequiredService<IHandlerRegistry>().GetAll().ShouldBeEmpty(
			"this arm asserts the empty-composition warning, so the composition must actually be empty");

		await StartHostedServicesAsync(provider);

		var warning = captured.Entries.SingleOrDefault(
			static e => e.Level == LogLevel.Warning && e.Message.Contains(
				"No message handlers are registered",
				StringComparison.Ordinal));

		warning.ShouldNotBeNull(
			"a composition with no handlers silently discards every event it publishes; the framework must "
			+ "say so once at start-up rather than leaving the consumer to discover it in production");

		// The remedies are asserted by substring rather than by "a warning occurred", because a warning
		// that does not name the call that fixes it sends the reader to the source or to the docs.
		warning.Message.ShouldContain(
			"AddDiscoveredHandlers()",
			Case.Sensitive,
			"the source-generated registration is the remedy that also works under trimming and ahead-of-"
			+ "time compilation, so it must be named first");

		warning.Message.ShouldContain(
			"AddHandlersFromAssembly(",
			Case.Sensitive,
			"the scanning remedy must be named too — it is the one that reproduces the behaviour the "
			+ "implicit fallback used to provide");
	}

	[Fact]
	public async Task StartTheHostAnywayWhenNoHandlerIsRegistered()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(static _ => { });

		using var provider = services.BuildServiceProvider();

		// A send-only host that publishes to a transport and consumes nothing is a supported shape, and
		// Microsoft does not throw for AddControllers() with no controllers either. Warn, do not throw.
		await Should.NotThrowAsync(() => StartHostedServicesAsync(provider));
	}

	// ---- SAFETY. Without this, the arm above is satisfied by a hook that warns unconditionally. ----

	[Fact]
	public async Task StaySilentWhenTheConfigureLambdaRegistersHandlers()
	{
		var captured = new CapturingLoggerProvider();
		var services = new ServiceCollection();
		_ = services.AddLogging(b => b.AddProvider(captured).SetMinimumLevel(LogLevel.Trace));

		_ = services.AddDispatch(
			static dispatch => dispatch.AddHandlersFromAssembly(typeof(WarningProbeAction).Assembly));

		using var provider = services.BuildServiceProvider();

		// Non-vacuity. Without this the silence below would also be true of a container where the registry
		// was empty and the start-up hook never ran — the exact reading this arm must exclude.
		provider.GetRequiredService<IHandlerRegistry>().GetAll().ShouldNotBeEmpty(
			"this arm asserts silence for a composition that HAS handlers, so it must actually have some");

		await StartHostedServicesAsync(provider);

		captured.Entries
			.Where(static e => e.Message.Contains("No message handlers are registered", StringComparison.Ordinal))
			.ShouldBeEmpty(
				"the composition registered handlers, so warning about their absence is crying wolf — and a "
				+ "warning that fires on correct compositions is one people learn to filter out");
	}

	private static async Task StartHostedServicesAsync(IServiceProvider provider)
	{
		foreach (var hosted in provider.GetServices<IHostedService>())
		{
			await hosted.StartAsync(TestContext.Current.CancellationToken);
		}
	}

	private sealed record WarningProbeAction : IDispatchAction;

	private sealed class WarningProbeHandler : IActionHandler<WarningProbeAction>
	{
		public Task HandleAsync(WarningProbeAction message, CancellationToken cancellationToken) =>
			Task.CompletedTask;
	}
}
