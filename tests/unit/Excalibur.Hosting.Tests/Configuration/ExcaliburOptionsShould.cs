// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Hosting.Configuration;

namespace Excalibur.Hosting.Tests.Configuration;

/// <summary>
/// Unit tests for <see cref="ExcaliburOptions"/> and its nested options classes.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Hosting")]
[Trait("Feature", "Configuration")]
public sealed class ExcaliburOptionsShould
{
	[Fact]
	public void InitializeEventSourcingOptions()
	{
		// Act
		var options = new ExcaliburOptions();

		// Assert
		options.EventSourcing.ShouldNotBeNull();
	}

	[Fact]
	public void InitializeOutboxOptions()
	{
		// Act
		var options = new ExcaliburOptions();

		// Assert
		options.Outbox.ShouldNotBeNull();
	}

	[Fact]
	public void InitializeSagaOptions()
	{
		// Act
		var options = new ExcaliburOptions();

		// Assert
		options.Saga.ShouldNotBeNull();
	}

	[Fact]
	public void InitializeLeaderElectionOptions()
	{
		// Act
		var options = new ExcaliburOptions();

		// Assert
		options.LeaderElection.ShouldNotBeNull();
	}

	[Fact]
	public void InitializeCdcOptions()
	{
		// Act
		var options = new ExcaliburOptions();

		// Assert
		options.Cdc.ShouldNotBeNull();
	}

	[Fact]
	public void AllowReplacingNestedOptions()
	{
		// Arrange
		var options = new ExcaliburOptions();
		var newEs = new EventSourcingOptions { Enabled = true };

		// Act
		options.EventSourcing = newEs;

		// Assert
		options.EventSourcing.ShouldBeSameAs(newEs);
		options.EventSourcing.Enabled.ShouldBeTrue();
	}
}

/// <summary>
/// Unit tests for <see cref="EventSourcingOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Hosting")]
[Trait("Feature", "Configuration")]
public sealed class EventSourcingOptionsShould
{
	[Fact]
	public void HaveEnabledFalseByDefault()
	{
		// Act
		var options = new EventSourcingOptions();

		// Assert
		options.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void HaveEnableSnapshotsTrueByDefault()
	{
		// Act
		var options = new EventSourcingOptions();

		// Assert
		options.EnableSnapshots.ShouldBeTrue();
	}

	[Fact]
	public void HaveSnapshotFrequency100ByDefault()
	{
		// Act
		var options = new EventSourcingOptions();

		// Assert
		options.SnapshotFrequency.ShouldBe(100);
	}

	[Fact]
	public void HaveDefaultReadBatchSize500ByDefault()
	{
		// Act
		var options = new EventSourcingOptions();

		// Assert
		options.DefaultReadBatchSize.ShouldBe(500);
	}

	[Fact]
	public void AllowCustomValues()
	{
		// Act
		var options = new EventSourcingOptions
		{
			Enabled = true,
			EnableSnapshots = false,
			SnapshotFrequency = 50,
			DefaultReadBatchSize = 1000,
		};

		// Assert
		options.Enabled.ShouldBeTrue();
		options.EnableSnapshots.ShouldBeFalse();
		options.SnapshotFrequency.ShouldBe(50);
		options.DefaultReadBatchSize.ShouldBe(1000);
	}
}

/// <summary>
/// Unit tests for <see cref="OutboxOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Hosting")]
[Trait("Feature", "Configuration")]
public sealed class OutboxOptionsShould
{
	[Fact]
	public void HaveEnabledFalseByDefault()
	{
		// Act
		var options = new OutboxOptions();

		// Assert
		options.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void HavePollingInterval5SecondsByDefault()
	{
		// Act
		var options = new OutboxOptions();

		// Assert
		options.PollingInterval.ShouldBe(TimeSpan.FromSeconds(5));
	}

	[Fact]
	public void HaveMaxBatchSize100ByDefault()
	{
		// Act
		var options = new OutboxOptions();

		// Assert
		options.MaxBatchSize.ShouldBe(100);
	}

	[Fact]
	public void HaveMaxRetryAttempts3ByDefault()
	{
		// Act
		var options = new OutboxOptions();

		// Assert
		options.MaxRetryAttempts.ShouldBe(3);
	}
}

/// <summary>
/// Unit tests for <see cref="SagaOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Hosting")]
[Trait("Feature", "Configuration")]
public sealed class SagaOptionsShould
{
	[Fact]
	public void HaveEnabledFalseByDefault()
	{
		// Act
		var options = new SagaOptions();

		// Assert
		options.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void HaveEnableTimeoutsFalseByDefault()
	{
		// Act
		var options = new SagaOptions();

		// Assert
		options.EnableTimeouts.ShouldBeFalse();
	}

	[Fact]
	public void HaveDefaultTimeout30MinutesByDefault()
	{
		// Act
		var options = new SagaOptions();

		// Assert
		options.DefaultTimeout.ShouldBe(TimeSpan.FromMinutes(30));
	}
}

/// <summary>
/// Unit tests for <see cref="LeaderElectionOptions"/> (in ExcaliburOptions).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Hosting")]
[Trait("Feature", "Configuration")]
public sealed class HostingLeaderElectionOptionsShould
{
	[Fact]
	public void HaveEnabledFalseByDefault()
	{
		// Act
		var options = new Excalibur.Hosting.Configuration.LeaderElectionOptions();

		// Assert
		options.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void HaveLeaseDuration30SecondsByDefault()
	{
		// Act
		var options = new Excalibur.Hosting.Configuration.LeaderElectionOptions();

		// Assert
		options.LeaseDuration.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void HaveRenewInterval10SecondsByDefault()
	{
		// Act
		var options = new Excalibur.Hosting.Configuration.LeaderElectionOptions();

		// Assert
		options.RenewInterval.ShouldBe(TimeSpan.FromSeconds(10));
	}
}

/// <summary>
/// Unit tests for <see cref="CdcOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Hosting")]
[Trait("Feature", "Configuration")]
public sealed class CdcOptionsShould
{
	[Fact]
	public void HaveEnabledFalseByDefault()
	{
		// Act
		var options = new CdcOptions();

		// Assert
		options.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void HavePollingInterval10SecondsByDefault()
	{
		// Act
		var options = new CdcOptions();

		// Assert
		options.PollingInterval.ShouldBe(TimeSpan.FromSeconds(10));
	}

	[Fact]
	public void HaveMaxBatchSize200ByDefault()
	{
		// Act
		var options = new CdcOptions();

		// Assert
		options.MaxBatchSize.ShouldBe(200);
	}
}
