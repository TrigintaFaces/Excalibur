// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Context;

namespace Excalibur.Dispatch.Middleware.Tests.Observability.Context;

/// <summary>
/// Unit tests for <see cref="ContextHistory"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
public sealed class ContextHistoryShould : UnitTestBase
{
	[Fact]
	public void CreateWithRequiredProperties()
	{
		// Arrange & Act
		var history = new ContextHistory
		{
			MessageId = "msg-123",
			Events = []
		};

		// Assert
		history.MessageId.ShouldBe("msg-123");
		history.Events.ShouldNotBeNull();
		history.Events.ShouldBeEmpty();
	}

	[Fact]
	public void CreateWithAllProperties()
	{
		// Arrange
		var startTime = DateTimeOffset.UtcNow;
		var events = new List<ContextHistoryEvent>
		{
			new() { EventType = "Started" },
			new() { EventType = "Completed" }
		};

		// Act
		var history = new ContextHistory
		{
			MessageId = "msg-456",
			CorrelationId = "corr-789",
			StartTime = startTime,
			Events = events
		};

		// Assert
		history.MessageId.ShouldBe("msg-456");
		history.CorrelationId.ShouldBe("corr-789");
		history.StartTime.ShouldBe(startTime);
		history.Events.Count.ShouldBe(2);
	}

	[Fact]
	public void AllowNullCorrelationId()
	{
		// Arrange & Act
		var history = new ContextHistory
		{
			MessageId = "msg-123",
			Events = [],
			CorrelationId = null
		};

		// Assert
		history.CorrelationId.ShouldBeNull();
	}

	[Fact]
	public void AllowEmptyEventsList()
	{
		// Arrange & Act
		var history = new ContextHistory
		{
			MessageId = "msg-123",
			Events = []
		};

		// Assert
		history.Events.ShouldBeEmpty();
	}

	[Fact]
	public void AllowAddingEventsToList()
	{
		// Arrange
		var history = new ContextHistory
		{
			MessageId = "msg-123",
			Events = new List<ContextHistoryEvent>()
		};

		// Act
		history.Events.Add(new ContextHistoryEvent { EventType = "NewEvent" });

		// Assert
		history.Events.Count.ShouldBe(1);
		history.Events[0].EventType.ShouldBe("NewEvent");
	}
}
