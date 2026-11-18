// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;

using Excalibur.LeaderElection.Redis;

using Shouldly;

using Xunit;

namespace Excalibur.LeaderElection.Tests.Redis;

/// <summary>Verifies complete immutable tenure observations across concurrent publication and retirement.</summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class RedisLeaderElectionRenewalTicksShould
{
	[Fact]
	public Task ObserveOnlyCompleteFenceAndAcquisitionTimePairsAcrossRestart() =>
		RedisTenureProtocolShould.VerifyCoherentSnapshotsAcrossRestartAsync();

	[Fact]
	public void NotRetainTheTornMultiFieldDateTimeOffsetRepresentation()
	{
		var legacyField = typeof(RedisLeaderElection)
			.GetField("_lastSuccessfulRenewal", BindingFlags.NonPublic | BindingFlags.Instance);

		legacyField.ShouldBeNull(
			"the pre-fix DateTimeOffset _lastSuccessfulRenewal (the torn representation) must be removed in " +
			"favour of the immutable, monotonic tenure proof.");
	}
}
