// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Context;

namespace Excalibur.Dispatch.Observability.Tests.Context;

/// <summary>
/// Unit tests for <see cref="ContextHistory"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Context")]
public sealed class ContextHistoryShould
{
	#region Required Property Tests

	[Fact]
	public void RequireMessageId()
	{
		// Arrange & Act
		var history = new ContextHistory
		{
			MessageId = "msg-123",
			Events = new List<ContextHistoryEvent>(),
		};

		// Assert
		history.MessageId.ShouldBe("msg-123");
	}

	[Fact]
	public void RequireEvents()
	{
		// Arrange
		var events = new List<ContextHistoryEvent>
		{
			new() { EventType = "Started" },
			new() { EventType = "Completed" },
		};

		// Act
		var history = new ContextHistory
		{
			MessageId = "msg-456",
			Events = events,
		};

		// Assert
		history.Events.ShouldBe(events);
		history.Events.Count.ShouldBe(2);
	}

	#endregion

	#region Optional Property Tests

	[Fact]
	public void HaveNullCorrelationIdByDefault()
	{
		// Arrange & Act
		var history = new ContextHistory
		{
			MessageId = "msg-789",
			Events = new List<ContextHistoryEvent>(),
		};

		// Assert
		history.CorrelationId.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingCorrelationId()
	{
		// Arrange & Act
		var history = new ContextHistory
		{
			MessageId = "msg-abc",
			CorrelationId = "corr-def",
			Events = new List<ContextHistoryEvent>(),
		};

		// Assert
		history.CorrelationId.ShouldBe("corr-def");
	}

	[Fact]
	public void HaveDefaultStartTime()
	{
		// Arrange & Act
		var history = new ContextHistory
		{
			MessageId = "msg-ghi",
			Events = new List<ContextHistoryEvent>(),
		};

		// Assert
		history.StartTime.ShouldBe(default(DateTimeOffset));
	}

	[Fact]
	public void AllowSettingStartTime()
	{
		// Arrange
		var startTime = DateTimeOffset.UtcNow;

		// Act
		var history = new ContextHistory
		{
			MessageId = "msg-jkl",
			StartTime = startTime,
			Events = new List<ContextHistoryEvent>(),
		};

		// Assert
		history.StartTime.ShouldBe(startTime);
	}

	#endregion

	#region Complete Object Tests

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange
		var startTime = DateTimeOffset.UtcNow;
		var events = new List<ContextHistoryEvent>
		{
			new() { EventType = "Started", Timestamp = startTime },
			new() { EventType = "Processing", Timestamp = startTime.AddMilliseconds(50) },
			new() { EventType = "Completed", Timestamp = startTime.AddMilliseconds(100) },
		};

		// Act
		var history = new ContextHistory
		{
			MessageId = "msg-mno",
			CorrelationId = "corr-pqr",
			StartTime = startTime,
			Events = events,
		};

		// Assert
		history.MessageId.ShouldBe("msg-mno");
		history.CorrelationId.ShouldBe("corr-pqr");
		history.StartTime.ShouldBe(startTime);
		history.Events.Count.ShouldBe(3);
	}

	[Fact]
	public void SupportEmptyEvents()
	{
		// Arrange & Act
		var history = new ContextHistory
		{
			MessageId = "msg-stu",
			Events = new List<ContextHistoryEvent>(),
		};

		// Assert
		history.Events.ShouldBeEmpty();
	}

	[Fact]
	public void SupportAddingEvents()
	{
		// Arrange
		var history = new ContextHistory
		{
			MessageId = "msg-vwx",
			Events = new List<ContextHistoryEvent>(),
		};

		// Act
		history.Events.Add(new ContextHistoryEvent { EventType = "New" });

		// Assert
		history.Events.Count.ShouldBe(1);
	}

	#endregion
}
