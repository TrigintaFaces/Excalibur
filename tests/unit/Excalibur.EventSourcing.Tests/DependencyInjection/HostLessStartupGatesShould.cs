// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.DependencyInjection;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.EventSourcing.Tests.DependencyInjection;

/// <summary>
/// Non-vacuous safety+liveness lock for the host-less startup trigger against a REAL prerequisite validator:
/// a consumer who builds an <see cref="IServiceProvider"/> and never starts a host must get the same
/// fail-fast verdict from <c>ValidateStartupGates()</c> that a host gets from <c>StartAsync</c>.
/// </summary>
/// <remarks>
/// <para>
/// The trigger previously ran only the <c>ValidateOnStart()</c> options gates, so a host-less container
/// missing its <see cref="IEventStore"/> provider returned cleanly from the documented call and then failed
/// at first aggregate load — the silent-pass the gates exist to prevent. Both arms per testing-patterns §3:
/// </para>
/// <list type="bullet">
/// <item>SAFETY — a misconfigured host-less container throws from the trigger.</item>
/// <item>
/// LIVENESS — a correctly configured one returns clean AND no hosted service is started, so the check
/// cannot be satisfied by throwing always, nor by starting the whole host (which would run outbox
/// processors and leader election in a container the consumer never intended to run as a host).
/// </item>
/// </list>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class HostLessStartupGatesShould
{
	private sealed record TestEvent;

	/// <summary>Records whether anything started it, so the no-double-start arm is observable.</summary>
	private sealed class StartProbe : IHostedService
	{
		public bool Started { get; private set; }

		public Task StartAsync(CancellationToken cancellationToken)
		{
			Started = true;
			return Task.CompletedTask;
		}

		public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
	}

	private static ServiceCollection MisconfiguredHostLessContainer()
	{
		// AddEventSourcing without a provider extension: no IEventStore is registered. A host would refuse
		// to start; a host-less consumer has only ValidateStartupGates() to tell them.
		var services = new ServiceCollection();
		_ = services.AddExcalibur(x => x.AddEventSourcing());
		return services;
	}

	private static ServiceCollection CorrectlyConfiguredHostLessContainer()
	{
		var services = new ServiceCollection();
		_ = services.AddExcalibur(x => x.AddEventSourcing(es => es.RegisterEventTypes<TestEvent>()));
		services.AddKeyedSingleton<IEventStore>("default", (_, _) => A.Fake<IEventStore>());
		return services;
	}

	[Fact]
	public void Fail_a_host_less_container_that_is_missing_its_event_store()
	{
		using var provider = MisconfiguredHostLessContainer().BuildServiceProvider(validateScopes: false);

		// The premise, measured rather than assumed: building and resolving fires nothing, so the
		// misconfiguration is sitting in this container un-surfaced. If the trigger did not run the
		// prerequisite validators, the assertion below would not throw — which is what makes it non-vacuous.
		_ = provider.GetServices<IHostedService>()
			.OfType<EventSourcingPrerequisiteValidator>()
			.SingleOrDefault()
			.ShouldNotBeNull();

		// SAFETY — the documented host-less call surfaces the same failure the host would have surfaced.
		var thrown = Should.Throw<InvalidOperationException>(() => provider.ValidateStartupGates());
		thrown.Message.ShouldContain("IEventStore", Case.Sensitive);
		thrown.Message.ShouldContain("AddEventSourcing", Case.Sensitive);
	}

	[Fact]
	public void Let_a_correctly_configured_host_less_container_pass_and_return_it_for_chaining()
	{
		// LIVENESS — the trigger must not be satisfiable by throwing always.
		using var provider = CorrectlyConfiguredHostLessContainer().BuildServiceProvider(validateScopes: false);

		var returned = provider.ValidateStartupGates();

		returned.ShouldBeSameAs(provider);
	}

	[Fact]
	public void Start_no_hosted_service_while_running_the_gates()
	{
		// LIVENESS — running the gates must not start the host. Resolving IEnumerable<IHostedService> and
		// starting it would fire outbox processors, leader election, and every other background service in a
		// container the consumer never intended to run as a host; the probe makes that observable.
		var services = CorrectlyConfiguredHostLessContainer();
		var probe = new StartProbe();
		services.AddSingleton<IHostedService>(probe);

		using var provider = services.BuildServiceProvider(validateScopes: false);

		_ = provider.ValidateStartupGates();

		probe.Started.ShouldBeFalse(
			"ValidateStartupGates() must run the prerequisite validators without starting hosted services.");
	}

	[Fact]
	public void Keep_the_hosted_registration_so_the_host_path_is_unchanged()
	{
		// The marker registration is ADDITIVE: a host still starts these validators through IHostedService.
		using var provider = MisconfiguredHostLessContainer().BuildServiceProvider(validateScopes: false);

		provider.GetServices<IHostedService>()
			.OfType<EventSourcingPrerequisiteValidator>()
			.Count()
			.ShouldBe(1);

		provider.GetServices<IStartupPrerequisiteValidator>()
			.OfType<EventSourcingPrerequisiteValidator>()
			.Count()
			.ShouldBe(1);
	}
}
