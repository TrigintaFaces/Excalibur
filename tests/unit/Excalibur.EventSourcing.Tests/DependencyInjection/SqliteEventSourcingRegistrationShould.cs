// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Sqlite;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Excalibur.EventSourcing.Tests.DependencyInjection;

/// <summary>
/// Binds what <c>UseSqlite</c> must publish to the container, through the registration path a consumer
/// actually calls.
/// </summary>
/// <remarks>
/// <para>
/// Every arm here builds a real <see cref="ServiceProvider"/> from <c>AddExcalibur(x =&gt;
/// x.AddEventSourcing(es =&gt; es.UseSqlite(...)))</c> and resolves through it. That is load-bearing rather
/// than ceremonial: the core registers the non-keyed <see cref="IEventStore"/> alias <em>before</em> the
/// provider extension runs, and the alias forwards to the keyed <c>"default"</c> registration. A suite
/// that drove the event-sourcing builder directly, or that constructed the store by hand, would never
/// have the alias in the collection at all -- so it would pass against a provider that publishes neither
/// key, which is exactly the state this file exists to reject.
/// </para>
/// <para>
/// Both supported shapes are covered, because they are separate public entry points that reach the same
/// private core: the delegate overload and the <see cref="IConfiguration"/> overload.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
[Trait("Database", "Sqlite")]
public sealed class SqliteEventSourcingRegistrationShould
{
	private const string ConnectionString = "Data Source=:memory:";

	private static ServiceProvider BuildFromDelegateOverload() =>
		BuildProvider(es => es.UseSqlite(sqlite => sqlite.ConnectionString = ConnectionString));

	private static ServiceProvider BuildFromConfigurationOverload()
	{
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionString"] = ConnectionString,
			})
			.Build();

		return BuildProvider(es => es.UseSqlite(configuration));
	}

	private static ServiceProvider BuildProvider(Action<IEventSourcingBuilder> useProvider)
	{
		var services = new ServiceCollection();
		_ = services.AddExcalibur(x => x.AddEventSourcing(useProvider));
		return services.BuildServiceProvider(validateScopes: false);
	}

	public static TheoryData<string> SupportedShapes => new() { "delegate", "configuration" };

	private static ServiceProvider BuildShape(string shape) => shape switch
	{
		"delegate" => BuildFromDelegateOverload(),
		"configuration" => BuildFromConfigurationOverload(),
		_ => throw new ArgumentOutOfRangeException(nameof(shape), shape, "Unknown registration shape."),
	};

	/// <summary>
	/// The keyed <c>"default"</c> event store is what the core resolves through -- repositories, erasure,
	/// and event notification all ask for it by that key.
	/// </summary>
	[Theory]
	[MemberData(nameof(SupportedShapes))]
	public void PublishTheKeyedDefaultEventStore(string shape)
	{
		using var provider = BuildShape(shape);

		provider.GetRequiredKeyedService<IEventStore>("default")
			.ShouldBeOfType<SqliteEventStore>();
	}

	/// <summary>The provider-named key is the addressable alias a multi-provider host resolves by name.</summary>
	[Theory]
	[MemberData(nameof(SupportedShapes))]
	public void PublishTheProviderNamedEventStore(string shape)
	{
		using var provider = BuildShape(shape);

		provider.GetRequiredKeyedService<IEventStore>("sqlite")
			.ShouldBeOfType<SqliteEventStore>();
	}

	/// <summary>
	/// The non-keyed convenience injection a consumer writes as a plain <c>IEventStore</c> constructor
	/// parameter. It is the core's forwarding alias, so it resolves only once the keyed default exists.
	/// </summary>
	[Theory]
	[MemberData(nameof(SupportedShapes))]
	public void ResolveTheNonKeyedEventStoreAlias(string shape)
	{
		using var provider = BuildShape(shape);

		provider.GetRequiredService<IEventStore>().ShouldBeOfType<SqliteEventStore>();
	}

	/// <summary>Every route must reach one store, not three -- a store holds a connection.</summary>
	[Theory]
	[MemberData(nameof(SupportedShapes))]
	public void ReachOneEventStoreThroughEveryRoute(string shape)
	{
		using var provider = BuildShape(shape);

		var byDefaultKey = provider.GetRequiredKeyedService<IEventStore>("default");
		var byProviderKey = provider.GetRequiredKeyedService<IEventStore>("sqlite");
		var byAlias = provider.GetRequiredService<IEventStore>();

		byProviderKey.ShouldBeSameAs(byDefaultKey);
		byAlias.ShouldBeSameAs(byDefaultKey);
	}

	/// <summary>The snapshot store is published on the same three routes as the event store.</summary>
	[Theory]
	[MemberData(nameof(SupportedShapes))]
	public void PublishTheSnapshotStoreOnEveryRoute(string shape)
	{
		using var provider = BuildShape(shape);

		var byDefaultKey = provider.GetRequiredKeyedService<ISnapshotStore>("default");
		byDefaultKey.ShouldBeOfType<SqliteSnapshotStore>();
		provider.GetRequiredKeyedService<ISnapshotStore>("sqlite").ShouldBeSameAs(byDefaultKey);
		provider.GetRequiredService<ISnapshotStore>().ShouldBeSameAs(byDefaultKey);
	}

	/// <summary>
	/// LIVENESS. The startup gate must let a correctly-configured SQLite host start. It reports a missing
	/// event store by probing the keyed <c>"default"</c> registration, so a provider that publishes no key
	/// is refused at startup with a message stating it has no event store -- while it plainly does.
	/// </summary>
	[Theory]
	[MemberData(nameof(SupportedShapes))]
	public async Task StartTheHostPrerequisiteGate(string shape)
	{
		using var provider = BuildShape(shape);

		var validator = provider.GetServices<IHostedService>()
			.OfType<EventSourcingPrerequisiteValidator>()
			.Single();

		await validator.StartAsync(CancellationToken.None);
	}
}
