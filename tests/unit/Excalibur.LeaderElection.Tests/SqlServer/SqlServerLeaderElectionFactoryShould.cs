// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.LeaderElection.Tests.SqlServer;

/// <summary>
/// Unit tests for <see cref="SqlServerLeaderElectionFactory" />.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SqlServerLeaderElectionFactoryShould : UnitTestBase
{
	private const string TestConnectionString = "Server=localhost;Database=test;Integrated Security=true;";

	[Fact]
	public void Constructor_ThrowsOnNullConnectionString()
	{
		// Arrange
		var loggerFactory = new LoggerFactory();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new SqlServerLeaderElectionFactory(null!, loggerFactory));
	}

	[Fact]
	public void Constructor_ThrowsOnNullLoggerFactory()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new SqlServerLeaderElectionFactory(TestConnectionString, null!));
	}

	[Fact]
	public void CreateElection_ReturnsSqlServerLeaderElection()
	{
		// Arrange
		var loggerFactory = new LoggerFactory();
		var factory = new SqlServerLeaderElectionFactory(TestConnectionString, loggerFactory);

		// Act
		var election = factory.CreateElection("TestApp.Leader", candidateId: null);

		// Assert
		_ = election.ShouldBeOfType<SqlServerLeaderElection>();
	}

	[Fact]
	public void CreateElection_UsesCandidateIdOverride()
	{
		// Arrange
		var loggerFactory = new LoggerFactory();
		var factory = new SqlServerLeaderElectionFactory(TestConnectionString, loggerFactory);

		// Act
		var election = factory.CreateElection("TestApp.Leader", candidateId: "custom-candidate");

		// Assert
		election.CandidateId.ShouldBe("custom-candidate");
	}

	[Fact]
	public void CreateElection_GeneratesCandidateId_WhenNotProvided()
	{
		// Arrange
		var loggerFactory = new LoggerFactory();
		var factory = new SqlServerLeaderElectionFactory(TestConnectionString, loggerFactory);

		// Act
		var election = factory.CreateElection("TestApp.Leader", candidateId: null);

		// Assert
		election.CandidateId.ShouldNotBeNullOrWhiteSpace();
		election.CandidateId.Length.ShouldBeLessThanOrEqualTo(24);
		var machinePrefix = Environment.MachineName[..Math.Min(Environment.MachineName.Length, election.CandidateId.Length)];
		election.CandidateId.StartsWith(machinePrefix, StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
	}

	[Fact]
	public void CreateElection_ThrowsOnNullResourceName()
	{
		// Arrange
		var loggerFactory = new LoggerFactory();
		var factory = new SqlServerLeaderElectionFactory(TestConnectionString, loggerFactory);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			factory.CreateElection(null!, candidateId: null));
	}

	[Fact]
	public void CreateElection_ThrowsOnEmptyResourceName()
	{
		// Arrange
		var loggerFactory = new LoggerFactory();
		var factory = new SqlServerLeaderElectionFactory(TestConnectionString, loggerFactory);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			factory.CreateElection("", candidateId: null));
	}

	[Fact]
	public void CreateHealthBasedElection_ReturnsSqlServerHealthBasedLeaderElection()
	{
		// Arrange
		var loggerFactory = new LoggerFactory();
		var factory = new SqlServerLeaderElectionFactory(TestConnectionString, loggerFactory);

		// Act
		var election = factory.CreateHealthBasedElection("TestApp.Leader", candidateId: null);

		// Assert
		_ = election.ShouldBeOfType<SqlServerHealthBasedLeaderElection>();
	}

	[Fact]
	public void CreateElection_CreatesSeparateInstances()
	{
		// Arrange
		var loggerFactory = new LoggerFactory();
		var factory = new SqlServerLeaderElectionFactory(TestConnectionString, loggerFactory);

		// Act
		var election1 = factory.CreateElection("TestApp.Leader1", candidateId: null);
		var election2 = factory.CreateElection("TestApp.Leader2", candidateId: null);

		// Assert
		election1.ShouldNotBeSameAs(election2);
	}
}
