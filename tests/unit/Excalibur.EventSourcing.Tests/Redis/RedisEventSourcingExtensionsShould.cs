// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Redis;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using StackExchange.Redis;

namespace Excalibur.EventSourcing.Tests.Redis;

[Trait("Category", "Unit")]
public sealed class RedisEventSourcingExtensionsShould : UnitTestBase
{
	[Fact]
	public void AddRedisEventStore_WithConfigure_ValidatesArgumentsAndRegistersServices()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(() => RedisEventSourcingExtensions.AddRedisEventStore(null!, _ => { }));
		Should.Throw<ArgumentNullException>(() => services.AddRedisEventStore((Action<RedisEventStoreOptions>)null!));

		var result = services.AddRedisEventStore(options => options.ConnectionString = "localhost:6379");
		result.ShouldBeSameAs(services);

		services.ShouldContain(sd => sd.ServiceType == typeof(ConnectionMultiplexer));
		services.ShouldContain(sd => sd.ServiceType == typeof(RedisEventStore));
		services.ShouldContain(sd => sd.ServiceType == typeof(IEventStore));
	}

	[Fact]
	public void AddRedisEventStore_WithConnectionString_ValidatesArguments()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(() => RedisEventSourcingExtensions.AddRedisEventStore(null!, "localhost:6379"));
		Should.Throw<ArgumentException>(() => services.AddRedisEventStore((string)null!));
		Should.Throw<ArgumentException>(() => services.AddRedisEventStore(" "));
	}

	[Fact]
	public void AddRedisEventStore_WithConnectionInstance_ValidatesArguments()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(() =>
			RedisEventSourcingExtensions.AddRedisEventStore(null!, connection: null!, configure: null));

		Should.Throw<ArgumentNullException>(() =>
			services.AddRedisEventStore(connection: null!, configure: null));
	}

	[Fact]
	public void AddRedisEventStore_WithConnectionInstance_RegistersServices_WithAndWithoutConfigure()
	{
		var connection = (ConnectionMultiplexer)System.Runtime.CompilerServices.RuntimeHelpers
			.GetUninitializedObject(typeof(ConnectionMultiplexer));

		var servicesWithConfigure = new ServiceCollection();
		_ = servicesWithConfigure.AddRedisEventStore(connection, options => options.ConnectionString = "localhost:6379");
		servicesWithConfigure.ShouldContain(sd => sd.ServiceType == typeof(ConnectionMultiplexer));
		servicesWithConfigure.ShouldContain(sd => sd.ServiceType == typeof(RedisEventStore));
		servicesWithConfigure.ShouldContain(sd => sd.ServiceType == typeof(IEventStore));

		using (var provider = servicesWithConfigure.BuildServiceProvider())
		{
			var options = provider.GetRequiredService<IOptions<RedisEventStoreOptions>>().Value;
			options.ConnectionString.ShouldBe("localhost:6379");
		}

		var servicesWithoutConfigure = new ServiceCollection();
		_ = servicesWithoutConfigure.AddRedisEventStore(connection, configure: null);
		servicesWithoutConfigure.ShouldContain(sd => sd.ServiceType == typeof(ConnectionMultiplexer));
		servicesWithoutConfigure.ShouldContain(sd => sd.ServiceType == typeof(RedisEventStore));
		servicesWithoutConfigure.ShouldContain(sd => sd.ServiceType == typeof(IEventStore));
	}

	[Fact]
	public void AddRedisSnapshotStore_ValidatesArgumentsAndRegistersServices()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(() => RedisEventSourcingExtensions.AddRedisSnapshotStore(null!, _ => { }));
		Should.Throw<ArgumentNullException>(() => services.AddRedisSnapshotStore((Action<RedisSnapshotStoreOptions>)null!));
		Should.Throw<ArgumentException>(() => services.AddRedisSnapshotStore(" "));

		var result = services.AddRedisSnapshotStore(options => options.ConnectionString = "localhost:6379");
		result.ShouldBeSameAs(services);

		services.ShouldContain(sd => sd.ServiceType == typeof(ConnectionMultiplexer));
		services.ShouldContain(sd => sd.ServiceType == typeof(RedisSnapshotStore));
		services.ShouldContain(sd => sd.ServiceType == typeof(ISnapshotStore));
	}

	[Fact]
	public void AddRedisEventSourcing_ValidatesArgumentsAndChainsRegistrations()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(() => RedisEventSourcingExtensions.AddRedisEventSourcing(null!, "localhost:6379"));
		Should.Throw<ArgumentException>(() => services.AddRedisEventSourcing(" "));
		Should.Throw<ArgumentNullException>(() =>
			services.AddRedisEventSourcing(null!, _ => { }));
		Should.Throw<ArgumentNullException>(() =>
			services.AddRedisEventSourcing(_ => { }, null!));

		var result = services.AddRedisEventSourcing("localhost:6379");
		result.ShouldBeSameAs(services);
		services.ShouldContain(sd => sd.ServiceType == typeof(IEventStore));
		services.ShouldContain(sd => sd.ServiceType == typeof(ISnapshotStore));
	}
}
