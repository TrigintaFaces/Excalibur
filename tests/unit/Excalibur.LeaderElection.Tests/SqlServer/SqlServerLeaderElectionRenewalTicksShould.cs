// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.LeaderElection.SqlServer;

using Shouldly;

using Xunit;

namespace Excalibur.LeaderElection.Tests.SqlServer;

/// <summary>
/// Author≠impl regression lock for S850 Lane E · <c>a58yu6</c> (torn cross-thread read of the
/// last-successful-renewal timestamp on <see cref="SqlServerLeaderElection"/>).
/// </summary>
/// <remarks>
/// Authored by FrontendDeveloper (did NOT implement the fix — independence per
/// <c>issue-remediation-protocol</c>) against the frozen GUIDE seam (msg 15508, decision #4): the renewal
/// loop reads the timestamp lock-free while <c>BecomeLeader</c> writes it under <c>_lock</c>, so a multi-field
/// <see cref="DateTimeOffset"/> would tear. The fix stores it as a single 64-bit <see cref="long"/> of UTC
/// ticks accessed via <see cref="Interlocked"/>. Structural conformance lock (deterministic), mirroring the
/// sibling <c>SqlServerLeaderElectionVolatileShould</c> — a behavioral torn-read test cannot be made
/// deterministic. <b>RED on the pre-fix surface</b> (the <c>long</c> ticks field is absent; the
/// <see cref="DateTimeOffset"/> field is present); GREEN on the fix.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SqlServerLeaderElectionRenewalTicksShould
{
	[Fact]
	public void StoreLastSuccessfulRenewalAsAnInterlockedLongTicksField()
	{
		var ticksField = typeof(SqlServerLeaderElection)
			.GetField("_lastSuccessfulRenewalTicks", BindingFlags.NonPublic | BindingFlags.Instance);

		ticksField.ShouldNotBeNull(
			"a58yu6: the last-successful-renewal timestamp must be a single 64-bit field accessed via " +
			"Interlocked.Read/Exchange — a multi-field DateTimeOffset tears when the renewal loop reads it " +
			"lock-free while BecomeLeader writes it under _lock.");
		ticksField.FieldType.ShouldBe(typeof(long));
	}

	[Fact]
	public void NotRetainTheTornMultiFieldDateTimeOffsetRepresentation()
	{
		var legacyField = typeof(SqlServerLeaderElection)
			.GetField("_lastSuccessfulRenewal", BindingFlags.NonPublic | BindingFlags.Instance);

		legacyField.ShouldBeNull(
			"the pre-fix DateTimeOffset _lastSuccessfulRenewal (the torn representation) must be removed in " +
			"favour of the Interlocked long ticks field.");
	}
}
