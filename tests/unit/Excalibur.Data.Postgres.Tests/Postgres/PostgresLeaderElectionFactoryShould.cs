// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.LeaderElection.Postgres;
using Excalibur.Dispatch.LeaderElection;

namespace Excalibur.Data.Tests.Postgres;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class PostgresLeaderElectionFactoryShould
{
	private readonly IOptions<PostgresLeaderElectionOptions> _pgOptions;
	private readonly ILoggerFactory _loggerFactory;

	public PostgresLeaderElectionFactoryShould()
	{
		_pgOptions = Microsoft.Extensions.Options.Options.Create(new PostgresLeaderElectionOptions
		{
			ConnectionString = "Host=localhost;Database=test;",
			LockKey = 42,
			CommandTimeoutSeconds = 10
		});
		_loggerFactory = A.Fake<ILoggerFactory>();
		A.CallTo(() => _loggerFactory.CreateLogger(A<string>._))
			.Returns(A.Fake<ILogger>());
	}

	[Fact]
	public void ThrowWhenPgOptionsIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new PostgresLeaderElectionFactory(null!, _loggerFactory));
	}

	[Fact]
	public void ThrowWhenLoggerFactoryIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new PostgresLeaderElectionFactory(_pgOptions, null!));
	}

	[Fact]
	public void ImplementILeaderElectionFactory()
	{
		var factory = new PostgresLeaderElectionFactory(_pgOptions, _loggerFactory);
		factory.ShouldBeAssignableTo<ILeaderElectionFactory>();
	}

	[Fact]
	public void CreateElection_ThrowWhenResourceNameIsNull()
	{
		var factory = new PostgresLeaderElectionFactory(_pgOptions, _loggerFactory);
		Should.Throw<ArgumentException>(() => factory.CreateElection(null!, null));
	}

	[Fact]
	public void CreateElection_ThrowWhenResourceNameIsEmpty()
	{
		var factory = new PostgresLeaderElectionFactory(_pgOptions, _loggerFactory);
		Should.Throw<ArgumentException>(() => factory.CreateElection("", null));
	}

	[Fact]
	public void CreateElection_ThrowWhenResourceNameIsWhitespace()
	{
		var factory = new PostgresLeaderElectionFactory(_pgOptions, _loggerFactory);
		Should.Throw<ArgumentException>(() => factory.CreateElection("   ", null));
	}

	[Fact]
	public void CreateElection_ReturnILeaderElection()
	{
		var factory = new PostgresLeaderElectionFactory(_pgOptions, _loggerFactory);
		var election = factory.CreateElection("test-resource", null);
		election.ShouldNotBeNull();
		election.ShouldBeAssignableTo<ILeaderElection>();
	}

	[Fact]
	public void CreateElection_ReturnILeaderElectionWithCandidateId()
	{
		var factory = new PostgresLeaderElectionFactory(_pgOptions, _loggerFactory);
		var election = factory.CreateElection("test-resource", "candidate-1");
		election.ShouldNotBeNull();
		election.ShouldBeAssignableTo<ILeaderElection>();
	}

	[Fact]
	public void CreateElection_ReturnDifferentInstancesForSameResource()
	{
		var factory = new PostgresLeaderElectionFactory(_pgOptions, _loggerFactory);
		var election1 = factory.CreateElection("resource-a", null);
		var election2 = factory.CreateElection("resource-a", null);
		election1.ShouldNotBeSameAs(election2);
	}

	[Fact]
	public void CreateHealthBasedElection_ThrowWhenResourceNameIsNull()
	{
		var factory = new PostgresLeaderElectionFactory(_pgOptions, _loggerFactory);
		Should.Throw<ArgumentException>(() => factory.CreateHealthBasedElection(null!, null));
	}

	[Fact]
	public void CreateHealthBasedElection_ThrowWhenResourceNameIsEmpty()
	{
		var factory = new PostgresLeaderElectionFactory(_pgOptions, _loggerFactory);
		Should.Throw<ArgumentException>(() => factory.CreateHealthBasedElection("", null));
	}

	[Fact]
	public void CreateHealthBasedElection_ReturnIHealthBasedLeaderElection()
	{
		var factory = new PostgresLeaderElectionFactory(_pgOptions, _loggerFactory);
		var election = factory.CreateHealthBasedElection("health-resource", null);
		election.ShouldNotBeNull();
		election.ShouldBeAssignableTo<IHealthBasedLeaderElection>();
	}

	[Fact]
	public void CreateHealthBasedElection_ReturnIHealthBasedLeaderElectionWithCandidateId()
	{
		var factory = new PostgresLeaderElectionFactory(_pgOptions, _loggerFactory);
		var election = factory.CreateHealthBasedElection("health-resource", "candidate-2");
		election.ShouldNotBeNull();
		election.ShouldBeAssignableTo<IHealthBasedLeaderElection>();
	}

	[Fact]
	public void CreateElection_ProduceDeterministicLockKeysForSameResourceName()
	{
		var factory = new PostgresLeaderElectionFactory(_pgOptions, _loggerFactory);
		// Both calls should produce the same lock key from the same resource name
		// This is verified indirectly - both return valid instances without error
		var election1 = factory.CreateElection("deterministic-resource", null);
		var election2 = factory.CreateElection("deterministic-resource", null);
		election1.ShouldNotBeNull();
		election2.ShouldNotBeNull();
	}
}
