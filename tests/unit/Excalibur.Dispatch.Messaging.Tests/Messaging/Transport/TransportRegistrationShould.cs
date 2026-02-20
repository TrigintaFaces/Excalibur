// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Transport;

using FakeItEasy;

namespace Excalibur.Dispatch.Tests.Messaging.Transport;

/// <summary>
/// Unit tests for <see cref="TransportRegistration"/>.
/// </summary>
/// <remarks>
/// Tests the transport registration record.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
[Trait("Priority", "0")]
public sealed class TransportRegistrationShould
{
	#region Constructor Tests

	[Fact]
	public void Constructor_SetsAllProperties()
	{
		// Arrange
		var adapter = A.Fake<ITransportAdapter>();
		var transportType = "RabbitMQ";
		var options = new Dictionary<string, object>
		{
			["ConnectionString"] = "amqp://localhost",
			["MaxRetries"] = 3,
		};

		// Act
		var registration = new TransportRegistration(adapter, transportType, options);

		// Assert
		registration.Adapter.ShouldBe(adapter);
		registration.TransportType.ShouldBe(transportType);
		registration.Options.ShouldBe(options);
	}

	[Fact]
	public void Constructor_WithEmptyTransportType_SetsEmptyString()
	{
		// Arrange
		var adapter = A.Fake<ITransportAdapter>();
		var options = new Dictionary<string, object>();

		// Act
		var registration = new TransportRegistration(adapter, string.Empty, options);

		// Assert
		registration.TransportType.ShouldBe(string.Empty);
	}

	[Fact]
	public void Constructor_WithEmptyOptions_SetsEmptyDictionary()
	{
		// Arrange
		var adapter = A.Fake<ITransportAdapter>();
		var options = new Dictionary<string, object>();

		// Act
		var registration = new TransportRegistration(adapter, "Kafka", options);

		// Assert
		registration.Options.ShouldBeEmpty();
	}

	#endregion

	#region Equality Tests

	[Fact]
	public void Equals_SameReference_ReturnsTrue()
	{
		// Arrange
		var adapter = A.Fake<ITransportAdapter>();
		var options = new Dictionary<string, object>();
		var registration = new TransportRegistration(adapter, "Kafka", options);

		// Act & Assert
		registration.Equals(registration).ShouldBeTrue();
	}

	[Fact]
	public void Equals_SameValues_ReturnsTrue()
	{
		// Arrange
		var adapter = A.Fake<ITransportAdapter>();
		var options = new Dictionary<string, object> { ["Key"] = "Value" };

		var reg1 = new TransportRegistration(adapter, "Kafka", options);
		var reg2 = new TransportRegistration(adapter, "Kafka", options);

		// Act & Assert
		reg1.Equals(reg2).ShouldBeTrue();
	}

	[Fact]
	public void Equals_DifferentAdapter_ReturnsFalse()
	{
		// Arrange
		var adapter1 = A.Fake<ITransportAdapter>();
		var adapter2 = A.Fake<ITransportAdapter>();
		var options = new Dictionary<string, object>();

		var reg1 = new TransportRegistration(adapter1, "Kafka", options);
		var reg2 = new TransportRegistration(adapter2, "Kafka", options);

		// Act & Assert
		reg1.Equals(reg2).ShouldBeFalse();
	}

	[Fact]
	public void Equals_DifferentTransportType_ReturnsFalse()
	{
		// Arrange
		var adapter = A.Fake<ITransportAdapter>();
		var options = new Dictionary<string, object>();

		var reg1 = new TransportRegistration(adapter, "Kafka", options);
		var reg2 = new TransportRegistration(adapter, "RabbitMQ", options);

		// Act & Assert
		reg1.Equals(reg2).ShouldBeFalse();
	}

	[Fact]
	public void Equals_DifferentOptions_ReturnsFalse()
	{
		// Arrange
		var adapter = A.Fake<ITransportAdapter>();
		var options1 = new Dictionary<string, object> { ["Key"] = "Value1" };
		var options2 = new Dictionary<string, object> { ["Key"] = "Value2" };

		var reg1 = new TransportRegistration(adapter, "Kafka", options1);
		var reg2 = new TransportRegistration(adapter, "Kafka", options2);

		// Act & Assert
		reg1.Equals(reg2).ShouldBeFalse();
	}

	#endregion

	#region GetHashCode Tests

	[Fact]
	public void GetHashCode_SameValues_ReturnsSameHash()
	{
		// Arrange
		var adapter = A.Fake<ITransportAdapter>();
		var options = new Dictionary<string, object>();

		var reg1 = new TransportRegistration(adapter, "Kafka", options);
		var reg2 = new TransportRegistration(adapter, "Kafka", options);

		// Act & Assert
		reg1.GetHashCode().ShouldBe(reg2.GetHashCode());
	}

	#endregion

	#region Options Tests

	[Fact]
	public void Options_CanContainDifferentValueTypes()
	{
		// Arrange
		var adapter = A.Fake<ITransportAdapter>();
		var options = new Dictionary<string, object>
		{
			["StringValue"] = "test",
			["IntValue"] = 42,
			["BoolValue"] = true,
			["DoubleValue"] = 3.14,
			["NullValue"] = null!,
		};

		// Act
		var registration = new TransportRegistration(adapter, "Test", options);

		// Assert
		registration.Options["StringValue"].ShouldBe("test");
		registration.Options["IntValue"].ShouldBe(42);
		registration.Options["BoolValue"].ShouldBe(true);
		registration.Options["DoubleValue"].ShouldBe(3.14);
		registration.Options["NullValue"].ShouldBeNull();
	}

	#endregion

	#region Record Features Tests

	[Fact]
	public void With_CreatesNewInstanceWithModifiedTransportType()
	{
		// Arrange
		var adapter = A.Fake<ITransportAdapter>();
		var options = new Dictionary<string, object>();
		var original = new TransportRegistration(adapter, "Kafka", options);

		// Act
		var modified = original with { TransportType = "RabbitMQ" };

		// Assert
		modified.TransportType.ShouldBe("RabbitMQ");
		modified.Adapter.ShouldBe(adapter);
		modified.Options.ShouldBe(options);
		original.TransportType.ShouldBe("Kafka"); // Original unchanged
	}

	[Fact]
	public void Deconstruct_ReturnsAllComponents()
	{
		// Arrange
		var adapter = A.Fake<ITransportAdapter>();
		var transportType = "Kafka";
		var options = new Dictionary<string, object> { ["Key"] = "Value" };
		var registration = new TransportRegistration(adapter, transportType, options);

		// Act
		var (deconstructedAdapter, deconstructedType, deconstructedOptions) = registration;

		// Assert
		deconstructedAdapter.ShouldBe(adapter);
		deconstructedType.ShouldBe(transportType);
		deconstructedOptions.ShouldBe(options);
	}

	#endregion
}
