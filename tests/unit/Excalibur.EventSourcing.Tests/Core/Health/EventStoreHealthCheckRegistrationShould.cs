using Excalibur.EventSourcing.Queries;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace Excalibur.EventSourcing.Tests.Core.Health;

/// <summary>
/// The registration refuses a composition it cannot serve AT STARTUP, rather than at the first scrape.
/// </summary>
/// <remarks>
/// <para>
/// The check reads the global stream, so a composition with no <see cref="IGlobalStreamQuery" /> cannot
/// be served. Three answers were available and two are dishonest. Handing back a check that probes
/// something else instead — an aggregate load, say — makes a green mean one thing on one composition and
/// a different thing on another, with nothing in the result telling the operator which they got. Handing
/// back a check that throws when scraped turns a registration mistake into a permanent unhealthy signal,
/// which most orchestrators report as a broken application rather than as a misconfiguration.
/// </para>
/// <para>
/// So it refuses at startup and names what to do. Document-store providers supply no global-stream
/// reader, so this is the path a consumer on one of them reaches by calling the check explicitly.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class EventStoreHealthCheckRegistrationShould
{
	/// <summary>
	/// SAFETY: a composition with no global-stream reader is refused, and the message is actionable.
	/// </summary>
	[Fact]
	public void RefuseACompositionWithNoGlobalStreamReader()
	{
		var provider = Build(registerGlobalStream: false);

		var thrown = Should.Throw<InvalidOperationException>(
			() => ValidateAll(provider),
			"without a reader the check has nothing to probe, and discovering that when the health "
			+ "endpoint is first scraped reports it as an unhealthy application rather than a "
			+ "misconfiguration.");

		thrown.Message.ShouldContain(
			"global-stream reader",
			Shouldly.Case.Insensitive,
			"the refusal must name the missing dependency, not just that something is wrong.");
	}

	/// <summary>
	/// LIVENESS: a composition that DOES supply a reader starts, and the check is constructible.
	/// </summary>
	/// <remarks>
	/// Without this arm the one above is satisfied by a validator that refuses every composition, which
	/// would break every relational provider while still passing its own test.
	/// </remarks>
	[Fact]
	public void AdmitACompositionThatSuppliesAGlobalStreamReader()
	{
		var provider = Build(registerGlobalStream: true);

		ValidateAll(provider);

		var registration = provider
			.GetRequiredService<Microsoft.Extensions.Options.IOptions<HealthCheckServiceOptions>>()
			.Value.Registrations.Single(r => r.Name == "event-store");

		_ = registration.Factory(provider).ShouldNotBeNull(
			"the whole point of refusing the other composition is that THIS one yields a working check.");
	}

	/// <summary>
	/// The validator must be reachable by a host that runs no hosted services.
	/// </summary>
	/// <remarks>
	/// A serverless or pull-driven host never starts an <see cref="IHostedService" />. If the refusal
	/// were registered only that way it would silently not happen exactly where a misconfiguration is
	/// hardest to diagnose, so both registrations are asserted.
	/// </remarks>
	[Fact]
	public void RegisterTheRefusalForBothHostShapes()
	{
		var provider = Build(registerGlobalStream: true);

		provider.GetServices<IStartupPrerequisiteValidator>()
			.Any(v => v is Excalibur.EventSourcing.DependencyInjection.EventStoreHealthCheckPrerequisiteValidator)
			.ShouldBeTrue("a host that runs no hosted services drives the startup gate instead.");

		provider.GetServices<IHostedService>()
			.Any(v => v is Excalibur.EventSourcing.DependencyInjection.EventStoreHealthCheckPrerequisiteValidator)
			.ShouldBeTrue("a host that does run hosted services must refuse on start.");
	}

	/// <summary>
	/// SAFETY: a reader that is REGISTERED but cannot be CONSTRUCTED is refused at startup too.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is the arm the other two cannot be: both register the fake as an already-built INSTANCE, and
	/// an instance satisfies "is it registered" and "can it be built" identically — so neither fixture
	/// can tell a registration test from a constructibility test, and no mutation of the validator
	/// reddens them. The fixture state, not the production code, is what was missing.
	/// </para>
	/// <para>
	/// The composition this stands for is real rather than contrived. The SQL Server provider registers
	/// the global-stream reader through a factory that throws when a cold store is present and the
	/// primary connection factory was supplied by the consumer instead of owned. A registration test
	/// admits that host; the throw then surfaces in the health-check registration's own factory, which
	/// runs lazily at the first scrape — an orchestrator reads that as a broken application rather than
	/// a misconfiguration, which is the whole thing this validator exists to prevent.
	/// </para>
	/// </remarks>
	[Fact]
	public void RefuseAReaderThatIsRegisteredButCannotBeConstructed()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton<IGlobalStreamQuery>(
			_ => throw new InvalidOperationException("Global archive hydration requires an owned primary connection factory."));
		_ = services.AddHealthChecks().AddEventStoreHealthCheck();

		using var provider = services.BuildServiceProvider();

		_ = Should.Throw<Exception>(
			() => ValidateAll(provider),
			"a reader that cannot be constructed must be refused at STARTUP. Under a registration-only "
			+ "predicate this composition is admitted and the failure appears at the first scrape, which "
			+ "is the failure mode being moved to startup rather than one to leave there.");

		// And the discriminating half: the refusal must happen at Validate(), not merely somewhere. If
		// the validator admitted this host, the exception above would still be thrown -- by the health
		// check's own factory below -- and the arm would pass while proving the opposite.
		var registration = provider
			.GetRequiredService<Microsoft.Extensions.Options.IOptions<HealthCheckServiceOptions>>()
			.Value.Registrations.Single(r => r.Name == "event-store");

		_ = Should.Throw<Exception>(
			() => registration.Factory(provider),
			"stated so the arm above cannot be read as proving more than it does: this composition fails "
			+ "at the factory as well. What the arm above establishes is that it ALSO fails earlier, at "
			+ "startup, which is the only place a consumer can act on it.");
	}

	private static ServiceProvider Build(bool registerGlobalStream)
	{
		var services = new ServiceCollection();

		// AddHealthChecks registers a hosted service of its own, and enumerating IHostedService
		// activates it -- which needs logging. Supplied so this fixture measures OUR registration
		// rather than failing on an unrelated dependency of the health-check framework.
		_ = services.AddLogging();

		if (registerGlobalStream)
		{
			services.AddSingleton(A.Fake<IGlobalStreamQuery>());
		}

		_ = services.AddHealthChecks().AddEventStoreHealthCheck();

		return services.BuildServiceProvider();
	}

	private static void ValidateAll(IServiceProvider provider)
	{
		foreach (var validator in provider.GetServices<IStartupPrerequisiteValidator>())
		{
			validator.Validate();
		}
	}
}
