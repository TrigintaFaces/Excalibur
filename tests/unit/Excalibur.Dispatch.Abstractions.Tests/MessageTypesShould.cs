// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

namespace Excalibur.Dispatch.Abstractions.Tests;

/// <summary>
/// Unit tests for message types including MessageKinds, MessageEnvelope, and CausationId.
/// </summary>
[Trait("Category", "Unit")]
public sealed class MessageTypesShould : UnitTestBase
{
	#region MessageKinds Tests

	[Fact]
	public void MessageKinds_None_HasZeroValue()
	{
		// Assert
		((int)MessageKinds.None).ShouldBe(0);
	}

	[Fact]
	public void MessageKinds_Action_HasBitFlag1()
	{
		// Assert
		((int)MessageKinds.Action).ShouldBe(1);
	}

	[Fact]
	public void MessageKinds_Event_HasBitFlag2()
	{
		// Assert
		((int)MessageKinds.Event).ShouldBe(2);
	}

	[Fact]
	public void MessageKinds_Document_HasBitFlag4()
	{
		// Assert
		((int)MessageKinds.Document).ShouldBe(4);
	}

	[Fact]
	public void MessageKinds_All_CombinesAllKinds()
	{
		// Assert
		MessageKinds.All.ShouldBe(MessageKinds.Action | MessageKinds.Event | MessageKinds.Document);
	}

	[Fact]
	public void MessageKinds_CanCombineFlags()
	{
		// Arrange
		var combined = MessageKinds.Action | MessageKinds.Event;

		// Assert
		combined.HasFlag(MessageKinds.Action).ShouldBeTrue();
		combined.HasFlag(MessageKinds.Event).ShouldBeTrue();
		combined.HasFlag(MessageKinds.Document).ShouldBeFalse();
	}

	#endregion MessageKinds Tests

	#region MessageEnvelope Tests

	[Fact]
	public void MessageEnvelope_DefaultConstructor_GeneratesMessageId()
	{
		// Act
		var envelope = new MessageEnvelope();

		// Assert
		envelope.MessageId.ShouldNotBeNullOrEmpty();
		Guid.TryParse(envelope.MessageId, out _).ShouldBeTrue();
	}

	[Fact]
	public void MessageEnvelope_DefaultConstructor_SetsReceivedTimestamp()
	{
		// Act
		var before = DateTimeOffset.UtcNow;
		var envelope = new MessageEnvelope();
		var after = DateTimeOffset.UtcNow;

		// Assert
		envelope.ReceivedTimestampUtc.ShouldBeGreaterThanOrEqualTo(before);
		envelope.ReceivedTimestampUtc.ShouldBeLessThanOrEqualTo(after);
	}

	[Fact]
	public void MessageEnvelope_MessageConstructor_StoresMessage()
	{
		// Arrange
		var message = new TestMessage();

		// Act
		var envelope = new MessageEnvelope(message);

		// Assert
		envelope.Message.ShouldBe(message);
	}

