// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Messaging;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Tests.Messaging;

/// <summary>
/// Unit tests for MessageBusProvider class covering registration, resolution, and enumeration.
/// </summary>
/// <remarks>
/// Sprint 411 - Core Pipeline Coverage (T411.3).
/// Target: Increase MessageBusProvider coverage from 44% to 70%.
/// </remarks>
[Trait("Category", "Unit")]
public sealed class MessageBusProviderShould
{
	#region Constructor Tests

	[Fact]
	public void Constructor_Should_Throw_For_Null_ServiceProvider()
	{
		// Arrange
		var registrations = new List<IMessageBusRegistration>();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new MessageBusProvider(null!, registrations));
	}

	[Fact]
	public void Constructor_Should_Throw_For_Null_Registrations()
	{
		// Arrange
		var serviceProvider = A.Fake<IServiceProvider>();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new MessageBusProvider(serviceProvider, null!));
	}

	[Fact]
	public void Constructor_Should_Skip_Null_Registrations()
	{
		// Arrange
		var serviceProvider = A.Fake<IServiceProvider>();
		var registrations = new List<IMessageBusRegistration?>
		{
			null,
			CreateLocalRegistration("bus1"),
			null
		};

		// Act - Should not throw
		var provider = new MessageBusProvider(serviceProvider, registrations!);

		// Assert
		provider.GetAllMessageBusNames().Count().ShouldBe(1);
	}

	[Fact]
	public void Constructor_Should_Throw_For_Duplicate_Local_Bus_Names()
	{
		// Arrange
		var serviceProvider = A.Fake<IServiceProvider>();
		var registrations = new List<IMessageBusRegistration>
		{
			CreateLocalRegistration("duplicate-bus"),
			CreateLocalRegistration("duplicate-bus")
		};

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() =>
			new MessageBusProvider(serviceProvider, registrations));
	}

	[Fact]
	public void Constructor_Should_Throw_For_Duplicate_Remote_Bus_Names()
	{
		// Arrange
		var serviceProvider = A.Fake<IServiceProvider>();
		var registrations = new List<IMessageBusRegistration>
		{
			CreateRemoteRegistration("duplicate-remote"),
			CreateRemoteRegistration("duplicate-remote")
		};

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() =>
			new MessageBusProvider(serviceProvider, registrations));
	}

	[Fact]
	public void Constructor_Should_Reject_Same_Name_For_Local_And_Remote()
	{
		// Note: MessageBusProvider uses a single seen set, meaning names must be
		// unique across both local AND remote registrations.

		// Arrange
		var serviceProvider = CreateServiceProviderWithBuses("shared-name");
		var registrations = new List<IMessageBusRegistration>
		{
			CreateLocalRegistration("shared-name"),
			CreateRemoteRegistration("shared-name")
		};

		// Act & Assert - Should throw because same name used twice
		_ = Should.Throw<InvalidOperationException>(() =>
			new MessageBusProvider(serviceProvider, registrations));
	}

	#endregion

	#region GetMessageBus Tests

	[Fact]
	public void GetMessageBus_Should_Return_Local_Bus()
	{
		// Arrange
		var bus = A.Fake<IMessageBus>();
		var serviceProvider = CreateServiceProviderWithBus("local-bus", bus);
		var registrations = new List<IMessageBusRegistration>
		{
			CreateLocalRegistration("local-bus")
		};
		var provider = new MessageBusProvider(serviceProvider, registrations);

		// Act
		var result = provider.GetMessageBus("local-bus");

		// Assert
		result.ShouldBe(bus);
	}

	[Fact]
	public void GetMessageBus_Should_Return_Remote_Bus_When_Local_Not_Found()
	{
		// Arrange
		var remoteBus = A.Fake<IMessageBus>();
		var serviceProvider = CreateServiceProviderWithBus("remote-bus", remoteBus);
		var registrations = new List<IMessageBusRegistration>
		{
			CreateRemoteRegistration("remote-bus")
		};
		var provider = new MessageBusProvider(serviceProvider, registrations);

		// Act
		var result = provider.GetMessageBus("remote-bus");

		// Assert
		result.ShouldBe(remoteBus);
	}

	[Fact]
	public void GetMessageBus_Should_Return_Null_When_Not_Found()
	{
		// Arrange
		var serviceProvider = A.Fake<IServiceProvider>();
		var registrations = new List<IMessageBusRegistration>();
		var provider = new MessageBusProvider(serviceProvider, registrations);

		// Act
		var result = provider.GetMessageBus("nonexistent");

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void GetMessageBus_Should_Prefer_Local_Over_Remote()
	{
		// Arrange
		var localBus = A.Fake<IMessageBus>();
		var remoteBus = A.Fake<IMessageBus>();
		var serviceCollection = new ServiceCollection();
		_ = serviceCollection.AddKeyedSingleton("shared-bus", localBus);
		var serviceProvider = serviceCollection.BuildServiceProvider();

		var registrations = new List<IMessageBusRegistration>
		{
			CreateLocalRegistration("shared-bus")
		};
		var provider = new MessageBusProvider(serviceProvider, registrations);

		// Act
		var result = provider.GetMessageBus("shared-bus");

		// Assert
		result.ShouldBe(localBus);
	}

	[Fact]
	public void GetMessageBus_Should_Be_Case_Insensitive()
	{
		// Arrange
		var bus = A.Fake<IMessageBus>();
		var serviceProvider = CreateServiceProviderWithBus("MyBus", bus);
		var registrations = new List<IMessageBusRegistration>
		{
			CreateLocalRegistration("MyBus")
		};
		var provider = new MessageBusProvider(serviceProvider, registrations);

		// Act
		var result = provider.GetMessageBus("MYBUS");

		// Assert
		result.ShouldBe(bus);
	}

	#endregion

	#region GetRemoteMessageBus Tests

	[Fact]
	public void GetRemoteMessageBus_Should_Return_Remote_Bus()
	{
		// Arrange
		var remoteBus = A.Fake<IMessageBus>();
		var serviceProvider = CreateServiceProviderWithBus("remote-bus", remoteBus);
		var registrations = new List<IMessageBusRegistration>
		{
			CreateRemoteRegistration("remote-bus")
		};
		var provider = new MessageBusProvider(serviceProvider, registrations);

		// Act
		var result = provider.GetRemoteMessageBus("remote-bus");

		// Assert
		result.ShouldBe(remoteBus);
	}

	[Fact]
	public void GetRemoteMessageBus_Should_Not_Return_Local_Bus()
	{
		// Arrange
		var localBus = A.Fake<IMessageBus>();
		var serviceProvider = CreateServiceProviderWithBus("local-bus", localBus);
		var registrations = new List<IMessageBusRegistration>
		{
			CreateLocalRegistration("local-bus")
		};
		var provider = new MessageBusProvider(serviceProvider, registrations);

		// Act
		var result = provider.GetRemoteMessageBus("local-bus");

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void GetRemoteMessageBus_Should_Return_Null_When_Not_Found()
	{
		// Arrange
		var serviceProvider = A.Fake<IServiceProvider>();
		var registrations = new List<IMessageBusRegistration>();
		var provider = new MessageBusProvider(serviceProvider, registrations);

		// Act
		var result = provider.GetRemoteMessageBus("nonexistent");

		// Assert
		result.ShouldBeNull();
	}

	#endregion

	#region TryGet Tests

	[Fact]
	public void TryGet_Should_Return_True_And_Local_Bus_When_Found()
	{
		// Arrange
		var bus = A.Fake<IMessageBus>();
		var serviceProvider = CreateServiceProviderWithBus("local-bus", bus);
		var registrations = new List<IMessageBusRegistration>
		{
			CreateLocalRegistration("local-bus")
		};
		var provider = new MessageBusProvider(serviceProvider, registrations);

		// Act
		var found = provider.TryGet("local-bus", out var result);

		// Assert
		found.ShouldBeTrue();
		result.ShouldBe(bus);
	}

	[Fact]
	public void TryGet_Should_Return_True_And_Remote_Bus_When_Found()
	{
		// Arrange
		var remoteBus = A.Fake<IMessageBus>();
		var serviceProvider = CreateServiceProviderWithBus("remote-bus", remoteBus);
		var registrations = new List<IMessageBusRegistration>
		{
			CreateRemoteRegistration("remote-bus")
		};
		var provider = new MessageBusProvider(serviceProvider, registrations);

		// Act
		var found = provider.TryGet("remote-bus", out var result);

		// Assert
		found.ShouldBeTrue();
		result.ShouldBe(remoteBus);
	}

	[Fact]
	public void TryGet_Should_Return_False_When_Not_Found()
	{
		// Arrange
		var serviceProvider = A.Fake<IServiceProvider>();
		var registrations = new List<IMessageBusRegistration>();
		var provider = new MessageBusProvider(serviceProvider, registrations);

		// Act
		var found = provider.TryGet("nonexistent", out var result);

		// Assert
		found.ShouldBeFalse();
		result.ShouldBeNull();
	}

	#endregion

	#region TryGetRemote Tests

	[Fact]
	public void TryGetRemote_Should_Return_True_And_Remote_Bus_When_Found()
	{
		// Arrange
		var remoteBus = A.Fake<IMessageBus>();
		var serviceProvider = CreateServiceProviderWithBus("remote-bus", remoteBus);
		var registrations = new List<IMessageBusRegistration>
		{
			CreateRemoteRegistration("remote-bus")
		};
		var provider = new MessageBusProvider(serviceProvider, registrations);

		// Act
		var found = provider.TryGetRemote("remote-bus", out var result);

		// Assert
		found.ShouldBeTrue();
		result.ShouldBe(remoteBus);
	}

	[Fact]
	public void TryGetRemote_Should_Return_False_For_Local_Bus()
	{
		// Arrange
		var localBus = A.Fake<IMessageBus>();
		var serviceProvider = CreateServiceProviderWithBus("local-bus", localBus);
		var registrations = new List<IMessageBusRegistration>
		{
			CreateLocalRegistration("local-bus")
		};
		var provider = new MessageBusProvider(serviceProvider, registrations);

		// Act
		var found = provider.TryGetRemote("local-bus", out var result);

		// Assert
		found.ShouldBeFalse();
		result.ShouldBeNull();
	}

	[Fact]
	public void TryGetRemote_Should_Return_False_When_Not_Found()
	{
		// Arrange
		var serviceProvider = A.Fake<IServiceProvider>();
		var registrations = new List<IMessageBusRegistration>();
		var provider = new MessageBusProvider(serviceProvider, registrations);

		// Act
		var found = provider.TryGetRemote("nonexistent", out var result);

		// Assert
		found.ShouldBeFalse();
		result.ShouldBeNull();
	}

	#endregion

	#region GetAllMessageBuses Tests

	[Fact]
	public void GetAllMessageBuses_Should_Return_All_Buses()
	{
		// Arrange
		var localBus1 = A.Fake<IMessageBus>();
		var localBus2 = A.Fake<IMessageBus>();
		var remoteBus = A.Fake<IMessageBus>();

		var services = new ServiceCollection();
		_ = services.AddKeyedSingleton("local1", localBus1);
		_ = services.AddKeyedSingleton("local2", localBus2);
		_ = services.AddKeyedSingleton("remote1", remoteBus);
		var serviceProvider = services.BuildServiceProvider();

		var registrations = new List<IMessageBusRegistration>
		{
			CreateLocalRegistration("local1"),
			CreateLocalRegistration("local2"),
			CreateRemoteRegistration("remote1")
		};
		var provider = new MessageBusProvider(serviceProvider, registrations);

		// Act
		var result = provider.GetAllMessageBuses().ToList();

		// Assert
		result.Count.ShouldBe(3);
		result.ShouldContain(localBus1);
		result.ShouldContain(localBus2);
		result.ShouldContain(remoteBus);
	}

	[Fact]
	public void GetAllMessageBuses_Should_Return_Empty_When_No_Buses()
	{
		// Arrange
		var serviceProvider = A.Fake<IServiceProvider>();
		var registrations = new List<IMessageBusRegistration>();
		var provider = new MessageBusProvider(serviceProvider, registrations);

		// Act
		var result = provider.GetAllMessageBuses();

		// Assert
		result.ShouldBeEmpty();
	}

	#endregion

	#region GetAllRemoteMessageBuses Tests

	[Fact]
	public void GetAllRemoteMessageBuses_Should_Return_Only_Remote_Buses()
	{
		// Arrange
		var localBus = A.Fake<IMessageBus>();
		var remoteBus1 = A.Fake<IMessageBus>();
		var remoteBus2 = A.Fake<IMessageBus>();

		var services = new ServiceCollection();
		_ = services.AddKeyedSingleton("local1", localBus);
		_ = services.AddKeyedSingleton("remote1", remoteBus1);
		_ = services.AddKeyedSingleton("remote2", remoteBus2);
		var serviceProvider = services.BuildServiceProvider();

		var registrations = new List<IMessageBusRegistration>
		{
			CreateLocalRegistration("local1"),
			CreateRemoteRegistration("remote1"),
			CreateRemoteRegistration("remote2")
		};
		var provider = new MessageBusProvider(serviceProvider, registrations);

		// Act
		var result = provider.GetAllRemoteMessageBuses().ToList();

		// Assert
		result.Count.ShouldBe(2);
		result.ShouldContain(remoteBus1);
		result.ShouldContain(remoteBus2);
		result.ShouldNotContain(localBus);
	}

	[Fact]
	public void GetAllRemoteMessageBuses_Should_Return_Empty_When_No_Remote_Buses()
	{
		// Arrange
		var localBus = A.Fake<IMessageBus>();
		var services = new ServiceCollection();
		_ = services.AddKeyedSingleton("local1", localBus);
		var serviceProvider = services.BuildServiceProvider();

		var registrations = new List<IMessageBusRegistration>
		{
			CreateLocalRegistration("local1")
		};
		var provider = new MessageBusProvider(serviceProvider, registrations);

		// Act
		var result = provider.GetAllRemoteMessageBuses();

		// Assert
		result.ShouldBeEmpty();
	}

	#endregion

	#region GetAllMessageBusNames Tests

	[Fact]
	public void GetAllMessageBusNames_Should_Return_All_Names()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddKeyedSingleton("local1", A.Fake<IMessageBus>());
		_ = services.AddKeyedSingleton("local2", A.Fake<IMessageBus>());
		_ = services.AddKeyedSingleton("remote1", A.Fake<IMessageBus>());
		var serviceProvider = services.BuildServiceProvider();

		var registrations = new List<IMessageBusRegistration>
		{
			CreateLocalRegistration("local1"),
			CreateLocalRegistration("local2"),
			CreateRemoteRegistration("remote1")
		};
		var provider = new MessageBusProvider(serviceProvider, registrations);

		// Act
		var result = provider.GetAllMessageBusNames().ToList();

		// Assert
		result.Count.ShouldBe(3);
		result.ShouldContain("local1");
		result.ShouldContain("local2");
		result.ShouldContain("remote1");
	}

	[Fact]
	public void GetAllMessageBusNames_Should_Return_Empty_When_No_Buses()
	{
		// Arrange
		var serviceProvider = A.Fake<IServiceProvider>();
		var registrations = new List<IMessageBusRegistration>();
		var provider = new MessageBusProvider(serviceProvider, registrations);

		// Act
		var result = provider.GetAllMessageBusNames();

		// Assert
		result.ShouldBeEmpty();
	}

	#endregion

	#region GetAllRemoteMessageBusNames Tests

	[Fact]
	public void GetAllRemoteMessageBusNames_Should_Return_Only_Remote_Names()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddKeyedSingleton("local1", A.Fake<IMessageBus>());
		_ = services.AddKeyedSingleton("remote1", A.Fake<IMessageBus>());
		_ = services.AddKeyedSingleton("remote2", A.Fake<IMessageBus>());
		var serviceProvider = services.BuildServiceProvider();

		var registrations = new List<IMessageBusRegistration>
		{
			CreateLocalRegistration("local1"),
			CreateRemoteRegistration("remote1"),
			CreateRemoteRegistration("remote2")
		};
		var provider = new MessageBusProvider(serviceProvider, registrations);

		// Act
		var result = provider.GetAllRemoteMessageBusNames().ToList();

		// Assert
		result.Count.ShouldBe(2);
		result.ShouldContain("remote1");
		result.ShouldContain("remote2");
		result.ShouldNotContain("local1");
	}

	#endregion

	#region Helper Methods

	private static IMessageBusRegistration CreateLocalRegistration(string name)
	{
		var registration = A.Fake<IMessageBusRegistration>();
		_ = A.CallTo(() => registration.Name).Returns(name);
		_ = A.CallTo(() => registration.IsRemote).Returns(false);
		return registration;
	}

	private static IMessageBusRegistration CreateRemoteRegistration(string name)
	{
		var registration = A.Fake<IMessageBusRegistration>();
		_ = A.CallTo(() => registration.Name).Returns(name);
		_ = A.CallTo(() => registration.IsRemote).Returns(true);
		return registration;
	}

	private static IServiceProvider CreateServiceProviderWithBus(string name, IMessageBus bus)
	{
		var services = new ServiceCollection();
		_ = services.AddKeyedSingleton(name, bus);
		return services.BuildServiceProvider();
	}

	private static IServiceProvider CreateServiceProviderWithBuses(params string[] names)
	{
		var services = new ServiceCollection();
		foreach (var name in names)
		{
			_ = services.AddKeyedSingleton(name, A.Fake<IMessageBus>());
		}
		return services.BuildServiceProvider();
	}

	#endregion
}
