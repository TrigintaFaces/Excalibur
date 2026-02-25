// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.LeaderElection;

namespace Excalibur.Dispatch.LeaderElection.Abstractions.Tests;

/// <summary>
/// Contract tests for <see cref="ILeaderElectionFactory"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ILeaderElectionFactoryContractShould : UnitTestBase
{
	[Fact]
	public void CreateElection_ShouldReturnILeaderElection()
	{
		// Arrange
		var factory = A.Fake<ILeaderElectionFactory>();
		var expected = A.Fake<ILeaderElection>();
		A.CallTo(() => factory.CreateElection("resource-1", "candidate-1"))
			.Returns(expected);

		// Act
		var result = factory.CreateElection("resource-1", "candidate-1");

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void CreateElection_WithNullCandidateId_ShouldStillWork()
	{
		// Arrange
		var factory = A.Fake<ILeaderElectionFactory>();
		var expected = A.Fake<ILeaderElection>();
		A.CallTo(() => factory.CreateElection("resource-1", null))
			.Returns(expected);

		// Act
		var result = factory.CreateElection("resource-1", null);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void CreateHealthBasedElection_ShouldReturnIHealthBasedLeaderElection()
	{
		// Arrange
		var factory = A.Fake<ILeaderElectionFactory>();
		var expected = A.Fake<IHealthBasedLeaderElection>();
		A.CallTo(() => factory.CreateHealthBasedElection("resource-1", "candidate-1"))
			.Returns(expected);

		// Act
		var result = factory.CreateHealthBasedElection("resource-1", "candidate-1");

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void CreateHealthBasedElection_WithNullCandidateId_ShouldStillWork()
	{
		// Arrange
		var factory = A.Fake<ILeaderElectionFactory>();
		var expected = A.Fake<IHealthBasedLeaderElection>();
		A.CallTo(() => factory.CreateHealthBasedElection("resource-1", null))
			.Returns(expected);

		// Act
		var result = factory.CreateHealthBasedElection("resource-1", null);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void CreateElection_DifferentResources_ShouldReturnDifferentInstances()
	{
		// Arrange
		var factory = A.Fake<ILeaderElectionFactory>();
		var election1 = A.Fake<ILeaderElection>();
		var election2 = A.Fake<ILeaderElection>();
		A.CallTo(() => factory.CreateElection("resource-1", A<string?>._)).Returns(election1);
		A.CallTo(() => factory.CreateElection("resource-2", A<string?>._)).Returns(election2);

		// Act
		var result1 = factory.CreateElection("resource-1", null);
		var result2 = factory.CreateElection("resource-2", null);

		// Assert
		result1.ShouldNotBe(result2);
	}
}
