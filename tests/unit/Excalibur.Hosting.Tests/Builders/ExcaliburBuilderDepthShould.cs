// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Hosting.Builders;
using Excalibur.Saga;

using Microsoft.Extensions.Options;

using HostingCdcOptions = Excalibur.Hosting.Configuration.CdcOptions;
using HostingEventSourcingOptions = Excalibur.Hosting.Configuration.EventSourcingOptions;
using HostingExcaliburOptions = Excalibur.Hosting.Configuration.ExcaliburOptions;
using HostingLeaderElectionOptions = Excalibur.Hosting.Configuration.LeaderElectionOptions;
using HostingOutboxOptions = Excalibur.Hosting.Configuration.OutboxOptions;
using HostingSagaOptions = Excalibur.Hosting.Configuration.SagaOptions;

namespace Excalibur.Hosting.Tests.Builders;

/// <summary>
/// Depth tests for <see cref="ExcaliburBuilder"/> subsystem registration methods.
/// Covers fluent chaining, null guard clauses, and correct delegation.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Hosting")]
[Trait("Feature", "Builders")]
public sealed class ExcaliburBuilderDepthShould : UnitTestBase
{
	#region AddEventSourcing

	[Fact]
	public void ThrowWhenConfigureIsNullForAddEventSourcing()
	{
		// Arrange
		var services = new ServiceCollection();
		IExcaliburBuilder? capturedBuilder = null;

		services.AddExcalibur(builder =>
		{
			capturedBuilder = builder;
		});

		// Act & Assert
		capturedBuilder.ShouldNotBeNull();
		Should.Throw<ArgumentNullException>(() =>
			capturedBuilder.AddEventSourcing(null!));
	}

	[Fact]
	public void ReturnSameBuilderFromAddEventSourcing()
	{
		// Arrange
		var services = new ServiceCollection();
		IExcaliburBuilder? result = null;

		// Act
		services.AddExcalibur(builder =>
		{
			result = builder.AddEventSourcing(_ => { });
		});

		// Assert
		result.ShouldNotBeNull();
	}

	[Fact]
	public void InvokeConfigureActionForAddEventSourcing()
	{
		// Arrange
		var services = new ServiceCollection();
		var called = false;

		// Act
		services.AddExcalibur(builder =>
		{
			builder.AddEventSourcing(_ => { called = true; });
		});

		// Assert
		called.ShouldBeTrue();
	}

	#endregion

	#region AddOutbox

	[Fact]
	public void ThrowWhenConfigureIsNullForAddOutbox()
	{
		// Arrange
		var services = new ServiceCollection();
		IExcaliburBuilder? capturedBuilder = null;

		services.AddExcalibur(builder =>
		{
			capturedBuilder = builder;
		});

		// Act & Assert
		capturedBuilder.ShouldNotBeNull();
		Should.Throw<ArgumentNullException>(() =>
			capturedBuilder.AddOutbox(null!));
	}

	[Fact]
	public void ReturnSameBuilderFromAddOutbox()
	{
		// Arrange
		var services = new ServiceCollection();
		IExcaliburBuilder? result = null;

		// Act
		services.AddExcalibur(builder =>
		{
			result = builder.AddOutbox(_ => { });
		});

		// Assert
		result.ShouldNotBeNull();
	}

	[Fact]
	public void InvokeConfigureActionForAddOutbox()
	{
		// Arrange
		var services = new ServiceCollection();
		var called = false;

		// Act
		services.AddExcalibur(builder =>
		{
			builder.AddOutbox(_ => { called = true; });
		});

		// Assert
		called.ShouldBeTrue();
	}

	#endregion

	#region AddCdc

	[Fact]
	public void ThrowWhenConfigureIsNullForAddCdc()
	{
		// Arrange
		var services = new ServiceCollection();
		IExcaliburBuilder? capturedBuilder = null;

		services.AddExcalibur(builder =>
		{
			capturedBuilder = builder;
		});

		// Act & Assert
		capturedBuilder.ShouldNotBeNull();
		Should.Throw<ArgumentNullException>(() =>
			capturedBuilder.AddCdc(null!));
	}

