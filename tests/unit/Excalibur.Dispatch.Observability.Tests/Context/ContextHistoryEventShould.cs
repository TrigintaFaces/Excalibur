// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Context;

namespace Excalibur.Dispatch.Observability.Tests.Context;

/// <summary>
/// Unit tests for <see cref="ContextHistoryEvent"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Context")]
public sealed class ContextHistoryEventShould
{
	#region Required Property Tests

	[Fact]
	public void RequireEventType()
	{
		// Arrange & Act
		var evt = new ContextHistoryEvent
		{
			EventType = "Started",
		};

		// Assert
		evt.EventType.ShouldBe("Started");
	}

	#endregion

	#region Default Value Tests

	[Fact]
	public void HaveDefaultTimestamp()
	{
		// Arrange & Act
		var evt = new ContextHistoryEvent
		{
			EventType = "Test",
		};

		// Assert
		evt.Timestamp.ShouldBe(default(DateTimeOffset));
	}

	[Fact]
	public void HaveNullDetailsByDefault()
	{
		// Arrange & Act
		var evt = new ContextHistoryEvent
		{
			EventType = "Test",
		};

		// Assert
		evt.Details.ShouldBeNull();
	}

	[Fact]
	public void HaveNullStageByDefault()
	{
		// Arrange & Act
		var evt = new ContextHistoryEvent
		{
			EventType = "Test",
		};

		// Assert
		evt.Stage.ShouldBeNull();
	}

	[Fact]
	public void HaveDefaultThreadId()
	{
		// Arrange & Act
		var evt = new ContextHistoryEvent
		{
			EventType = "Test",
		};

		// Assert
		evt.ThreadId.ShouldBe(0);
	}

	[Fact]
	public void HaveDefaultFieldCount()
	{
		// Arrange & Act
		var evt = new ContextHistoryEvent
		{
			EventType = "Test",
		};

		// Assert
		evt.FieldCount.ShouldBe(0);
	}

	[Fact]
	public void HaveDefaultSizeBytes()
	{
		// Arrange & Act
		var evt = new ContextHistoryEvent
		{
			EventType = "Test",
		};

		// Assert
		evt.SizeBytes.ShouldBe(0);
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void AllowSettingTimestamp()
	{
		// Arrange
		var timestamp = DateTimeOffset.UtcNow;

		// Act
		var evt = new ContextHistoryEvent
		{
			EventType = "Test",
			Timestamp = timestamp,
		};

		// Assert
		evt.Timestamp.ShouldBe(timestamp);
	}

	[Fact]
	public void AllowSettingDetails()
	{
		// Arrange & Act
		var evt = new ContextHistoryEvent
		{
			EventType = "Error",
			Details = "Exception occurred during processing",
		};

		// Assert
		evt.Details.ShouldBe("Exception occurred during processing");
	}

	[Fact]
	public void AllowSettingStage()
	{
		// Arrange & Act
		var evt = new ContextHistoryEvent
		{
			EventType = "Modified",
			Stage = "PreHandler",
		};

		// Assert
		evt.Stage.ShouldBe("PreHandler");
	}

	[Fact]
	public void AllowSettingThreadId()
	{
		// Arrange & Act
		var evt = new ContextHistoryEvent
		{
			EventType = "Test",
			ThreadId = 42,
		};

		// Assert
		evt.ThreadId.ShouldBe(42);
	}

	[Fact]
	public void AllowSettingFieldCount()
	{
		// Arrange & Act
		var evt = new ContextHistoryEvent
		{
			EventType = "Snapshot",
			FieldCount = 15,
		};

		// Assert
		evt.FieldCount.ShouldBe(15);
	}

	[Fact]
	public void AllowSettingSizeBytes()
	{
		// Arrange & Act
		var evt = new ContextHistoryEvent
		{
			EventType = "Snapshot",
			SizeBytes = 2048,
		};

		// Assert
		evt.SizeBytes.ShouldBe(2048);
	}

	#endregion

	#region Complete Object Tests

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange
		var timestamp = DateTimeOffset.UtcNow;

		// Act
		var evt = new ContextHistoryEvent
		{
			Timestamp = timestamp,
			EventType = "Processing",
			Details = "Message being processed by OrderHandler",
			Stage = "Handler",
			ThreadId = 10,
			FieldCount = 5,
			SizeBytes = 1024,
		};

		// Assert
		evt.Timestamp.ShouldBe(timestamp);
		evt.EventType.ShouldBe("Processing");
		evt.Details.ShouldBe("Message being processed by OrderHandler");
		evt.Stage.ShouldBe("Handler");
		evt.ThreadId.ShouldBe(10);
		evt.FieldCount.ShouldBe(5);
		evt.SizeBytes.ShouldBe(1024);
	}

	[Theory]
	[InlineData("Started")]
	[InlineData("Processing")]
	[InlineData("Modified")]
	[InlineData("Completed")]
	[InlineData("Failed")]
	[InlineData("Snapshot")]
	public void SupportVariousEventTypes(string eventType)
	{
		// Arrange & Act
		var evt = new ContextHistoryEvent
		{
			EventType = eventType,
		};

		// Assert
		evt.EventType.ShouldBe(eventType);
	}

	[Theory]
	[InlineData("PreHandler")]
	[InlineData("Handler")]
	[InlineData("PostHandler")]
	[InlineData("Middleware")]
	[InlineData("Serialization")]
	public void SupportVariousStages(string stage)
	{
		// Arrange & Act
		var evt = new ContextHistoryEvent
		{
			EventType = "Test",
			Stage = stage,
		};

		// Assert
		evt.Stage.ShouldBe(stage);
	}

	#endregion
}
