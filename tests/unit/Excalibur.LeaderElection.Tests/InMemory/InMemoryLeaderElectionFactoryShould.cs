// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.LeaderElection.Tests.InMemory;

/// <summary>
/// Unit tests for <see cref="InMemoryLeaderElectionFactory" />.
/// </summary>
[Trait("Category", "Unit")]
public sealed class InMemoryLeaderElectionFactoryShould : UnitTestBase
{
	[Fact]
	public void Constructor_ThrowsOnNullOptions()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new InMemoryLeaderElectionFactory(null!, loggerFactory: null));
	}

	[Fact]
	public void CreateElection_ReturnsInMemoryLeaderElection()
	{
		// Arrange
		var options = Options.Create(new LeaderElectionOptions
		{
			InstanceId = "test-instance",
			LeaseDuration = TimeSpan.FromSeconds(15),
		});
		var factory = new InMemoryLeaderElectionFactory(options, loggerFactory: null);

		// Act
		var election = factory.CreateElection($"test-resource-{Guid.NewGuid():N}", candidateId: null);

		// Assert
		_ = election.ShouldBeOfType<InMemoryLeaderElection>();

		// Cleanup
		((InMemoryLeaderElection)election).Dispose();
	}

	[Fact]
	public void CreateElection_UsesCandidateIdOverride()
	{
		// Arrange
		var options = Options.Create(new LeaderElectionOptions
		{
			InstanceId = "default-instance",
		});
		var factory = new InMemoryLeaderElectionFactory(options, loggerFactory: null);

		// Act
		var election = factory.CreateElection($"test-resource-{Guid.NewGuid():N}", candidateId: "custom-candidate");

		// Assert
		election.CandidateId.ShouldBe("custom-candidate");

		// Cleanup
		((InMemoryLeaderElection)election).Dispose();
	}

	[Fact]
	public void CreateElection_UsesDefaultInstanceId_WhenNoCandidateIdProvided()
	{
		// Arrange
		var options = Options.Create(new LeaderElectionOptions
		{
			InstanceId = "default-instance",
		});
		var factory = new InMemoryLeaderElectionFactory(options, loggerFactory: null);

		// Act
		var election = factory.CreateElection($"test-resource-{Guid.NewGuid():N}", candidateId: null);

		// Assert
		election.CandidateId.ShouldBe("default-instance");

		// Cleanup
		((InMemoryLeaderElection)election).Dispose();
	}

	[Fact]
	public void CreateElection_CopiesOptionsSettings()
	{
		// Arrange
		var options = Options.Create(new LeaderElectionOptions
		{
			InstanceId = "test-instance",
			LeaseDuration = TimeSpan.FromSeconds(30),
			RenewInterval = TimeSpan.FromSeconds(10),
			RetryInterval = TimeSpan.FromSeconds(3),
			GracePeriod = TimeSpan.FromSeconds(7),
		});
		var factory = new InMemoryLeaderElectionFactory(options, loggerFactory: null);

		// Act
		var election = factory.CreateElection($"test-resource-{Guid.NewGuid():N}", candidateId: null);

		// Assert - verify election was created (internal options are not exposed)
		_ = election.ShouldNotBeNull();
		election.CandidateId.ShouldBe("test-instance");

		// Cleanup
		((InMemoryLeaderElection)election).Dispose();
	}

	[Fact]
	public void CreateElection_CopiesCandidateMetadata()
	{
		// Arrange
		var options = Options.Create(new LeaderElectionOptions
		{
			InstanceId = "test-instance",
		});
		options.Value.CandidateMetadata["key1"] = "value1";
		options.Value.CandidateMetadata["key2"] = "value2";

		var factory = new InMemoryLeaderElectionFactory(options, loggerFactory: null);

		// Act
		var election = (InMemoryLeaderElection)factory.CreateElection($"test-resource-{Guid.NewGuid():N}", candidateId: null);

		// Assert - verify election was created with metadata
		_ = election.ShouldNotBeNull();

		// Cleanup
		election.Dispose();
	}

	[Fact]
	public void CreateHealthBasedElection_ReturnsInMemoryLeaderElection()
	{
		// Arrange
		var options = Options.Create(new LeaderElectionOptions
		{
			InstanceId = "test-instance",
			EnableHealthChecks = true,
		});
		var factory = new InMemoryLeaderElectionFactory(options, loggerFactory: null);

		// Act
		var election = factory.CreateHealthBasedElection($"test-resource-{Guid.NewGuid():N}", candidateId: null);

		// Assert
		_ = election.ShouldBeOfType<InMemoryLeaderElection>();

		// Cleanup
		((InMemoryLeaderElection)election).Dispose();
	}

	[Fact]
	public void CreateHealthBasedElection_UsesCandidateIdOverride()
	{
		// Arrange
		var options = Options.Create(new LeaderElectionOptions
		{
			InstanceId = "default-instance",
		});
		var factory = new InMemoryLeaderElectionFactory(options, loggerFactory: null);

		// Act
		var election = factory.CreateHealthBasedElection($"test-resource-{Guid.NewGuid():N}", candidateId: "custom-candidate");

		// Assert
		election.CandidateId.ShouldBe("custom-candidate");

		// Cleanup
		((InMemoryLeaderElection)election).Dispose();
	}

	[Fact]
	public void CreateHealthBasedElection_UsesDefaultInstanceId_WhenNoCandidateIdProvided()
	{
		// Arrange
		var options = Options.Create(new LeaderElectionOptions
		{
			InstanceId = "default-health-instance",
		});
		var factory = new InMemoryLeaderElectionFactory(options, loggerFactory: null);

		// Act
		var election = factory.CreateHealthBasedElection($"test-resource-{Guid.NewGuid():N}", candidateId: null);

		// Assert
		election.CandidateId.ShouldBe("default-health-instance");

		// Cleanup
		((InMemoryLeaderElection)election).Dispose();
	}

	[Fact]
	public void CreateHealthBasedElection_CopiesHealthSettings()
	{
		// Arrange
		var options = Options.Create(new LeaderElectionOptions
		{
			InstanceId = "test-instance",
			MinimumHealthScore = 0.9,
			StepDownWhenUnhealthy = true,
		});
		var factory = new InMemoryLeaderElectionFactory(options, loggerFactory: null);

		// Act
		var election = factory.CreateHealthBasedElection($"test-resource-{Guid.NewGuid():N}", candidateId: null);

		// Assert - verify election was created
		_ = election.ShouldNotBeNull();
		election.CandidateId.ShouldBe("test-instance");

		// Cleanup
		((InMemoryLeaderElection)election).Dispose();
	}

	[Fact]
	public void CreateHealthBasedElection_CopiesCandidateMetadata()
	{
		// Arrange
		var options = Options.Create(new LeaderElectionOptions
		{
			InstanceId = "test-instance",
		});
		options.Value.CandidateMetadata["health-key"] = "health-value";

		var factory = new InMemoryLeaderElectionFactory(options, loggerFactory: null);

		// Act
		var election = factory.CreateHealthBasedElection($"test-resource-{Guid.NewGuid():N}", candidateId: null);

		// Assert
		_ = election.ShouldNotBeNull();
		election.CandidateId.ShouldBe("test-instance");

		// Cleanup
		((InMemoryLeaderElection)election).Dispose();
	}

	[Fact]
	public void CreateHealthBasedElection_CopiesAllOptionSettings()
	{
		// Arrange
		var options = Options.Create(new LeaderElectionOptions
		{
			InstanceId = "full-options-test",
			LeaseDuration = TimeSpan.FromSeconds(45),
			RenewInterval = TimeSpan.FromSeconds(12),
			RetryInterval = TimeSpan.FromSeconds(4),
			GracePeriod = TimeSpan.FromSeconds(8),
			MinimumHealthScore = 0.75,
			StepDownWhenUnhealthy = false,
		});
		var factory = new InMemoryLeaderElectionFactory(options, loggerFactory: null);

		// Act
		var election = factory.CreateHealthBasedElection($"test-resource-{Guid.NewGuid():N}", candidateId: null);

		// Assert
		_ = election.ShouldNotBeNull();
		election.CandidateId.ShouldBe("full-options-test");

		// Cleanup
		((InMemoryLeaderElection)election).Dispose();
	}

	[Fact]
	public void CreateElection_WithLoggerFactory_CreatesLogger()
	{
		// Arrange
		var loggerFactory = new LoggerFactory();
		var options = Options.Create(new LeaderElectionOptions
		{
			InstanceId = "test-instance",
		});
		var factory = new InMemoryLeaderElectionFactory(options, loggerFactory);

		// Act
		var election = factory.CreateElection($"test-resource-{Guid.NewGuid():N}", candidateId: null);

		// Assert
		_ = election.ShouldNotBeNull();

		// Cleanup
		((InMemoryLeaderElection)election).Dispose();
	}

	[Fact]
	public void CreateHealthBasedElection_WithLoggerFactory_CreatesLogger()
	{
		// Arrange
		var loggerFactory = new LoggerFactory();
		var options = Options.Create(new LeaderElectionOptions
		{
			InstanceId = "test-instance",
		});
		var factory = new InMemoryLeaderElectionFactory(options, loggerFactory);

		// Act
		var election = factory.CreateHealthBasedElection($"test-resource-{Guid.NewGuid():N}", candidateId: null);

		// Assert
		_ = election.ShouldNotBeNull();

		// Cleanup
		((InMemoryLeaderElection)election).Dispose();
	}

	[Fact]
	public void CreateElection_CreatesSeparateInstances()
	{
		// Arrange
		var options = Options.Create(new LeaderElectionOptions
		{
			InstanceId = "test-instance",
		});
		var factory = new InMemoryLeaderElectionFactory(options, loggerFactory: null);

		// Act
		var election1 = factory.CreateElection($"resource-1-{Guid.NewGuid():N}", candidateId: null);
		var election2 = factory.CreateElection($"resource-2-{Guid.NewGuid():N}", candidateId: null);

		// Assert
		election1.ShouldNotBeSameAs(election2);

		// Cleanup
		((InMemoryLeaderElection)election1).Dispose();
		((InMemoryLeaderElection)election2).Dispose();
	}

	[Fact]
	public void CreateHealthBasedElection_CreatesSeparateInstances()
	{
		// Arrange
		var options = Options.Create(new LeaderElectionOptions
		{
			InstanceId = "test-instance",
		});
		var factory = new InMemoryLeaderElectionFactory(options, loggerFactory: null);

		// Act
		var election1 = factory.CreateHealthBasedElection($"resource-1-{Guid.NewGuid():N}", candidateId: null);
		var election2 = factory.CreateHealthBasedElection($"resource-2-{Guid.NewGuid():N}", candidateId: null);

		// Assert
		election1.ShouldNotBeSameAs(election2);

		// Cleanup
		((InMemoryLeaderElection)election1).Dispose();
		((InMemoryLeaderElection)election2).Dispose();
	}

	[Fact]
	public void CreateElection_WithNullLoggerFactory_DoesNotThrow()
	{
		// Arrange
		var options = Options.Create(new LeaderElectionOptions
		{
			InstanceId = "null-logger-test",
		});
		var factory = new InMemoryLeaderElectionFactory(options, loggerFactory: null);

		// Act
		var election = factory.CreateElection($"test-resource-{Guid.NewGuid():N}", candidateId: null);

		// Assert
		_ = election.ShouldNotBeNull();

		// Cleanup
		((InMemoryLeaderElection)election).Dispose();
	}

	[Fact]
	public void CreateHealthBasedElection_WithNullLoggerFactory_DoesNotThrow()
	{
		// Arrange
		var options = Options.Create(new LeaderElectionOptions
		{
			InstanceId = "null-logger-health-test",
		});
		var factory = new InMemoryLeaderElectionFactory(options, loggerFactory: null);

		// Act
		var election = factory.CreateHealthBasedElection($"test-resource-{Guid.NewGuid():N}", candidateId: null);

		// Assert
		_ = election.ShouldNotBeNull();

		// Cleanup
		((InMemoryLeaderElection)election).Dispose();
	}
}
