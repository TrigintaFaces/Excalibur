// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.LeaderElection.Tests.DependencyInjection;

/// <summary>
/// Fail-fast DX guard: when <c>UseRedis(...)</c> is called without any connection configured
/// (no connection string, no multiplexer instance/factory) and no <see cref="IConnectionMultiplexer"/>
/// is registered elsewhere, resolving the multiplexer must surface an actionable
/// <see cref="InvalidOperationException"/> naming the builder methods to call — not a raw
/// "no service for type IConnectionMultiplexer". A consumer-supplied multiplexer must be unaffected.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "LeaderElection")]
public sealed class RedisLeaderElectionMissingConnectionShould
{
	[Fact]
	public void Throw_ActionableMessage_WhenNoConnectionConfigured()
	{
		var services = new ServiceCollection();
		_ = services.AddExcalibur(x => x.AddLeaderElection(le =>
			le.UseRedis(_ => { /* deliberately no connection configured */ })));

		using var provider = services.BuildServiceProvider(validateScopes: false);

		var ex = Should.Throw<InvalidOperationException>(
			() => provider.GetRequiredService<IConnectionMultiplexer>());

		ex.Message.ShouldContain("Redis leader election requires a Redis connection");
		ex.Message.ShouldContain("ConnectionString");
		ex.Message.ShouldContain("UseRedis");
	}

	[Fact]
	public void UseConsumerRegisteredMultiplexer_WhenNoBuilderConnection()
	{
		// A consumer who registers their OWN IConnectionMultiplexer must be unaffected by the
		// fail-fast registration (TryAddSingleton does not override an existing registration; a
		// later plain registration wins on resolve).
		var ownMultiplexer = A.Fake<IConnectionMultiplexer>();

		var services = new ServiceCollection();
		_ = services.AddExcalibur(x => x.AddLeaderElection(le =>
			le.UseRedis(_ => { /* no builder connection — rely on consumer registration */ })));
		services.AddSingleton(ownMultiplexer);

		using var provider = services.BuildServiceProvider(validateScopes: false);

		var resolved = provider.GetRequiredService<IConnectionMultiplexer>();
		resolved.ShouldBeSameAs(ownMultiplexer);
	}
}
