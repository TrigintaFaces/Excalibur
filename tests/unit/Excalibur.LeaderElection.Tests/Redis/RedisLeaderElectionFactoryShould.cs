// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.LeaderElection.Tests.Redis;

/// <summary>
/// Unit tests for <see cref="RedisLeaderElectionFactory" />.
/// </summary>
[Trait("Category", "Unit")]
public sealed class RedisLeaderElectionFactoryShould : UnitTestBase
{
	[Fact]
	public void Constructor_ThrowsOnNullRedis()
	{
		// Arrange
		var loggerFactory = new LoggerFactory();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new RedisLeaderElectionFactory(null!, loggerFactory));
	}

	[Fact]
	public void Constructor_ThrowsOnNullLoggerFactory()
	{
		// Arrange
		var redis = A.Fake<IConnectionMultiplexer>();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new RedisLeaderElectionFactory(redis, null!));
	}

	[Fact]
	public void CreateElection_ReturnsRedisLeaderElection()
	{
		// Arrange
		var redis = A.Fake<IConnectionMultiplexer>();
		var loggerFactory = new LoggerFactory();
		var factory = new RedisLeaderElectionFactory(redis, loggerFactory);

		// Act
		var election = factory.CreateElection("test:leader", candidateId: null);

		// Assert
		_ = election.ShouldBeOfType<RedisLeaderElection>();
	}

	[Fact]
	public void CreateElection_UsesCandidateIdOverride()
	{
		// Arrange
		var redis = A.Fake<IConnectionMultiplexer>();
		var loggerFactory = new LoggerFactory();
		var factory = new RedisLeaderElectionFactory(redis, loggerFactory);

		// Act
		var election = factory.CreateElection("test:leader", candidateId: "custom-candidate");

		// Assert
		election.CandidateId.ShouldBe("custom-candidate");
	}

	[Fact]
	public void CreateElection_GeneratesCandidateId_WhenNotProvided()
	{
		// Arrange
		var redis = A.Fake<IConnectionMultiplexer>();
		var loggerFactory = new LoggerFactory();
		var factory = new RedisLeaderElectionFactory(redis, loggerFactory);

		// Act
		var election = factory.CreateElection("test:leader", candidateId: null);

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
		var redis = A.Fake<IConnectionMultiplexer>();
		var loggerFactory = new LoggerFactory();
		var factory = new RedisLeaderElectionFactory(redis, loggerFactory);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			factory.CreateElection(null!, candidateId: null));
	}

	[Fact]
	public void CreateElection_ThrowsOnEmptyResourceName()
	{
		// Arrange
		var redis = A.Fake<IConnectionMultiplexer>();
		var loggerFactory = new LoggerFactory();
		var factory = new RedisLeaderElectionFactory(redis, loggerFactory);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			factory.CreateElection("", candidateId: null));
	}

	[Fact]
	public void CreateHealthBasedElection_ThrowsNotSupportedException()
	{
		// Arrange
		var redis = A.Fake<IConnectionMultiplexer>();
		var loggerFactory = new LoggerFactory();
		var factory = new RedisLeaderElectionFactory(redis, loggerFactory);

		// Act & Assert
		_ = Should.Throw<NotSupportedException>(() =>
			factory.CreateHealthBasedElection("test:leader", candidateId: null));
	}

	[Fact]
	public void CreateElection_CreatesSeparateInstances()
	{
		// Arrange
		var redis = A.Fake<IConnectionMultiplexer>();
		var loggerFactory = new LoggerFactory();
		var factory = new RedisLeaderElectionFactory(redis, loggerFactory);

		// Act
		var election1 = factory.CreateElection("test:leader1", candidateId: null);
		var election2 = factory.CreateElection("test:leader2", candidateId: null);

		// Assert
		election1.ShouldNotBeSameAs(election2);
	}
}
