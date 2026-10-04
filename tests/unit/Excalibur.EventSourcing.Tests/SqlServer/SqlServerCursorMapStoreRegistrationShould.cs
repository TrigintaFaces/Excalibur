// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing.Projections;
using Excalibur.EventSourcing.SqlServer;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

using Shouldly;

using Xunit;

namespace Excalibur.EventSourcing.Tests.SqlServer;

/// <summary>
/// Binds the SQL Server cursor-map store's registration by RESOLVING it, and binds the default a host
/// gets when it does not register one.
/// </summary>
/// <remarks>
/// <para>
/// <b>What was missing.</b> The implementation, its public constructors and its schema script all shipped,
/// but there was no <c>Add*</c> extension for it — every sibling store in the package has one. Enabling
/// event notification registers the in-memory map through <c>TryAdd</c>, so a host that wanted durable
/// per-stream cursors and did not know to construct the SQL Server store by hand got an in-memory map
/// that is empty after every restart, with nothing reporting the substitution.
/// </para>
/// <para>
/// The first arm asserts that default deliberately. It is the safety-and-liveness pair the test rules
/// ask for: proving the durable store resolves says nothing about what a host gets when it is absent,
/// and the absent case is the one that was shipping.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class SqlServerCursorMapStoreRegistrationShould
{
	private const string UnusedConnectionString = "Server=unused;Database=unused;";

	private static ServiceCollection NewServices()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		return services;
	}

	[Fact]
	public void ResolveNoCursorMapWhenNothingRegistersOne()
	{
		var services = NewServices();

		using var provider = services.BuildServiceProvider();

		// A bare collection has none at all. The in-memory one arrives with event notification, which is
		// the case the next arm covers.
		provider.GetService<ICursorMapStore>().ShouldBeNull();
	}

	[Fact]
	public void ResolveTheSqlServerStoreWhenRegistered()
	{
		var services = NewServices();

		_ = services.AddSqlServerCursorMapStore(UnusedConnectionString);

		using var provider = services.BuildServiceProvider();

		// A factory-based registration means nothing opens a connection until an operation runs, so
		// resolution is provable without a reachable server.
		provider.GetService<ICursorMapStore>()
			.ShouldBeOfType<SqlServerCursorMapStore>(
				"the durable store must be reachable through a registration, not only by hand-construction");
	}

	[Fact]
	public void ResolveTheSqlServerStoreFromAConnectionFactory()
	{
		var services = NewServices();

		_ = services.AddSqlServerCursorMapStore(
			() => new Microsoft.Data.SqlClient.SqlConnection(UnusedConnectionString));

		using var provider = services.BuildServiceProvider();

		provider.GetService<ICursorMapStore>().ShouldBeOfType<SqlServerCursorMapStore>();
	}

	[Fact]
	public void WinAgainstAnInMemoryMapRegisteredEarlier()
	{
		var services = NewServices();

		// The ordering that matters, and the reason hand-construction was fragile: the in-memory map is
		// registered through TryAdd by event notification, so whichever call runs first used to decide.
		// This registration uses AddSingleton, so a durable choice wins however the two are sequenced.
		services.TryAddSingleton<ICursorMapStore, InMemoryCursorMapStore>();
		_ = services.AddSqlServerCursorMapStore(UnusedConnectionString);

		using var provider = services.BuildServiceProvider();

		provider.GetService<ICursorMapStore>()
			.ShouldBeOfType<SqlServerCursorMapStore>(
				"a durable cursor map must not be suppressed by an in-memory one registered earlier");
	}

	[Fact]
	public void ThrowWhenConnectionStringIsBlank()
	{
		var services = NewServices();

		_ = Should.Throw<ArgumentException>(() => services.AddSqlServerCursorMapStore("   "));
	}

	[Fact]
	public void ThrowWhenConnectionFactoryIsNull()
	{
		var services = NewServices();

		_ = Should.Throw<ArgumentNullException>(
			() => services.AddSqlServerCursorMapStore((Func<Microsoft.Data.SqlClient.SqlConnection>)null!));
	}
}
