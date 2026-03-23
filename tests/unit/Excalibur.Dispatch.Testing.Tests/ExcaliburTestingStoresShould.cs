// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Inbox.InMemory;
using Excalibur.Data.InMemory.Snapshots;
using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.InMemory;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Testing.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Excalibur.Testing")]
public sealed class ExcaliburTestingStoresShould
{
	[Fact]
	public void RegisterEventStoreAsSingleton()
	{
		var services = new ServiceCollection();
		services.AddExcaliburTestingStores();

		// IEventStore is now registered as a keyed service
		var descriptor = services.FirstOrDefault(sd => sd.ServiceType == typeof(IEventStore) && sd.IsKeyedService);
		descriptor.ShouldNotBeNull();
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
	}

	[Fact]
	public void RegisterSnapshotStoreAsSingleton()
	{
		var services = new ServiceCollection();
		services.AddExcaliburTestingStores();

		// ISnapshotStore is now registered as a keyed service
		var descriptor = services.FirstOrDefault(sd => sd.ServiceType == typeof(ISnapshotStore) && sd.IsKeyedService);
		descriptor.ShouldNotBeNull();
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
	}

	[Fact]
	public void RegisterInboxStoreAsSingleton()
	{
		var services = new ServiceCollection();
		services.AddExcaliburTestingStores();

		// IInboxStore is now registered as a keyed service
		var descriptor = services.FirstOrDefault(sd => sd.ServiceType == typeof(IInboxStore) && sd.IsKeyedService);
		descriptor.ShouldNotBeNull();
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
	}

	[Fact]
	public void ReturnSameEventStoreInstance()
	{
		var services = new ServiceCollection();
		services.AddExcaliburTestingStores();

		using var provider = services.BuildServiceProvider();

		// The concrete type is still registered non-keyed, verify singleton via concrete type
		var store1 = provider.GetRequiredService<InMemoryEventStore>();
		var store2 = provider.GetRequiredService<InMemoryEventStore>();
		store1.ShouldBeSameAs(store2);
	}

	[Fact]
	public void ReturnSameSnapshotStoreInstance()
	{
		var services = new ServiceCollection();
		services.AddExcaliburTestingStores();

		using var provider = services.BuildServiceProvider();

		// The concrete type is still registered non-keyed, verify singleton via concrete type
		var store1 = provider.GetRequiredService<InMemorySnapshotStore>();
		var store2 = provider.GetRequiredService<InMemorySnapshotStore>();
		store1.ShouldBeSameAs(store2);
	}

	[Fact]
	public void ReturnSameInboxStoreInstance()
	{
		var services = new ServiceCollection();
		services.AddExcaliburTestingStores();

		using var provider = services.BuildServiceProvider();

		// The concrete type is still registered non-keyed, verify singleton via concrete type
		var store1 = provider.GetRequiredService<InMemoryInboxStore>();
		var store2 = provider.GetRequiredService<InMemoryInboxStore>();
		store1.ShouldBeSameAs(store2);
	}

	[Fact]
	public void NotOverwriteExistingSnapshotStoreRegistration()
	{
		var services = new ServiceCollection();
		services.TryAddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
		services.TryAddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

		var fakeSnapshotStore = A.Fake<ISnapshotStore>();
		services.AddSingleton(fakeSnapshotStore);

		services.AddExcaliburTestingStores();

		using var provider = services.BuildServiceProvider();

		var resolved = provider.GetRequiredService<ISnapshotStore>();
		resolved.ShouldBeSameAs(fakeSnapshotStore);
	}

	[Fact]
	public void NotOverwriteExistingInboxStoreRegistration()
	{
		var services = new ServiceCollection();
		services.TryAddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
		services.TryAddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

		var fakeInboxStore = A.Fake<IInboxStore>();
		services.AddSingleton(fakeInboxStore);

		services.AddExcaliburTestingStores();

		using var provider = services.BuildServiceProvider();

		var resolved = provider.GetRequiredService<IInboxStore>();
		resolved.ShouldBeSameAs(fakeInboxStore);
	}

	[Fact]
	public void ThrowOnNullServiceCollection()
	{
		IServiceCollection services = null!;
		Should.Throw<ArgumentNullException>(() =>
			services.AddExcaliburTestingStores());
	}

	[Fact]
	public void SupportMethodChaining()
	{
		var services = new ServiceCollection();
		var returned = services.AddExcaliburTestingStores();
		returned.ShouldBeSameAs(services);
	}
}
