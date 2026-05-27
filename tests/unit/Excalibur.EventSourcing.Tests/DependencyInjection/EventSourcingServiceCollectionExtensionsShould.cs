// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Snapshots;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

namespace Excalibur.EventSourcing.Tests.DependencyInjection;

/// <summary>
/// Unit tests for <see cref="EventSourcingServiceCollectionExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class EventSourcingServiceCollectionExtensionsShould
{
	#region AddExcaliburEventSourcing Tests

	[Fact]
	public void AddExcaliburEventSourcing_ShouldRegisterDefaultSnapshotStrategy()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburEventSourcing();
		var provider = services.BuildServiceProvider();

		// Assert
		var snapshotStrategy = provider.GetService<ISnapshotStrategy>();
		_ = snapshotStrategy.ShouldNotBeNull();
		snapshotStrategy.ShouldBeSameAs(NoSnapshotStrategy.Instance);
	}

	[Fact]
	public void AddExcaliburEventSourcing_ShouldReturnServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddExcaliburEventSourcing();

		// Assert
		result.ShouldBe(services);
	}

	[Fact]
	public void AddExcaliburEventSourcing_ShouldThrowOnNullServices()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => services.AddExcaliburEventSourcing());
	}

	[Fact]
	public void AddExcaliburEventSourcing_WithConfigure_ShouldThrowOnNullServices()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => services.AddExcaliburEventSourcing(_ => { }));
	}

	[Fact]
	public void AddExcaliburEventSourcing_WithConfigure_ShouldThrowOnNullConfigure()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => services.AddExcaliburEventSourcing((Action<IEventSourcingBuilder>)null!));
	}

	[Fact]
	public void AddExcaliburEventSourcing_WithConfigure_ShouldInvokeConfigureAction()
	{
		// Arrange
		var services = new ServiceCollection();
		var configureWasCalled = false;

		// Act
		_ = services.AddExcaliburEventSourcing(builder =>
		{
			configureWasCalled = true;
			_ = builder.ShouldNotBeNull();
		});

		// Assert
		configureWasCalled.ShouldBeTrue();
	}

	[Fact]
	public void AddExcaliburEventSourcing_WithConfigure_ShouldReturnServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddExcaliburEventSourcing(_ => { });

		// Assert
		result.ShouldBe(services);
	}

	[Fact]
	public void AddExcaliburEventSourcing_CalledMultipleTimes_ShouldNotDuplicateRegistrations()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburEventSourcing();
		_ = services.AddExcaliburEventSourcing();
		var provider = services.BuildServiceProvider();

		// Assert - Should still have only one snapshot strategy
		var strategies = services.Where(s => s.ServiceType == typeof(ISnapshotStrategy)).ToList();
		strategies.Count.ShouldBe(1);
	}

	#endregion AddExcaliburEventSourcing Tests

	#region HasExcaliburEventSourcing Tests

	[Fact]
	public void HasExcaliburEventSourcing_ShouldReturnFalse_WhenNotRegistered()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.HasExcaliburEventSourcing();

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void HasExcaliburEventSourcing_ShouldReturnTrue_WhenRegistered()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddExcaliburEventSourcing();

		// Act
		var result = services.HasExcaliburEventSourcing();

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void HasExcaliburEventSourcing_ShouldThrowOnNullServices()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => services.HasExcaliburEventSourcing());
	}

	#endregion HasExcaliburEventSourcing Tests

	#region Snapshot Strategy Tests

	[Fact]
	public void AddExcaliburEventSourcing_BuilderOverridesDefaultSnapshotStrategy()
	{
		// The base AddExcaliburEventSourcing() registers NoSnapshotStrategy as default,
		// but the builder's Use* methods use Replace semantics (last wins), so
		// UseIntervalSnapshots correctly overrides the default.

		// Arrange
		var services = new ServiceCollection();

		// Act - Builder strategy replaces the default NoSnapshotStrategy
		_ = services.AddExcaliburEventSourcing(builder => builder.UseIntervalSnapshots(50));
		var provider = services.BuildServiceProvider();

		// Assert - Builder's strategy wins over the default
		var snapshotStrategy = provider.GetRequiredService<ISnapshotStrategy>();
		_ = snapshotStrategy.ShouldBeOfType<IntervalSnapshotStrategy>();
	}

	[Fact]
	public void Builder_UseIntervalSnapshots_DirectRegistration_ShouldWork()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = new ExcaliburEventSourcingBuilder(services);

		// Act - Register directly via builder without base registration
		_ = builder.UseIntervalSnapshots(50);
		var provider = services.BuildServiceProvider();

		// Assert
		var snapshotStrategy = provider.GetRequiredService<ISnapshotStrategy>();
		_ = snapshotStrategy.ShouldBeOfType<IntervalSnapshotStrategy>();
	}

	[Fact]
	public void Builder_UseTimeBasedSnapshots_DirectRegistration_ShouldWork()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = new ExcaliburEventSourcingBuilder(services);

		// Act
		_ = builder.UseTimeBasedSnapshots(TimeSpan.FromMinutes(5));
		var provider = services.BuildServiceProvider();

		// Assert
		var snapshotStrategy = provider.GetRequiredService<ISnapshotStrategy>();
		_ = snapshotStrategy.ShouldBeOfType<TimeBasedSnapshotStrategy>();
	}

	[Fact]
	public void Builder_UseSizeBasedSnapshots_DirectRegistration_ShouldWork()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = new ExcaliburEventSourcingBuilder(services);

		// Act
		_ = builder.UseSizeBasedSnapshots(1024 * 1024);
		var provider = services.BuildServiceProvider();

		// Assert
		var snapshotStrategy = provider.GetRequiredService<ISnapshotStrategy>();
		_ = snapshotStrategy.ShouldBeOfType<SizeBasedSnapshotStrategy>();
	}

	[Fact]
	public void Builder_UseCompositeSnapshotStrategy_DirectRegistration_ShouldWork()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = new ExcaliburEventSourcingBuilder(services);

		// Act
		_ = builder.UseCompositeSnapshotStrategy(composite => composite
			.AddIntervalStrategy(100)
			.AddTimeBasedStrategy(TimeSpan.FromMinutes(10)));
		var provider = services.BuildServiceProvider();

		// Assert
		var snapshotStrategy = provider.GetRequiredService<ISnapshotStrategy>();
		_ = snapshotStrategy.ShouldBeOfType<CompositeSnapshotStrategy>();
	}

	[Fact]
	public void AddExcaliburEventSourcing_WithNoSnapshots_ShouldUseNoSnapshotStrategy()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburEventSourcing(builder => builder.UseNoSnapshots());
		var provider = services.BuildServiceProvider();

		// Assert
		var snapshotStrategy = provider.GetRequiredService<ISnapshotStrategy>();
		snapshotStrategy.ShouldBeSameAs(NoSnapshotStrategy.Instance);
	}

	#endregion Snapshot Strategy Tests
}
