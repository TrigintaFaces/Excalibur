// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.LeaderElection;

namespace Excalibur.Dispatch.LeaderElection.Abstractions.Tests;

/// <summary>
/// Unit tests for <see cref="LeaderChangedEventArgs"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class LeaderChangedEventArgsShould : UnitTestBase
{
	[Fact]
	public void Constructor_SetsProperties()
	{
		// Act
		var args = new LeaderChangedEventArgs("old-leader", "new-leader", "lock-resource");

		// Assert
		args.PreviousLeaderId.ShouldBe("old-leader");
		args.NewLeaderId.ShouldBe("new-leader");
		args.ResourceName.ShouldBe("lock-resource");
	}

	[Fact]
	public void Constructor_WithNullLeaderIds_SetsNull()
	{
		// Act
		var args = new LeaderChangedEventArgs(null, null, "resource");

		// Assert
		args.PreviousLeaderId.ShouldBeNull();
		args.NewLeaderId.ShouldBeNull();
	}

	[Fact]
	public void Timestamp_IsRecentUtc()
	{
		// Arrange
		var before = DateTimeOffset.UtcNow;

		// Act
		var args = new LeaderChangedEventArgs("a", "b", "r");

		// Assert
		args.Timestamp.ShouldBeGreaterThanOrEqualTo(before);
		var assertionUpperBound1 = DateTimeOffset.UtcNow;
		args.Timestamp.ShouldBeLessThanOrEqualTo(assertionUpperBound1);
	}

	[Fact]
	public void InheritsFromEventArgs()
	{
		// Act
		var args = new LeaderChangedEventArgs("a", "b", "r");

		// Assert
		args.ShouldBeAssignableTo<EventArgs>();
	}
}
