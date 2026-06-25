// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Microsoft.Data.SqlClient;

namespace Excalibur.LeaderElection.Tests.SqlServer;

/// <summary>
/// Author≠impl regression lock for the <strong>SqlServer</strong> half of <c>zg4zga</c> (Sprint-846 Lane C,
/// AC-C4 — split-brain ownership-vs-liveness).
///
/// <para>
/// This lock is <strong>structural</strong>, not behavioral, after an empirical finding: a
/// <c>KILL &lt;spid&gt;</c> "alive-but-not-owning" container test is <em>vacuous</em> — a hard server-side
/// KILL breaks the connection, so the pre-fix <c>SELECT 1</c> liveness check <em>also</em> throws/returns
/// false and the pre-fix provider <em>also</em> relinquishes leadership (the behavioral test passes on both
/// pre- and post-fix). SqlClient idle-connection-resiliency only transparently reconnects a transient TCP
/// drop, not an explicit KILL, so the "false-positive leader on a reconnected session" state is not
/// container-reproducible deterministically. The genuine, non-vacuous guarantee is therefore structural —
/// identical in spirit to the Postgres guard (<c>PostgresLeaderElectionConnectionHardeningShould</c>):
/// the dedicated lock connection disables connection-resiliency and pooling, so a lost session-scoped
/// <c>sp_getapplock</c> surfaces as a broken connection rather than a live session that no longer owns the
/// lock. (The reentrancy AC-C2 behavioral lock for SqlServer remains in the integration suite.)
/// </para>
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
[Trait("Feature", "LeaderElection")]
public sealed class SqlServerLeaderElectionConnectionHardeningShould
{
	private static SqlConnectionStringBuilder BuildLockConnectionString(string raw)
	{
		// BuildLockConnectionString is the private static seam carrying the zg4zga connection hardening.
		// Non-vacuity: on pre-zg4zga committed mainline this method does not exist (added by the fix) → the
		// ShouldNotBeNull below fails (RED); it also goes RED on a mutant that drops the hardening.
		var method = typeof(SqlServerLeaderElection).GetMethod(
			"BuildLockConnectionString",
			BindingFlags.NonPublic | BindingFlags.Static);
		method.ShouldNotBeNull(
			"BuildLockConnectionString must exist — it carries the zg4zga connection hardening");

		var hardened = (string)method!.Invoke(obj: null, parameters: [raw])!;
		return new SqlConnectionStringBuilder(hardened);
	}

	[Fact]
	public void DisableConnectionResiliency_OnTheDedicatedLockConnection()
	{
		// Arrange — a raw connection string with resiliency unspecified (SqlClient defaults
		// ConnectRetryCount=1, which transparently reconnects to a NEW session that does NOT hold the
		// session-scoped applock → the split-brain false-positive).
		const string raw = "Server=localhost;Database=le;User Id=sa;Password=p;TrustServerCertificate=true";

		// Act
		var hardened = BuildLockConnectionString(raw);

		// Assert — no transparent reconnect: a dropped session must surface as a broken connection.
		hardened.ConnectRetryCount.ShouldBe(0);
	}

	[Fact]
	public void DisablePooling_OnTheDedicatedLockConnection()
	{
		// Arrange
		const string raw = "Server=localhost;Database=le;User Id=sa;Password=p;TrustServerCertificate=true";

		// Act
		var hardened = BuildLockConnectionString(raw);

		// Assert — the lock connection is dedicated (never reset/reused by the pool onto a different
		// session that does not hold the applock).
		hardened.Pooling.ShouldBeFalse();
	}

	[Fact]
	public void ForceHardeningOff_EvenWhenCallerEnablesResiliencyAndPooling()
	{
		// Arrange — even when the caller explicitly enables resiliency + pooling, the structural guarantee
		// must not be defeasible by consumer configuration.
		const string raw =
			"Server=localhost;Database=le;User Id=sa;Password=p;TrustServerCertificate=true;Pooling=true;ConnectRetryCount=3";

		// Act
		var hardened = BuildLockConnectionString(raw);

		// Assert — RED on a mutant that fails to override these (would stay 3 / true).
		hardened.ConnectRetryCount.ShouldBe(0);
		hardened.Pooling.ShouldBeFalse();
	}
}