	[Fact]
	public void MessageEnvelope_MessageConstructor_WithNull_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new MessageEnvelope(null!));
	}

	[Fact]
	public void MessageEnvelope_CorrelationId_CanBeSetAndRetrieved()
	{
		// Arrange
		var envelope = new MessageEnvelope();
		var correlationId = Guid.NewGuid().ToString();

		// Act
		envelope.CorrelationId = correlationId;

		// Assert
		envelope.CorrelationId.ShouldBe(correlationId);
	}

	[Fact]
	public void MessageEnvelope_CausationId_CanBeSetAndRetrieved()
	{
		// Arrange
		var envelope = new MessageEnvelope();
		var causationId = Guid.NewGuid().ToString();

		// Act
		envelope.CausationId = causationId;

		// Assert
		envelope.CausationId.ShouldBe(causationId);
	}

	[Fact]
	public void MessageEnvelope_SetItem_StoresValue()
	{
		// Arrange
		var envelope = new MessageEnvelope();

		// Act
		envelope.SetItem("key", "value");

		// Assert
		envelope.GetItem<string>("key").ShouldBe("value");
	}

	[Fact]
	public void MessageEnvelope_GetItem_ReturnsDefaultForMissingKey()
	{
		// Arrange
		var envelope = new MessageEnvelope();

		// Act
		var result = envelope.GetItem<string>("missing");

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void MessageEnvelope_RemoveItem_RemovesValue()
	{
		// Arrange
		var envelope = new MessageEnvelope();
		envelope.SetItem("key", "value");

		// Act
		envelope.RemoveItem("key");

		// Assert
		envelope.ContainsItem("key").ShouldBeFalse();
	}

	[Fact]
	public void MessageEnvelope_Headers_CanBeSetAndRetrieved()
	{
		// Arrange
		var envelope = new MessageEnvelope();

		// Act
		envelope.Headers["Content-Type"] = "application/json";

		// Assert
		envelope.Headers["Content-Type"].ShouldBe("application/json");
	}

	[Fact]
	public void MessageEnvelope_Serialization_PreservesProperties()
	{
		// Arrange
		var envelope = new MessageEnvelope
		{
			CorrelationId = Guid.NewGuid().ToString(),
			CausationId = Guid.NewGuid().ToString(),
			TenantId = "tenant-1",
			MessageType = "TestMessage"
		};

		// Act
		var json = JsonSerializer.Serialize(envelope);
		var deserialized = JsonSerializer.Deserialize<MessageEnvelope>(json);

		// Assert
		_ = deserialized.ShouldNotBeNull();
		deserialized.CorrelationId.ShouldBe(envelope.CorrelationId);
		deserialized.CausationId.ShouldBe(envelope.CausationId);
		deserialized.TenantId.ShouldBe(envelope.TenantId);
		deserialized.MessageType.ShouldBe(envelope.MessageType);
	}

	[Fact]
	public void MessageEnvelope_DeliveryCount_DefaultsToZero()
	{
		// Act
		var envelope = new MessageEnvelope();

		// Assert
		envelope.DeliveryCount.ShouldBe(0);
	}

	#endregion MessageEnvelope Tests

	#region CausationId Tests

	[Fact]
	public void CausationId_DefaultConstructor_GeneratesNewGuid()
	{
		// Act
		var causationId = new CausationId();

		// Assert
		causationId.Value.ShouldNotBe(Guid.Empty);
	}

	[Fact]
	public void CausationId_GuidConstructor_StoresValue()
	{
		// Arrange
		var guid = Guid.NewGuid();

		// Act
		var causationId = new CausationId(guid);

		// Assert
		causationId.Value.ShouldBe(guid);
	}

	[Fact]
	public void CausationId_StringConstructor_ParsesGuid()
	{
		// Arrange
		var guid = Guid.NewGuid();

		// Act
		var causationId = new CausationId(guid.ToString());

		// Assert
		causationId.Value.ShouldBe(guid);
	}

	[Fact]
	public void CausationId_StringConstructor_WithNull_ThrowsArgumentException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => new CausationId(null));
	}

	[Fact]
	public void CausationId_Equals_WithSameValue_ReturnsTrue()
	{
		// Arrange
		var guid = Guid.NewGuid();
		var id1 = new CausationId(guid);
		var id2 = new CausationId(guid);

		// Assert
		id1.Equals(id2).ShouldBeTrue();
	}

	[Fact]
	public void CausationId_Equals_WithDifferentValue_ReturnsFalse()
	{
		// Arrange
		var id1 = new CausationId();
		var id2 = new CausationId();

		// Assert
		id1.Equals(id2).ShouldBeFalse();
	}

	[Fact]
	public void CausationId_ToString_ReturnsGuidString()
	{
		// Arrange
		var guid = Guid.NewGuid();
		var causationId = new CausationId(guid);

		// Assert
		causationId.ToString().ShouldBe(guid.ToString());
	}

	#endregion CausationId Tests

	#region Test Fixtures

	private sealed class TestMessage : IDispatchMessage
	{ }

	#endregion Test Fixtures
}
