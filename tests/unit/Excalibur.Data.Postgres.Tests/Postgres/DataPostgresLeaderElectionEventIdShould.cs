// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Postgres.Diagnostics;

namespace Excalibur.Data.Tests.Postgres;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class DataPostgresLeaderElectionEventIdShould
{
	[Fact]
	public void HaveLeaderElectionIds()
	{
		DataPostgresEventId.LeaderElectionStarted.ShouldBe(107100);
		DataPostgresEventId.LeaderElectionStopped.ShouldBe(107101);
		DataPostgresEventId.LockAcquisitionFailed.ShouldBe(107102);
		DataPostgresEventId.LockAcquisitionError.ShouldBe(107103);
		DataPostgresEventId.LockReleased.ShouldBe(107104);
		DataPostgresEventId.LockReleaseError.ShouldBe(107105);
		DataPostgresEventId.LeaderElectionError.ShouldBe(107106);
		DataPostgresEventId.BecameLeader.ShouldBe(107107);
		DataPostgresEventId.LostLeadership.ShouldBe(107108);
		DataPostgresEventId.LeaderElectionDisposeError.ShouldBe(107109);
	}

	[Fact]
	public void HaveUniqueEventIds()
	{
		var ids = new[]
		{
			DataPostgresEventId.LeaderElectionStarted,
			DataPostgresEventId.LeaderElectionStopped,
			DataPostgresEventId.LockAcquisitionFailed,
			DataPostgresEventId.LockAcquisitionError,
			DataPostgresEventId.LockReleased,
			DataPostgresEventId.LockReleaseError,
			DataPostgresEventId.LeaderElectionError,
			DataPostgresEventId.BecameLeader,
			DataPostgresEventId.LostLeadership,
			DataPostgresEventId.LeaderElectionDisposeError,
		};

		ids.ShouldBeUnique();
	}

	[Fact]
	public void HaveLeaderElectionIdsInCorrectRange()
	{
		var leaderIds = new[]
		{
			DataPostgresEventId.LeaderElectionStarted,
			DataPostgresEventId.LeaderElectionStopped,
			DataPostgresEventId.LockAcquisitionFailed,
			DataPostgresEventId.LockAcquisitionError,
			DataPostgresEventId.LockReleased,
			DataPostgresEventId.LockReleaseError,
			DataPostgresEventId.LeaderElectionError,
			DataPostgresEventId.BecameLeader,
			DataPostgresEventId.LostLeadership,
			DataPostgresEventId.LeaderElectionDisposeError,
		};

		foreach (var id in leaderIds)
		{
			id.ShouldBeGreaterThanOrEqualTo(107100);
			id.ShouldBeLessThan(107200);
		}
	}
}
