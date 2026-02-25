// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.LeaderElection;

namespace Excalibur.Dispatch.LeaderElection.Abstractions.Tests;

/// <summary>
/// Unit tests for <see cref="LeaderElectionEventArgs"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class LeaderElectionEventArgsShould : UnitTestBase
{
	[Fact]
	public void Constructor_SetsProperties()
	{
		// Act
		var args = new LeaderElectionEventArgs("candidate-1", "resource-lock");

		// Assert
		args.CandidateId.ShouldBe("candidate-1");
		args.ResourceName.ShouldBe("resource-lock");
	}

	[Fact]
	public void Timestamp_IsRecentUtc()
	{
		// Arrange
		var before = DateTimeOffset.UtcNow;

		// Act
		var args = new LeaderElectionEventArgs("c1", "r1");

		// Assert
		args.Timestamp.ShouldBeGreaterThanOrEqualTo(before);
		args.Timestamp.ShouldBeLessThanOrEqualTo(DateTimeOffset.UtcNow);
	}

	[Fact]
	public void InheritsFromEventArgs()
	{
		// Act
		var args = new LeaderElectionEventArgs("c1", "r1");

		// Assert
		args.ShouldBeAssignableTo<EventArgs>();
	}
}
