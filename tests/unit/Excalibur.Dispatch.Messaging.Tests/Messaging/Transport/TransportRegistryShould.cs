// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Tests.Messaging.Transport;

/// <summary>
/// Unit tests for <see cref="TransportRegistry"/>.
/// Tests DI merging fix per Sprint 34 bd-790j, bd-4zd4, bd-4jek.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class TransportRegistryShould
{
	#region RegisterTransport Tests

	[Fact]
	public void RegisterTransport_AddTransportSuccessfully()
	{
		// Arrange
		var registry = new TransportRegistry();
		var adapter = CreateAdapter("rabbitmq");

		// Act
		registry.RegisterTransport("rabbitmq", adapter, "RabbitMQ");

		// Assert
		registry.GetTransportAdapter("rabbitmq").ShouldBe(adapter);
	}

	[Fact]
	public void RegisterTransport_ThrowWhenNameIsNull()
	{
		// Arrange
		var registry = new TransportRegistry();
		var adapter = CreateAdapter("test");

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			registry.RegisterTransport(null!, adapter, "Test"));
	}

	[Fact]
	public void RegisterTransport_ThrowWhenNameIsEmpty()
	{
		// Arrange
		var registry = new TransportRegistry();
		var adapter = CreateAdapter("test");

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			registry.RegisterTransport("", adapter, "Test"));
	}

	[Fact]
	public void RegisterTransport_ThrowWhenNameIsWhitespace()
	{
		// Arrange
		var registry = new TransportRegistry();
		var adapter = CreateAdapter("test");

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			registry.RegisterTransport("   ", adapter, "Test"));
	}

	[Fact]
	public void RegisterTransport_ThrowWhenAdapterIsNull()
	{
		// Arrange
		var registry = new TransportRegistry();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			registry.RegisterTransport("test", null!, "Test"));
	}

	[Fact]
	public void RegisterTransport_ThrowWhenTransportTypeIsNull()
	{
		// Arrange
		var registry = new TransportRegistry();
		var adapter = CreateAdapter("test");

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			registry.RegisterTransport("test", adapter, null!));
	}

	[Fact]
	public void RegisterTransport_ThrowWhenDuplicateName()
	{
		// Arrange
		var registry = new TransportRegistry();
		var adapter1 = CreateAdapter("adapter1");
		var adapter2 = CreateAdapter("adapter2");
		registry.RegisterTransport("rabbitmq", adapter1, "RabbitMQ");

		// Act & Assert
		var ex = Should.Throw<InvalidOperationException>(() =>
			registry.RegisterTransport("rabbitmq", adapter2, "RabbitMQ"));
		ex.Message.ShouldContain("rabbitmq");
		ex.Message.ShouldContain("already registered");
	}

	[Fact]
	public void RegisterTransport_AllowMultipleUniqueTransports()
	{
		// Arrange
		var registry = new TransportRegistry();
		var adapter1 = CreateAdapter("adapter1");
		var adapter2 = CreateAdapter("adapter2");
		var adapter3 = CreateAdapter("adapter3");

		// Act
		registry.RegisterTransport("rabbitmq", adapter1, "RabbitMQ");
		registry.RegisterTransport("kafka", adapter2, "Kafka");
		registry.RegisterTransport("servicebus", adapter3, "ServiceBus");

		// Assert
		registry.GetTransportNames().Count().ShouldBe(3);
	}

	[Fact]
	public void RegisterTransport_StoreOptions()
	{
		// Arrange
		var registry = new TransportRegistry();
		var adapter = CreateAdapter("test");
		var options = new Dictionary<string, object>
		{
			["host"] = "localhost",
			["port"] = 5672
		};

		// Act
		registry.RegisterTransport("rabbitmq", adapter, "RabbitMQ", options);

		// Assert
		var registration = registry.GetTransportRegistration("rabbitmq");
		_ = registration.ShouldNotBeNull();
		registration.Options.ShouldContainKey("host");
		registration.Options["host"].ShouldBe("localhost");
	}

	#endregion RegisterTransport Tests

	#region GetTransportAdapter Tests

	[Fact]
	public void GetTransportAdapter_ReturnAdapterWhenFound()
	{
		// Arrange
		var registry = new TransportRegistry();
		var adapter = CreateAdapter("rabbitmq");
		registry.RegisterTransport("rabbitmq", adapter, "RabbitMQ");

		// Act
		var result = registry.GetTransportAdapter("rabbitmq");

		// Assert
		result.ShouldBe(adapter);
	}

	[Fact]
	public void GetTransportAdapter_ReturnNullWhenNotFound()
	{
		// Arrange
		var registry = new TransportRegistry();

		// Act
		var result = registry.GetTransportAdapter("nonexistent");

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void GetTransportAdapter_ThrowWhenNameIsNull()
	{
		// Arrange
		var registry = new TransportRegistry();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			registry.GetTransportAdapter(null!));
	}

	#endregion GetTransportAdapter Tests

	#region GetTransportRegistration Tests

	[Fact]
	public void GetTransportRegistration_ReturnRegistrationWhenFound()
	{
		// Arrange
		var registry = new TransportRegistry();
		var adapter = CreateAdapter("rabbitmq");
		registry.RegisterTransport("rabbitmq", adapter, "RabbitMQ");

		// Act
		var result = registry.GetTransportRegistration("rabbitmq");

		// Assert
		_ = result.ShouldNotBeNull();
		result.Adapter.ShouldBe(adapter);
		result.TransportType.ShouldBe("RabbitMQ");
	}

	[Fact]
	public void GetTransportRegistration_ReturnNullWhenNotFound()
	{
		// Arrange
		var registry = new TransportRegistry();

		// Act
		var result = registry.GetTransportRegistration("nonexistent");

		// Assert
		result.ShouldBeNull();
	}

	#endregion GetTransportRegistration Tests

	#region GetTransportNames Tests

	[Fact]
	public void GetTransportNames_ReturnEmptyWhenNoTransports()
	{
		// Arrange
		var registry = new TransportRegistry();

		// Act
		var result = registry.GetTransportNames();

		// Assert
		result.ShouldBeEmpty();
	}

	[Fact]
	public void GetTransportNames_ReturnAllRegisteredNames()
	{
		// Arrange
		var registry = new TransportRegistry();
		registry.RegisterTransport("rabbitmq", CreateAdapter("1"), "RabbitMQ");
		registry.RegisterTransport("kafka", CreateAdapter("2"), "Kafka");

		// Act
		var result = registry.GetTransportNames().ToList();

		// Assert
		result.Count.ShouldBe(2);
		result.ShouldContain("rabbitmq");
		result.ShouldContain("kafka");
	}

	#endregion GetTransportNames Tests

	#region GetAllTransports Tests

	[Fact]
	public void GetAllTransports_ReturnEmptyDictionaryWhenNoTransports()
	{
		// Arrange
		var registry = new TransportRegistry();

		// Act
		var result = registry.GetAllTransports();

		// Assert
		result.ShouldBeEmpty();
	}

	[Fact]
	public void GetAllTransports_ReturnAllRegistrations()
	{
		// Arrange
		var registry = new TransportRegistry();
		var adapter1 = CreateAdapter("1");
		var adapter2 = CreateAdapter("2");
		registry.RegisterTransport("rabbitmq", adapter1, "RabbitMQ");
		registry.RegisterTransport("kafka", adapter2, "Kafka");

		// Act
		var result = registry.GetAllTransports();

		// Assert
		result.Count.ShouldBe(2);
		result["rabbitmq"].Adapter.ShouldBe(adapter1);
		result["kafka"].Adapter.ShouldBe(adapter2);
	}

	#endregion GetAllTransports Tests

	#region RemoveTransport Tests

	[Fact]
	public void RemoveTransport_ReturnTrueWhenRemoved()
	{
		// Arrange
		var registry = new TransportRegistry();
		registry.RegisterTransport("rabbitmq", CreateAdapter("1"), "RabbitMQ");

		// Act
		var result = registry.RemoveTransport("rabbitmq");

		// Assert
		result.ShouldBeTrue();
		registry.GetTransportAdapter("rabbitmq").ShouldBeNull();
	}

	[Fact]
	public void RemoveTransport_ReturnFalseWhenNotFound()
	{
		// Arrange
		var registry = new TransportRegistry();

		// Act
		var result = registry.RemoveTransport("nonexistent");

		// Assert
		result.ShouldBeFalse();
	}

	#endregion RemoveTransport Tests

	#region Clear Tests

	[Fact]
	public void Clear_RemoveAllTransports()
	{
		// Arrange
		var registry = new TransportRegistry();
		registry.RegisterTransport("rabbitmq", CreateAdapter("1"), "RabbitMQ");
		registry.RegisterTransport("kafka", CreateAdapter("2"), "Kafka");
		registry.SetDefaultTransport("rabbitmq");

		// Act
		registry.Clear();

		// Assert
		registry.GetTransportNames().ShouldBeEmpty();
		registry.HasDefaultTransport.ShouldBeFalse();
	}

	#endregion Clear Tests

	#region Default Transport Tests (bd-4zd4)

	[Fact]
	public void HasDefaultTransport_ReturnFalseByDefault()
	{
		// Arrange
		var registry = new TransportRegistry();

		// Assert
		registry.HasDefaultTransport.ShouldBeFalse();
	}

	[Fact]
	public void DefaultTransportName_ReturnNullByDefault()
	{
		// Arrange
		var registry = new TransportRegistry();

		// Assert
		registry.DefaultTransportName.ShouldBeNull();
	}

	[Fact]
	public void SetDefaultTransport_SetSuccessfully()
	{
		// Arrange
		var registry = new TransportRegistry();
		registry.RegisterTransport("rabbitmq", CreateAdapter("1"), "RabbitMQ");

		// Act
		registry.SetDefaultTransport("rabbitmq");

		// Assert
		registry.HasDefaultTransport.ShouldBeTrue();
		registry.DefaultTransportName.ShouldBe("rabbitmq");
	}

	[Fact]
	public void SetDefaultTransport_ThrowWhenTransportNotRegistered()
	{
		// Arrange
		var registry = new TransportRegistry();
		registry.RegisterTransport("kafka", CreateAdapter("1"), "Kafka");

		// Act & Assert
		var ex = Should.Throw<InvalidOperationException>(() =>
			registry.SetDefaultTransport("rabbitmq"));
		ex.Message.ShouldContain("rabbitmq");
		ex.Message.ShouldContain("not registered");
		ex.Message.ShouldContain("kafka"); // Should list available transports
	}

	[Fact]
	public void SetDefaultTransport_ThrowWhenNameIsNull()
	{
		// Arrange
		var registry = new TransportRegistry();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			registry.SetDefaultTransport(null!));
	}

	[Fact]
	public void SetDefaultTransport_AllowChangingDefault()
	{
		// Arrange
		var registry = new TransportRegistry();
		registry.RegisterTransport("rabbitmq", CreateAdapter("1"), "RabbitMQ");
		registry.RegisterTransport("kafka", CreateAdapter("2"), "Kafka");
		registry.SetDefaultTransport("rabbitmq");

		// Act
		registry.SetDefaultTransport("kafka");

		// Assert
		registry.DefaultTransportName.ShouldBe("kafka");
	}

	[Fact]
	public void GetDefaultTransportAdapter_ReturnAdapterWhenSet()
	{
		// Arrange
		var registry = new TransportRegistry();
		var adapter = CreateAdapter("1");
		registry.RegisterTransport("rabbitmq", adapter, "RabbitMQ");
		registry.SetDefaultTransport("rabbitmq");

		// Act
		var result = registry.GetDefaultTransportAdapter();

		// Assert
		result.ShouldBe(adapter);
	}

	[Fact]
	public void GetDefaultTransportAdapter_ReturnNullWhenNotSet()
	{
		// Arrange
		var registry = new TransportRegistry();
		registry.RegisterTransport("rabbitmq", CreateAdapter("1"), "RabbitMQ");

		// Act
		var result = registry.GetDefaultTransportAdapter();

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void GetDefaultTransportRegistration_ReturnRegistrationWhenSet()
	{
		// Arrange
		var registry = new TransportRegistry();
		var adapter = CreateAdapter("1");
		registry.RegisterTransport("rabbitmq", adapter, "RabbitMQ");
		registry.SetDefaultTransport("rabbitmq");

		// Act
		var result = registry.GetDefaultTransportRegistration();

		// Assert
		_ = result.ShouldNotBeNull();
		result.Adapter.ShouldBe(adapter);
	}

	[Fact]
	public void GetDefaultTransportRegistration_ReturnNullWhenNotSet()
	{
		// Arrange
		var registry = new TransportRegistry();

		// Act
		var result = registry.GetDefaultTransportRegistration();

		// Assert
		result.ShouldBeNull();
	}

	#endregion Default Transport Tests (bd-4zd4)

	#region Thread Safety Tests

	[Fact]
	public async Task RegisterTransport_ThreadSafeForConcurrentRegistrations()
	{
		// Arrange
		var registry = new TransportRegistry();
		var tasks = new List<Task>();

		// Act - Register 100 transports concurrently
		for (var i = 0; i < 100; i++)
		{
			var index = i;
			tasks.Add(Task.Run(() =>
				registry.RegisterTransport($"transport-{index}", CreateAdapter($"adapter-{index}"), "Type")));
		}

		await Task.WhenAll(tasks);

		// Assert
		registry.GetTransportNames().Count().ShouldBe(100);
	}

	#endregion Thread Safety Tests

	private static ITransportAdapter CreateAdapter(string name)
	{
		var adapter = A.Fake<ITransportAdapter>();
		_ = A.CallTo(() => adapter.Name).Returns(name);
		_ = A.CallTo(() => adapter.TransportType).Returns("Test");
		_ = A.CallTo(() => adapter.IsRunning).Returns(true);
		return adapter;
	}
}
