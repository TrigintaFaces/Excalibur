// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.LeaderElection.Postgres;

using Npgsql;

namespace Excalibur.Data.Tests.Postgres;

/// <summary>
/// Author≠impl regression lock for the <strong>connection-hardening half (Layer 1)</strong> of the Postgres
/// <c>zg4zga</c> fix (Sprint-846 Lane C, AC-C4 — split-brain ownership-vs-liveness). It binds the structural
/// seam <c>BuildLockConnectionString</c> (<c>Pooling=false</c>): the dedicated lock connection is never
/// reset/reused onto a different backend, so a dropped session surfaces deterministically as
/// <c>State != Open</c> → leadership relinquished, rather than a live connection on a different backend that
/// no longer holds the advisory lock.
/// </summary>
/// <remarks>
/// Scope note (corrected per ProductManager 14961 / ProjectReviewer 14968): only the <em>connection-loss</em>
/// path is vacuous behaviorally — a <c>pg_terminate_backend</c> closes the connection, so both the pre-fix
/// <c>SELECT 1</c> and the post-fix probe fail (green on both). But the <em>lock-loss on a live session</em>
/// path IS container-reproducible (<c>pg_advisory_unlock</c> keeps the connection <c>Open</c> while removing
/// ownership) and is locked behaviorally in <c>PostgresLeaderElectionOwnershipIntegrationShould</c> (the
/// Layer-2 <c>pg_locks</c> ownership-probe lock). This structural lock (Layer 1) + that behavioral lock
/// (Layer 2) together fully guard AC-C4 on Postgres.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
[Trait("Feature", "LeaderElection")]
public sealed class PostgresLeaderElectionConnectionHardeningShould
{
	private static string BuildLockConnectionString(string raw)
	{
		// BuildLockConnectionString is the private static seam that carries the zg4zga connection hardening.
		// Non-vacuity: on pre-zg4zga committed mainline this method does not exist (added by the fix), so
		// GetMethod returns null and the ShouldNotBeNull below fails (RED); it also goes RED on the one-token
		// mutant that drops `Pooling = false`.
		var method = typeof(PostgresLeaderElection).GetMethod(
			"BuildLockConnectionString",
			BindingFlags.NonPublic | BindingFlags.Static);
		method.ShouldNotBeNull(
			"BuildLockConnectionString must exist — it carries the zg4zga Pooling=false connection hardening");

		return (string)method!.Invoke(obj: null, parameters: [raw])!;
	}

	[Fact]
	public void DisablePooling_OnTheDedicatedLockConnection()
	{
		// Arrange — a raw connection string with pooling unspecified (Npgsql defaults Pooling=true).
		const string raw = "Host=localhost;Database=leader_election;Username=u;Password=p";

		// Act
		var hardened = BuildLockConnectionString(raw);

		// Assert — the lock connection MUST disable pooling so a lost session surfaces as State!=Open.
		new NpgsqlConnectionStringBuilder(hardened).Pooling.ShouldBeFalse();
	}

	[Fact]
	public void ForcePoolingOff_EvenWhenCallerEnablesIt()
	{
		// Arrange — even when the caller explicitly enables pooling, the structural guarantee must not be
		// defeasible by consumer configuration (a pooled connection could reset/reuse onto a different live
		// backend that does not hold the advisory lock → split-brain false-positive).
		const string raw = "Host=localhost;Database=leader_election;Username=u;Password=p;Pooling=true";

		// Act
		var hardened = BuildLockConnectionString(raw);

		// Assert — RED on the one-token mutant (no Pooling override → stays true).
		new NpgsqlConnectionStringBuilder(hardened).Pooling.ShouldBeFalse();
	}
}
