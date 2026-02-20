// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Hosting.Builders;
using Excalibur.Hosting.Configuration;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Integration.Tests.DependencyInjection;

/// <summary>
/// Integration tests for <see cref="ExcaliburOptions"/> root options class
/// and its 5 nested subsystem options (Sprint 502, bd-giiwd).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Component", "DependencyInjection")]
public sealed class ExcaliburOptionsIntegrationShould : IDisposable
{
	private ServiceProvider? _serviceProvider;

	public void Dispose()
	{
		_serviceProvider?.Dispose();
	}

	#region IOptions<ExcaliburOptions> DI Resolution (AC-7)

	[Fact]
	public void ResolveExcaliburOptions_WhenAddExcaliburCalled()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddExcalibur(_ => { });
		_serviceProvider = services.BuildServiceProvider();

		// Assert
		var options = _serviceProvider.GetService<IOptions<ExcaliburOptions>>();
		_ = options.ShouldNotBeNull("IOptions<ExcaliburOptions> should be resolvable after AddExcalibur()");
		_ = options.Value.ShouldNotBeNull();
	}

	[Fact]
	public void ResolveExcaliburOptionsWithDefaults_WhenAddExcaliburCalledWithNoConfig()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddExcalibur(_ => { });
		_serviceProvider = services.BuildServiceProvider();

		// Assert — all nested options should be initialized with defaults
		var options = _serviceProvider.GetRequiredService<IOptions<ExcaliburOptions>>().Value;
		_ = options.EventSourcing.ShouldNotBeNull();
		_ = options.Outbox.ShouldNotBeNull();
		_ = options.Saga.ShouldNotBeNull();
		_ = options.LeaderElection.ShouldNotBeNull();
		_ = options.Cdc.ShouldNotBeNull();
	}

	#endregion

	#region EventSourcingOptions Defaults (AC-1)

	[Fact]
	public void EventSourcingOptions_DefaultEnabled_IsFalse()
	{
		var options = new EventSourcingOptions();
		options.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void EventSourcingOptions_DefaultEnableSnapshots_IsTrue()
	{
		var options = new EventSourcingOptions();
		options.EnableSnapshots.ShouldBeTrue();
	}

	[Fact]
	public void EventSourcingOptions_DefaultSnapshotFrequency_Is100()
	{
		var options = new EventSourcingOptions();
		options.SnapshotFrequency.ShouldBe(100);
	}

	[Fact]
	public void EventSourcingOptions_DefaultReadBatchSize_Is500()
	{
		var options = new EventSourcingOptions();
		options.DefaultReadBatchSize.ShouldBe(500);
	}

	#endregion

	#region OutboxOptions Defaults (AC-2)

	[Fact]
	public void OutboxOptions_DefaultEnabled_IsFalse()
	{
		var options = new OutboxOptions();
		options.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void OutboxOptions_DefaultPollingInterval_IsFiveSeconds()
	{
		var options = new OutboxOptions();
		options.PollingInterval.ShouldBe(TimeSpan.FromSeconds(5));
	}

	[Fact]
	public void OutboxOptions_DefaultMaxBatchSize_Is100()
	{
		var options = new OutboxOptions();
		options.MaxBatchSize.ShouldBe(100);
	}

	[Fact]
	public void OutboxOptions_DefaultMaxRetryAttempts_Is3()
	{
		var options = new OutboxOptions();
		options.MaxRetryAttempts.ShouldBe(3);
	}

	#endregion

	#region SagaOptions Defaults (AC-3)

	[Fact]
	public void SagaOptions_DefaultEnabled_IsFalse()
	{
		var options = new SagaOptions();
		options.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void SagaOptions_DefaultEnableTimeouts_IsFalse()
	{
		var options = new SagaOptions();
		options.EnableTimeouts.ShouldBeFalse();
	}

	[Fact]
	public void SagaOptions_DefaultTimeout_Is30Minutes()
	{
		var options = new SagaOptions();
		options.DefaultTimeout.ShouldBe(TimeSpan.FromMinutes(30));
	}

	#endregion

	#region LeaderElectionOptions Defaults (AC-4)

	[Fact]
	public void LeaderElectionOptions_DefaultEnabled_IsFalse()
	{
		var options = new LeaderElectionOptions();
		options.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void LeaderElectionOptions_DefaultLeaseDuration_Is30Seconds()
	{
		var options = new LeaderElectionOptions();
		options.LeaseDuration.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void LeaderElectionOptions_DefaultRenewInterval_Is10Seconds()
	{
		var options = new LeaderElectionOptions();
		options.RenewInterval.ShouldBe(TimeSpan.FromSeconds(10));
	}

	#endregion

	#region CdcOptions Defaults (AC-5)

	[Fact]
	public void CdcOptions_DefaultEnabled_IsFalse()
	{
		var options = new CdcOptions();
		options.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void CdcOptions_DefaultPollingInterval_Is10Seconds()
	{
		var options = new CdcOptions();
		options.PollingInterval.ShouldBe(TimeSpan.FromSeconds(10));
	}

	[Fact]
	public void CdcOptions_DefaultMaxBatchSize_Is200()
	{
		var options = new CdcOptions();
		options.MaxBatchSize.ShouldBe(200);
	}

	#endregion

	#region ExcaliburOptions Root Properties (AC-6)

	[Fact]
	public void ExcaliburOptions_AllNestedProperties_AreInitialized()
	{
		var options = new ExcaliburOptions();
		_ = options.EventSourcing.ShouldNotBeNull();
		_ = options.EventSourcing.ShouldBeOfType<EventSourcingOptions>();
		_ = options.Outbox.ShouldNotBeNull();
		_ = options.Outbox.ShouldBeOfType<OutboxOptions>();
		_ = options.Saga.ShouldNotBeNull();
		_ = options.Saga.ShouldBeOfType<SagaOptions>();
		_ = options.LeaderElection.ShouldNotBeNull();
		_ = options.LeaderElection.ShouldBeOfType<LeaderElectionOptions>();
		_ = options.Cdc.ShouldNotBeNull();
		_ = options.Cdc.ShouldBeOfType<CdcOptions>();
	}

	#endregion

	#region Setter Tests

	[Fact]
	public void EventSourcingOptions_CanSetProperties()
	{
		var options = new EventSourcingOptions
		{
			Enabled = true,
			EnableSnapshots = false,
			SnapshotFrequency = 50,
			DefaultReadBatchSize = 1000,
		};

		options.Enabled.ShouldBeTrue();
		options.EnableSnapshots.ShouldBeFalse();
		options.SnapshotFrequency.ShouldBe(50);
		options.DefaultReadBatchSize.ShouldBe(1000);
	}

	[Fact]
	public void OutboxOptions_CanSetProperties()
	{
		var options = new OutboxOptions
		{
			Enabled = true,
			PollingInterval = TimeSpan.FromSeconds(10),
			MaxBatchSize = 50,
			MaxRetryAttempts = 5,
		};

		options.Enabled.ShouldBeTrue();
		options.PollingInterval.ShouldBe(TimeSpan.FromSeconds(10));
		options.MaxBatchSize.ShouldBe(50);
		options.MaxRetryAttempts.ShouldBe(5);
	}

	[Fact]
	public void SagaOptions_CanSetProperties()
	{
		var options = new SagaOptions
		{
			Enabled = true,
			EnableTimeouts = true,
			DefaultTimeout = TimeSpan.FromHours(1),
		};

		options.Enabled.ShouldBeTrue();
		options.EnableTimeouts.ShouldBeTrue();
		options.DefaultTimeout.ShouldBe(TimeSpan.FromHours(1));
	}

	[Fact]
	public void LeaderElectionOptions_CanSetProperties()
	{
		var options = new LeaderElectionOptions
		{
			Enabled = true,
			LeaseDuration = TimeSpan.FromMinutes(1),
			RenewInterval = TimeSpan.FromSeconds(20),
		};

		options.Enabled.ShouldBeTrue();
		options.LeaseDuration.ShouldBe(TimeSpan.FromMinutes(1));
		options.RenewInterval.ShouldBe(TimeSpan.FromSeconds(20));
	}

	[Fact]
	public void CdcOptions_CanSetProperties()
	{
		var options = new CdcOptions
		{
			Enabled = true,
			PollingInterval = TimeSpan.FromSeconds(30),
			MaxBatchSize = 500,
		};

		options.Enabled.ShouldBeTrue();
		options.PollingInterval.ShouldBe(TimeSpan.FromSeconds(30));
		options.MaxBatchSize.ShouldBe(500);
	}

	[Fact]
	public void ExcaliburOptions_CanReplaceNestedOptions()
	{
		var options = new ExcaliburOptions();
		var customEs = new EventSourcingOptions { Enabled = true };

		options.EventSourcing = customEs;

		options.EventSourcing.ShouldBeSameAs(customEs);
		options.EventSourcing.Enabled.ShouldBeTrue();
	}

	#endregion
}
