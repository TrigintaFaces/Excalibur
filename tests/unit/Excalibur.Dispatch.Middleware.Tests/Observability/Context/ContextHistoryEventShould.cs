// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Context;

namespace Excalibur.Dispatch.Middleware.Tests.Observability.Context;

/// <summary>
/// Unit tests for <see cref="ContextHistoryEvent"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
public sealed class ContextHistoryEventShould : UnitTestBase
{
	[Fact]
	public void CreateWithRequiredProperties()
	{
		// Arrange & Act
		var historyEvent = new ContextHistoryEvent
		{
			EventType = "ContextCreated"
		};

		// Assert
		historyEvent.EventType.ShouldBe("ContextCreated");
	}

	[Fact]
	public void CreateWithAllProperties()
	{
		// Arrange
		var timestamp = DateTimeOffset.UtcNow;

		// Act
		var historyEvent = new ContextHistoryEvent
		{
			EventType = "FieldAdded",
			Timestamp = timestamp,
			Details = "Added CorrelationId field",
			Stage = "Pipeline",
			ThreadId = 42,
			FieldCount = 5,
			SizeBytes = 1024
		};

		// Assert
		historyEvent.EventType.ShouldBe("FieldAdded");
		historyEvent.Timestamp.ShouldBe(timestamp);
		historyEvent.Details.ShouldBe("Added CorrelationId field");
		historyEvent.Stage.ShouldBe("Pipeline");
		historyEvent.ThreadId.ShouldBe(42);
		historyEvent.FieldCount.ShouldBe(5);
		historyEvent.SizeBytes.ShouldBe(1024);
	}

	[Fact]
	public void AllowNullOptionalProperties()
	{
		// Arrange & Act
		var historyEvent = new ContextHistoryEvent
		{
			EventType = "TestEvent",
			Details = null,
			Stage = null
		};

		// Assert
		historyEvent.Details.ShouldBeNull();
		historyEvent.Stage.ShouldBeNull();
	}

	[Fact]
	public void DefaultThreadIdIsZero()
	{
		// Arrange & Act
		var historyEvent = new ContextHistoryEvent
		{
			EventType = "TestEvent"
		};

		// Assert
		historyEvent.ThreadId.ShouldBe(0);
	}

	[Fact]
	public void DefaultFieldCountIsZero()
	{
		// Arrange & Act
		var historyEvent = new ContextHistoryEvent
		{
			EventType = "TestEvent"
		};

		// Assert
		historyEvent.FieldCount.ShouldBe(0);
	}

	[Fact]
	public void DefaultSizeBytesIsZero()
	{
		// Arrange & Act
		var historyEvent = new ContextHistoryEvent
		{
			EventType = "TestEvent"
		};

		// Assert
		historyEvent.SizeBytes.ShouldBe(0);
	}

	[Theory]
	[InlineData("ContextCreated")]
	[InlineData("FieldAdded")]
	[InlineData("FieldRemoved")]
	[InlineData("FieldModified")]
	[InlineData("ContextCleared")]
	public void AcceptVariousEventTypes(string eventType)
	{
		// Arrange & Act
		var historyEvent = new ContextHistoryEvent
		{
			EventType = eventType
		};

		// Assert
		historyEvent.EventType.ShouldBe(eventType);
	}
}