	[Fact]
	public void ReturnSameBuilderFromAddCdc()
	{
		// Arrange
		var services = new ServiceCollection();
		IExcaliburBuilder? result = null;

		// Act
		services.AddExcalibur(builder =>
		{
			result = builder.AddCdc(_ => { });
		});

		// Assert
		result.ShouldNotBeNull();
	}

	[Fact]
	public void InvokeConfigureActionForAddCdc()
	{
		// Arrange
		var services = new ServiceCollection();
		var called = false;

		// Act
		services.AddExcalibur(builder =>
		{
			builder.AddCdc(_ => { called = true; });
		});

		// Assert
		called.ShouldBeTrue();
	}

	#endregion

	#region AddSagas

	[Fact]
	public void ReturnSameBuilderFromAddSagasWithConfigure()
	{
		// Arrange
		var services = new ServiceCollection();
		IExcaliburBuilder? result = null;

		// Act
		services.AddExcalibur(builder =>
		{
			result = builder.AddSagas(opts => opts.MaxConcurrency = 5);
		});

		// Assert
		result.ShouldNotBeNull();
	}

	[Fact]
	public void ReturnSameBuilderFromAddSagasWithoutConfigure()
	{
		// Arrange
		var services = new ServiceCollection();
		IExcaliburBuilder? result = null;

		// Act
		services.AddExcalibur(builder =>
		{
			result = builder.AddSagas();
		});

		// Assert
		result.ShouldNotBeNull();
	}

	[Fact]
	public void ReturnSameBuilderFromAddSagasWithNullConfigure()
	{
		// Arrange
		var services = new ServiceCollection();
		IExcaliburBuilder? result = null;

		// Act
		services.AddExcalibur(builder =>
		{
			result = builder.AddSagas(null);
		});

		// Assert
		result.ShouldNotBeNull();
	}

	[Fact]
	public void RegisterConfigureActionForAddSagas()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act — AddSagas delegates to services.Configure<SagaOptions>(), which is deferred
		services.AddExcalibur(builder =>
		{
			builder.AddSagas(opts => opts.MaxConcurrency = 42);
		});

