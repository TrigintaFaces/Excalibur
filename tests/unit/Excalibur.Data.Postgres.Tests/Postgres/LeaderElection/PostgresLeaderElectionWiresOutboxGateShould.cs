// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.LeaderElection;

namespace Excalibur.Data.Tests.Postgres.LeaderElection;

/// <summary>
/// Regression lock: a store-specific leader-election registration wires the outbox leader gate.
/// </summary>
/// <remarks>
/// <para>
/// THE BUG. Seven store-specific leader-election packages existed and NONE of them registered the outbox
/// leader gate - a repo-wide grep for the registration across all seven returned zero, while the same grep
/// over the sibling core package returned hits. So <c>AddPostgresLeaderElection(...)</c> made an
/// <c>ILeaderElection</c> resolvable but left <c>ILeaderProcessingGate</c> unregistered, and every instance
/// drained the outbox concurrently on a deployment whose operator had registered an election precisely to
/// stop that.
/// </para>
/// <para>
/// This arm binds the WIRING half of the fix (the registration now happens). Its sibling
/// <c>OutboxRefusesUnfencedLeaderElectionShould</c> binds the BACKSTOP half (a host that still reaches an
/// unfenced state refuses to start), so a future registration path that forgets the gate cannot regress into
/// a silent unfenced drain.
/// </para>
/// <para>
/// The assertion is on the RESOLVED gate, not on a descriptor count: a registration that cannot actually be
/// satisfied from the container is not a wiring. The gate resolves the election it fences on, so resolving
/// the gate also proves the election registration and the gate registration are mutually consistent - the
/// failure mode where a gate is registered against an election that is only keyed, and throws on first use.
/// </para>
/// <para>
/// NON-VACUITY: RED against committed HEAD before the fix - <c>AddPostgresLeaderElection</c> registered no
/// gate at all, so <c>GetService&lt;ILeaderProcessingGate&gt;()</c> returned null.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "LeaderElection")]
public sealed class PostgresLeaderElectionWiresOutboxGateShould
{
	// Syntactically valid and never connected to: the arms assert registration and resolution, and the
	// election opens its connection when it campaigns, not when it is constructed.
	private const string ConnectionString = "Host=localhost;Database=excalibur_lock_test;Username=u;Password=p";

	[Fact]
	public async Task RegisterTheLeaderProcessingGate_WhenPostgresLeaderElectionIsAdded()
	{
		// SAFETY - THE BUG ARM. Registering the election through the store-specific extension must also wire
		// the gate, or the outbox drains unfenced on every instance.
		await using var provider = BuildProvider(addLeaderElection: true);

		var gate = provider.GetService<ILeaderProcessingGate>();

		gate.ShouldNotBeNull(
			"AddPostgresLeaderElection registers an ILeaderElection, which is the multi-instance signal. If no " +
			"ILeaderProcessingGate resolves, nothing fences the outbox drain and every instance claims " +
			"concurrently - the split-brain the operator added leader election to prevent.");

		provider.GetService<IProcessingGate>().ShouldNotBeNull(
			"The gate must also satisfy the general IProcessingGate contract the drain consumes, not only the " +
			"leader-specific one.");
	}

	[Fact]
	public async Task ResolveAGateBackedByTheRegisteredElection_WhenPostgresLeaderElectionIsAdded()
	{
		// The gate resolves the election it fences on. If the election were registered only under a key, this
		// resolution would throw - so this arm proves the two registrations are mutually consistent, and that
		// the wiring is real rather than a descriptor that cannot be satisfied.
		await using var provider = BuildProvider(addLeaderElection: true);

		_ = Should.NotThrow(
			() => provider.GetRequiredService<ILeaderProcessingGate>(),
			"The registered gate must actually be constructible from this container. A gate registered against " +
			"an election that is only keyed would resolve to nothing and throw on first use, which is the " +
			"unfenced drain wearing a passing registration check.");

		_ = Should.NotThrow(
			() => provider.GetRequiredService<ILeaderElection>(),
			"The gate consumes a NON-KEYED ILeaderElection. If only a keyed registration exists, the gate is " +
			"unsatisfiable and the wiring is cosmetic.");
	}

	[Fact]
	public async Task NotRegisterAnyGate_WhenNoLeaderElectionIsAdded()
	{
		// LIVENESS. The gate must arrive with the election and not otherwise. A registration that appears
		// unconditionally would fence single-node hosts that never asked for coordination, and would also make
		// the safety arm above pass for the wrong reason.
		await using var provider = BuildProvider(addLeaderElection: false);

		provider.GetService<ILeaderProcessingGate>().ShouldBeNull(
			"With no leader election registered there is nothing to fence on. A gate appearing here would mean " +
			"the safety arm proves nothing, and that single-node hosts are gated on an election they never " +
			"configured.");
	}

	// Disposal is asynchronous, and must stay that way. Resolving the gate resolves the election behind
	// it, and the registered election is a TelemetryLeaderElection, which implements IAsyncDisposable and
	// NOT IDisposable. Microsoft.Extensions.DependencyInjection throws on a synchronous Dispose of a
	// container holding such a singleton, so a `using var` here fails at teardown after the assertions
	// have already passed. A real host is unaffected - IHost.Dispose routes to asynchronous disposal.
	private static ServiceProvider BuildProvider(bool addLeaderElection)
	{
		var services = new ServiceCollection();

		// The real host supplies logging. Resolving the gate resolves the election behind it, and
		// PostgresLeaderElection takes ILogger<PostgresLeaderElection> as a required parameter.
		_ = services.AddLogging();

		if (addLeaderElection)
		{
			// The exact public entry point a consumer calls - not the core AddLeaderElection builder path,
			// which was already wired. This store-specific overload is the one that was not.
			_ = services.AddPostgresLeaderElection(o =>
			{
				o.ConnectionString = ConnectionString;
				o.LockKey = 4242;
			});
		}

		return services.BuildServiceProvider(validateScopes: false);
	}
}
