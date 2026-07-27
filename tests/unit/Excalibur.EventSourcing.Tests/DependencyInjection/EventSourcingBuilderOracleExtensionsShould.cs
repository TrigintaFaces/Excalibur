// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Oracle;
using Excalibur.EventSourcing.Oracle.DependencyInjection;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

namespace Excalibur.EventSourcing.Tests.DependencyInjection;

/// <summary>
/// evrn1e — Oracle was the only event-store provider missing the fluent
/// <c>es =&gt; es.UseOracle(...)</c> composition shape (9 others had one). <see cref="EventSourcingBuilderOracleExtensions.UseOracle"/>
/// adds it by delegating to the validated <c>AddOracleEventStore</c>/<c>AddOracleSnapshotStore</c> path, so it
/// MUST register BOTH the event store and the snapshot store keyed as "default" — the surface the core resolves.
/// A real <see cref="ServiceProvider"/> is built through the production registration path; no Oracle container
/// is required because both store constructors capture the connection factory lazily.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
[Trait("Database", "Oracle")]
public sealed class EventSourcingBuilderOracleExtensionsShould
{
	private const string UnusedConnectionString =
		"Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=unused)(PORT=1521))"
		+ "(CONNECT_DATA=(SERVICE_NAME=FREE)));User Id=x;Password=y;";

	private static IEventSourcingBuilder BuilderOver(IServiceCollection services)
	{
		var builder = A.Fake<IEventSourcingBuilder>();
		A.CallTo(() => builder.Services).Returns(services);
		return builder;
	}

	/// <summary>
	/// LIVENESS — <c>UseOracle</c> registers BOTH stores keyed "default", resolvable through a real provider
	/// (lazy connection factory ⇒ no live Oracle). This is the composition the core consumes; a registration
	/// that wired only one store (or a non-keyed one) would fail here.
	/// </summary>
	[Fact]
	public void RegisterOracleEventAndSnapshotStores_KeyedDefault_ResolvableWithoutLiveDb()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = BuilderOver(services).UseOracle(options => options.ConnectionString = UnusedConnectionString);

		using var provider = services.BuildServiceProvider();

		provider.GetRequiredKeyedService<IEventStore>("default").ShouldBeOfType<OracleEventStore>();
		provider.GetRequiredKeyedService<ISnapshotStore>("default").ShouldBeOfType<OracleSnapshotStore>();
	}

	/// <summary>
	/// LIVENESS — fluent chaining returns the same builder (composition must compose).
	/// </summary>
	[Fact]
	public void ReturnSameBuilder_ForChaining()
	{
		var services = new ServiceCollection();
		var builder = BuilderOver(services);

		var result = builder.UseOracle(options => options.ConnectionString = UnusedConnectionString);

		result.ShouldBeSameAs(builder);
	}

	/// <summary>
	/// SAFETY — a null builder is rejected (the extension-method receiver guard).
	/// </summary>
	[Fact]
	public void Throw_WhenBuilderIsNull()
	{
		IEventSourcingBuilder builder = null!;

		_ = Should.Throw<ArgumentNullException>(
			() => builder.UseOracle(options => options.ConnectionString = UnusedConnectionString));
	}

	/// <summary>
	/// SAFETY — a null event-store configuration is rejected (the required configure action).
	/// </summary>
	[Fact]
	public void Throw_WhenConfigureEventStoreIsNull()
	{
		var services = new ServiceCollection();
		var builder = BuilderOver(services);

		_ = Should.Throw<ArgumentNullException>(
			() => builder.UseOracle((Action<OracleEventStoreOptions>)null!));
	}
}
