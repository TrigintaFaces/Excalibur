// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Outbox.Redis;

using StackExchange.Redis;

namespace Excalibur.Outbox.Tests.Builders;

/// <summary>
/// The Redis outbox builder accepts <see cref="IConnectionMultiplexer"/>, so a host that supplies its own
/// implementation — a wrapper, a proxy, a test double — must be able to resolve the store. These arms probe a
/// real container rather than inspecting registration shape: the defect they lock was a concrete-type cast that
/// only surfaced at resolve time.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class RedisOutboxSubstitutableMultiplexerShould
{
	// Deliberately NOT a ConnectionMultiplexer: that concrete type is sealed-by-construction for tests
	// (it can only be produced by connecting to a server), which is exactly why a cast to it made the
	// connection-supplied branch unreachable for any host that does not hand us a live connection.
	private static IConnectionMultiplexer FakeMultiplexer(IDatabase database)
	{
		var multiplexer = A.Fake<IConnectionMultiplexer>();
		_ = A.CallTo(() => multiplexer.GetDatabase(A<int>._, A<object>._)).Returns(database);
		return multiplexer;
	}

	[Fact]
	public async Task ResolveStore_WhenHostSuppliesANonConnectionMultiplexerImplementation()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		var multiplexer = FakeMultiplexer(A.Fake<IDatabase>());

		services.AddExcaliburOutbox(outbox => outbox.UseRedis(redis =>
			redis.ConnectionMultiplexer(multiplexer).KeyPrefix("outbox-test")));

		await using var provider = services.BuildServiceProvider();

		// SAFETY ARM: the advertised interface must actually be accepted. Before the store took
		// IConnectionMultiplexer this threw InvalidCastException here, at first resolve.
		var store = Should.NotThrow(provider.GetRequiredService<RedisOutboxStore>);
		store.ShouldNotBeNull();
	}

	[Fact]
	public async Task UseTheSuppliedMultiplexer_RatherThanConnectingFromOptions()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		var database = A.Fake<IDatabase>();
		var multiplexer = FakeMultiplexer(database);

		services.AddExcaliburOutbox(outbox => outbox.UseRedis(redis =>
			redis.ConnectionMultiplexer(multiplexer).KeyPrefix("outbox-test").Database(3)));

		await using var provider = services.BuildServiceProvider();
		_ = provider.GetRequiredService<RedisOutboxStore>();

		// LIVENESS ARM: resolving without throwing is not enough — the store must bind to the multiplexer
		// the host supplied, on the configured database. A change that accepted the interface and then
		// ignored it (or lazily dialled the connection string instead) still passes the arm above.
		A.CallTo(() => multiplexer.GetDatabase(3, A<object>._)).MustHaveHappened();
	}

	[Fact]
	public async Task NotInventAPlaceholderEndpoint_WhenAMultiplexerIsSupplied()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		services.AddExcaliburOutbox(outbox => outbox.UseRedis(redis =>
			redis.ConnectionMultiplexer(FakeMultiplexer(A.Fake<IDatabase>())).KeyPrefix("outbox-test")));

		await using var provider = services.BuildServiceProvider();
		var connectionString = provider.GetRequiredService<IOptions<RedisOutboxOptions>>().Value.ConnectionString;

		// Supplying a multiplexer and supplying a connection string are mutually exclusive modes, so the
		// string is legitimately absent here. What must NOT happen is a fabricated endpoint written purely to
		// satisfy validation: a placeholder is indistinguishable from a real host to anyone reading the
		// options, and the store would dial it if it ever fell back to connecting for itself.
		connectionString.ShouldBeNull();
	}
}
