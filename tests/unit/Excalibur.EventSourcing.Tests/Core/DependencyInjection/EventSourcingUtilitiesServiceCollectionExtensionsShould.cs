// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.Snapshots.Upgrading;
using Excalibur.EventSourcing.Snapshots.Versioning;

namespace Excalibur.EventSourcing.Tests.Core.DependencyInjection;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class EventSourcingUtilitiesServiceCollectionExtensionsShould
{
	[Fact]
	public void RegisterSnapshotUpgraderRegistry()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddSnapshotUpgraderRegistry();

		// Assert
		services.ShouldContain(s => s.ServiceType == typeof(SnapshotUpgraderRegistry));
	}

	[Fact]
	public void ThrowWhenServicesIsNullForSnapshotUpgraderRegistry()
	{
		IServiceCollection? services = null;
		Should.Throw<ArgumentNullException>(() => services!.AddSnapshotUpgraderRegistry());
	}

	[Fact]
	public void RegisterSnapshotSchemaVersioning()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddSnapshotSchemaVersioning();

		// Assert
		services.ShouldContain(s => s.ServiceType == typeof(ISnapshotSchemaValidator));
		services.ShouldContain(s => s.ServiceType == typeof(AttributeBasedSnapshotSchemaValidator));
	}

	[Fact]
	public void ThrowWhenServicesIsNullForSnapshotSchemaVersioning()
	{
		IServiceCollection? services = null;
		Should.Throw<ArgumentNullException>(() => services!.AddSnapshotSchemaVersioning());
	}

	[Fact]
	public void RegisterTimeTravelQuery()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton(A.Fake<IEventStore>());

		// Act
		services.AddTimeTravelQuery();

		// Assert
		services.ShouldContain(s => s.ServiceType == typeof(ITimeTravelQuery));
	}

	[Fact]
	public void ThrowWhenServicesIsNullForTimeTravelQuery()
	{
		IServiceCollection? services = null;
		Should.Throw<ArgumentNullException>(() => services!.AddTimeTravelQuery());
	}

	[Fact]
	public void ThrowWhenServicesIsNullForSnapshotEncryption()
	{
		IServiceCollection? services = null;
		Should.Throw<ArgumentNullException>(() => services!.AddSnapshotEncryption());
	}

	[Fact]
	public void ThrowWhenServicesIsNullForSnapshotCompression()
	{
		IServiceCollection? services = null;
		Should.Throw<ArgumentNullException>(() => services!.AddSnapshotCompression());
	}

	[Fact]
	public void ThrowWhenServicesIsNullForEventStoreThroughputMetrics()
	{
		IServiceCollection? services = null;
		Should.Throw<ArgumentNullException>(() => services!.AddEventStoreThroughputMetrics());
	}

	[Fact]
	public void ThrowWhenProviderNameIsEmptyForEventStoreThroughputMetrics()
	{
		var services = new ServiceCollection();
		Should.Throw<ArgumentException>(() => services.AddEventStoreThroughputMetrics(""));
	}

	[Fact]
	public void NotThrowWhenNoSnapshotStoreRegisteredForEncryption()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act - should not throw when no snapshot store is registered
		services.AddSnapshotEncryption();

		// Assert - no exception
	}

	[Fact]
	public void NotThrowWhenNoSnapshotStoreRegisteredForCompression()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act - should not throw when no snapshot store is registered
		services.AddSnapshotCompression();

		// Assert - no exception
	}

	[Fact]
	public void NotThrowWhenNoEventStoreRegisteredForThroughputMetrics()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act - should not throw when no event store is registered
		services.AddEventStoreThroughputMetrics();

		// Assert - no exception
	}

	[Fact]
	public void AcceptConfigureActionForSnapshotCompression()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddSnapshotCompression(opts => opts.MinimumSizeBytes = 512);

		// Assert - no exception
	}

	[Fact]
	public void ThrowWhenConfigureActionIsNullForSnapshotCompression()
	{
		var services = new ServiceCollection();
		Should.Throw<ArgumentNullException>(() => services.AddSnapshotCompression(null!));
	}
}
