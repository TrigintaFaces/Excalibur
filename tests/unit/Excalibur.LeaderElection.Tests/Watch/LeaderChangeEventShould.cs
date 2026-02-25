// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.LeaderElection.Watch;

namespace Excalibur.LeaderElection.Tests.Watch;

/// <summary>
/// Unit tests for <see cref="LeaderChangeEvent"/> and <see cref="LeaderWatchOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
[Trait("Feature", "LeaderElection")]
public sealed class LeaderChangeEventShould
{
	[Fact]
	public void StoreAllProperties()
	{
		// Arrange
		var changedAt = DateTimeOffset.UtcNow;

		// Act
		var evt = new LeaderChangeEvent("node-1", "node-2", changedAt, LeaderChangeReason.Elected);

		// Assert
		evt.PreviousLeader.ShouldBe("node-1");
		evt.NewLeader.ShouldBe("node-2");
		evt.ChangedAt.ShouldBe(changedAt);
		evt.Reason.ShouldBe(LeaderChangeReason.Elected);
	}

	[Fact]
	public void AllowNullLeaders()
	{
		// Act
		var evt = new LeaderChangeEvent(null, null, DateTimeOffset.UtcNow, LeaderChangeReason.Expired);

		// Assert
		evt.PreviousLeader.ShouldBeNull();
		evt.NewLeader.ShouldBeNull();
	}

	[Fact]
	public void SupportRecordEquality()
	{
		// Arrange
		var changedAt = DateTimeOffset.UtcNow;

		// Act
		var evt1 = new LeaderChangeEvent("a", "b", changedAt, LeaderChangeReason.Elected);
		var evt2 = new LeaderChangeEvent("a", "b", changedAt, LeaderChangeReason.Elected);

		// Assert
		evt1.ShouldBe(evt2);
	}

	[Fact]
	public void SupportRecordInequality()
	{
		// Act
		var evt1 = new LeaderChangeEvent("a", "b", DateTimeOffset.UtcNow, LeaderChangeReason.Elected);
		var evt2 = new LeaderChangeEvent("a", "c", DateTimeOffset.UtcNow, LeaderChangeReason.Elected);

		// Assert
		evt1.ShouldNotBe(evt2);
	}
}
