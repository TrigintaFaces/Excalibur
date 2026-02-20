// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Testing.Tracking;
using Excalibur.Dispatch.Testing.Transport;
using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Testing.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Testing")]
public sealed class DispatchTestingServiceCollectionExtensionsShould
{
	[Fact]
	public async Task RegisterInMemoryTransportSender()
	{
		var services = new ServiceCollection();
		services.AddDispatchTesting();
		await using var sp = services.BuildServiceProvider();

		sp.GetService<InMemoryTransportSender>().ShouldNotBeNull();
	}

	[Fact]
	public async Task RegisterITransportSenderAsInMemory()
	{
		var services = new ServiceCollection();
		services.AddDispatchTesting();
		await using var sp = services.BuildServiceProvider();

		var sender = sp.GetService<ITransportSender>();
		sender.ShouldNotBeNull();
		sender.ShouldBeOfType<InMemoryTransportSender>();
	}

	[Fact]
	public async Task RegisterInMemoryTransportReceiver()
	{
		var services = new ServiceCollection();
		services.AddDispatchTesting();
		await using var sp = services.BuildServiceProvider();

		sp.GetService<InMemoryTransportReceiver>().ShouldNotBeNull();
	}

	[Fact]
	public async Task RegisterITransportReceiverAsInMemory()
	{
		var services = new ServiceCollection();
		services.AddDispatchTesting();
		await using var sp = services.BuildServiceProvider();

		var receiver = sp.GetService<ITransportReceiver>();
		receiver.ShouldNotBeNull();
		receiver.ShouldBeOfType<InMemoryTransportReceiver>();
	}

	[Fact]
	public async Task RegisterInMemoryTransportSubscriber()
	{
		var services = new ServiceCollection();
		services.AddDispatchTesting();
		await using var sp = services.BuildServiceProvider();

		sp.GetService<InMemoryTransportSubscriber>().ShouldNotBeNull();
	}

	[Fact]
	public async Task RegisterITransportSubscriberAsInMemory()
	{
		var services = new ServiceCollection();
		services.AddDispatchTesting();
		await using var sp = services.BuildServiceProvider();

		var subscriber = sp.GetService<ITransportSubscriber>();
		subscriber.ShouldNotBeNull();
		subscriber.ShouldBeOfType<InMemoryTransportSubscriber>();
	}

	[Fact]
	public async Task RegisterDispatchedMessageLog()
	{
		var services = new ServiceCollection();
		services.AddDispatchTesting();
		await using var sp = services.BuildServiceProvider();

		sp.GetService<IDispatchedMessageLog>().ShouldNotBeNull();
	}

	[Fact]
	public void ReturnServiceCollectionForChaining()
	{
		var services = new ServiceCollection();
		var returned = services.AddDispatchTesting();
		returned.ShouldBeSameAs(services);
	}

	[Fact]
	public void ThrowOnNullServiceCollection()
	{
		IServiceCollection services = null!;
		Should.Throw<ArgumentNullException>(() => services.AddDispatchTesting());
	}

	[Fact]
	public async Task AcceptCustomDestinationAndSource()
	{
		var services = new ServiceCollection();
		services.AddDispatchTesting(destination: "custom-dest", source: "custom-src");
		await using var sp = services.BuildServiceProvider();

		var sender = sp.GetRequiredService<InMemoryTransportSender>();
		sender.ShouldNotBeNull();
	}

	[Fact]
	public async Task RegisterSingletonInstances()
	{
		var services = new ServiceCollection();
		services.AddDispatchTesting();
		await using var sp = services.BuildServiceProvider();

		var s1 = sp.GetRequiredService<InMemoryTransportSender>();
		var s2 = sp.GetRequiredService<InMemoryTransportSender>();
		s1.ShouldBeSameAs(s2);
	}
}
