// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Transport;

namespace Excalibur.Dispatch.Abstractions.Tests.Transport;

/// <summary>
/// Unit tests for <see cref="TransportMeter"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class TransportMeterShould
{
	[Fact]
	public void MeterName_HasExpectedValue()
	{
		// Assert
		TransportMeter.MeterName.ShouldBe("Excalibur.Dispatch.Transport");
	}

	[Fact]
	public void MeterVersion_HasExpectedValue()
	{
		// Assert
		TransportMeter.MeterVersion.ShouldBe("1.0.0");
	}

	[Fact]
	public void RecordMessageSent_DoesNotThrow()
	{
		// Act & Assert - should not throw
		Should.NotThrow(() => TransportMeter.RecordMessageSent("test-transport", "rabbitmq"));
	}

	[Fact]
	public void RecordMessageSent_WithMessageType_DoesNotThrow()
	{
		// Act & Assert
		Should.NotThrow(() => TransportMeter.RecordMessageSent("test-transport", "rabbitmq", "OrderCreated"));
	}

	[Fact]
	public void RecordMessageReceived_DoesNotThrow()
	{
		// Act & Assert
		Should.NotThrow(() => TransportMeter.RecordMessageReceived("test-transport", "kafka"));
	}

	[Fact]
	public void RecordMessageReceived_WithMessageType_DoesNotThrow()
	{
		// Act & Assert
		Should.NotThrow(() => TransportMeter.RecordMessageReceived("test-transport", "kafka", "OrderUpdated"));
	}

	[Fact]
	public void RecordError_DoesNotThrow()
	{
		// Act & Assert
		Should.NotThrow(() => TransportMeter.RecordError("test-transport", "rabbitmq", "timeout"));
	}

	[Fact]
	public void RecordSendDuration_DoesNotThrow()
	{
		// Act & Assert
		Should.NotThrow(() => TransportMeter.RecordSendDuration("test-transport", "rabbitmq", 42.5));
	}

	[Fact]
	public void RecordReceiveDuration_DoesNotThrow()
	{
		// Act & Assert
		Should.NotThrow(() => TransportMeter.RecordReceiveDuration("test-transport", "kafka", 15.3));
	}

	[Fact]
	public void RecordTransportStarted_DoesNotThrow()
	{
		// Act & Assert
		Should.NotThrow(() => TransportMeter.RecordTransportStarted("test-transport", "rabbitmq"));
	}

	[Fact]
	public void RecordTransportStopped_DoesNotThrow()
	{
		// Act & Assert
		Should.NotThrow(() => TransportMeter.RecordTransportStopped("test-transport", "rabbitmq"));
	}

	[Fact]
	public void UpdateTransportState_DoesNotThrow()
	{
		// Act & Assert
		Should.NotThrow(() => TransportMeter.UpdateTransportState(
			"meter-test-transport", "rabbitmq", isConnected: true, pendingMessages: 10));
	}

	[Fact]
	public void RemoveTransport_DoesNotThrow()
	{
		// Arrange
		TransportMeter.UpdateTransportState("removable-transport", "kafka", isConnected: true);

		// Act & Assert
		Should.NotThrow(() => TransportMeter.RemoveTransport("removable-transport"));
	}

	[Fact]
	public void RemoveTransport_NonExistent_DoesNotThrow()
	{
		// Act & Assert
		Should.NotThrow(() => TransportMeter.RemoveTransport("non-existent-transport"));
	}

	[Fact]
	public void UpdateTransportState_CanUpdateExistingState()
	{
		// Arrange
		TransportMeter.UpdateTransportState("update-test", "rabbitmq", isConnected: true, pendingMessages: 5);

		// Act & Assert - updating should not throw
		Should.NotThrow(() => TransportMeter.UpdateTransportState(
			"update-test", "rabbitmq", isConnected: false, pendingMessages: 0));

		// Cleanup
		TransportMeter.RemoveTransport("update-test");
	}
}
