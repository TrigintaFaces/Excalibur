// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.InMemory.Inbox;
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

		using var provider = services.BuildServiceProvider();

		var eventStore = provider.GetService<IEventStore>();
		eventStore.ShouldNotBeNull();
		eventStore.ShouldBeOfType<InMemoryEventStore>();
	}

	[Fact]
	public void RegisterSnapshotStoreAsSingleton()
	{
		var services = new ServiceCollection();
		services.AddExcaliburTestingStores();

		using var provider = services.BuildServiceProvider();

		var snapshotStore = provider.GetService<ISnapshotStore>();
		snapshotStore.ShouldNotBeNull();
		snapshotStore.ShouldBeOfType<InMemorySnapshotStore>();
	}

	[Fact]
	public void RegisterInboxStoreAsSingleton()
	{
		var services = new ServiceCollection();
		services.AddExcaliburTestingStores();

		using var provider = services.BuildServiceProvider();

		var inboxStore = provider.GetService<IInboxStore>();
		inboxStore.ShouldNotBeNull();
		inboxStore.ShouldBeOfType<InMemoryInboxStore>();
	}

	[Fact]
	public void ReturnSameEventStoreInstance()
	{
		var services = new ServiceCollection();
		services.AddExcaliburTestingStores();

		using var provider = services.BuildServiceProvider();

		var store1 = provider.GetRequiredService<IEventStore>();
		var store2 = provider.GetRequiredService<IEventStore>();
		store1.ShouldBeSameAs(store2);
	}

	[Fact]
	public void ReturnSameSnapshotStoreInstance()
	{
		var services = new ServiceCollection();
		services.AddExcaliburTestingStores();

		using var provider = services.BuildServiceProvider();

		var store1 = provider.GetRequiredService<ISnapshotStore>();
		var store2 = provider.GetRequiredService<ISnapshotStore>();
		store1.ShouldBeSameAs(store2);
	}

	[Fact]
	public void ReturnSameInboxStoreInstance()
	{
		var services = new ServiceCollection();
		services.AddExcaliburTestingStores();

		using var provider = services.BuildServiceProvider();

		var store1 = provider.GetRequiredService<IInboxStore>();
		var store2 = provider.GetRequiredService<IInboxStore>();
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