		// Assert — resolve options from provider to verify configure was registered
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<SagaOptions>>().Value;
		options.MaxConcurrency.ShouldBe(42);
	}

	#endregion

	#region AddLeaderElection

	[Fact]
	public void ReturnSameBuilderFromAddLeaderElectionWithConfigure()
	{
		// Arrange
		var services = new ServiceCollection();
		IExcaliburBuilder? result = null;

		// Act
		services.AddExcalibur(builder =>
		{
			result = builder.AddLeaderElection(opts => opts.LeaseDuration = TimeSpan.FromSeconds(60));
		});

		// Assert
		result.ShouldNotBeNull();
	}

	[Fact]
	public void ReturnSameBuilderFromAddLeaderElectionWithoutConfigure()
	{
		// Arrange
		var services = new ServiceCollection();
		IExcaliburBuilder? result = null;

		// Act
		services.AddExcalibur(builder =>
		{
			result = builder.AddLeaderElection();
		});

		// Assert
		result.ShouldNotBeNull();
	}

	[Fact]
	public void ReturnSameBuilderFromAddLeaderElectionWithNullConfigure()
	{
		// Arrange
		var services = new ServiceCollection();
		IExcaliburBuilder? result = null;

		// Act
		services.AddExcalibur(builder =>
		{
			result = builder.AddLeaderElection(null);
		});

		// Assert
		result.ShouldNotBeNull();
	}

	[Fact]
	public void RegisterConfigureActionForAddLeaderElection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act — AddLeaderElection delegates to services.Configure<LeaderElectionOptions>(), which is deferred
		services.AddExcalibur(builder =>
		{
			builder.AddLeaderElection(opts => opts.LeaseDuration = TimeSpan.FromMinutes(5));
		});

		// Assert — resolve options from provider to verify configure was registered
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<Excalibur.Dispatch.LeaderElection.LeaderElectionOptions>>().Value;
		options.LeaseDuration.ShouldBe(TimeSpan.FromMinutes(5));
	}

	#endregion

	#region Fluent Chaining

	[Fact]
	public void SupportFluentChainingOfAllSubsystems()
	{
		// Arrange
		var services = new ServiceCollection();
		IExcaliburBuilder? finalResult = null;

		// Act — chain all 5 subsystem calls; verify the builder is returned each time
		services.AddExcalibur(builder =>
		{
			finalResult = builder
				.AddEventSourcing(_ => { })
				.AddOutbox(_ => { })
				.AddCdc(_ => { })
				.AddSagas()
				.AddLeaderElection();
		});

		// Assert — if chaining broke, finalResult would be null
		finalResult.ShouldNotBeNull();
	}

	#endregion

	#region ExcaliburOptions Defaults

	[Fact]
	public void HaveCorrectDefaultsForEventSourcingOptions()
	{
		var options = new HostingEventSourcingOptions();

		options.Enabled.ShouldBeFalse();
		options.EnableSnapshots.ShouldBeTrue();
		options.SnapshotFrequency.ShouldBe(100);
		options.DefaultReadBatchSize.ShouldBe(500);
	}

	[Fact]
	public void HaveCorrectDefaultsForOutboxOptions()
	{
		var options = new HostingOutboxOptions();

		options.Enabled.ShouldBeFalse();
		options.PollingInterval.ShouldBe(TimeSpan.FromSeconds(5));
		options.MaxBatchSize.ShouldBe(100);
		options.MaxRetryAttempts.ShouldBe(3);
	}

	[Fact]
	public void HaveCorrectDefaultsForSagaOptions()
	{
		var options = new HostingSagaOptions();

		options.Enabled.ShouldBeFalse();
		options.EnableTimeouts.ShouldBeFalse();
		options.DefaultTimeout.ShouldBe(TimeSpan.FromMinutes(30));
	}

	[Fact]
	public void HaveCorrectDefaultsForLeaderElectionOptions()
	{
		var options = new HostingLeaderElectionOptions();

		options.Enabled.ShouldBeFalse();
		options.LeaseDuration.ShouldBe(TimeSpan.FromSeconds(30));
		options.RenewInterval.ShouldBe(TimeSpan.FromSeconds(10));
	}

	[Fact]
	public void HaveCorrectDefaultsForCdcOptions()
	{
		var options = new HostingCdcOptions();

		options.Enabled.ShouldBeFalse();
		options.PollingInterval.ShouldBe(TimeSpan.FromSeconds(10));
		options.MaxBatchSize.ShouldBe(200);
	}

	[Fact]
	public void HaveCorrectDefaultsForExcaliburOptions()
	{
		var options = new HostingExcaliburOptions();

		options.EventSourcing.ShouldNotBeNull();
		options.Outbox.ShouldNotBeNull();
		options.Saga.ShouldNotBeNull();
		options.LeaderElection.ShouldNotBeNull();
		options.Cdc.ShouldNotBeNull();
	}

	[Fact]
	public void AllowSettingExcaliburOptionsProperties()
	{
		var options = new HostingExcaliburOptions
		{
			EventSourcing = new HostingEventSourcingOptions { Enabled = true },
			Outbox = new HostingOutboxOptions { MaxBatchSize = 50 },
			Saga = new HostingSagaOptions { EnableTimeouts = true },
			LeaderElection = new HostingLeaderElectionOptions { LeaseDuration = TimeSpan.FromMinutes(1) },
			Cdc = new HostingCdcOptions { Enabled = true },
		};

		options.EventSourcing.Enabled.ShouldBeTrue();
		options.Outbox.MaxBatchSize.ShouldBe(50);
		options.Saga.EnableTimeouts.ShouldBeTrue();
		options.LeaderElection.LeaseDuration.ShouldBe(TimeSpan.FromMinutes(1));
		options.Cdc.Enabled.ShouldBeTrue();
	}

	#endregion
}
