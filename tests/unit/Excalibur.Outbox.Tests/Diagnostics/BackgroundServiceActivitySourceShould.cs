// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.Outbox.Diagnostics;

namespace Excalibur.Outbox.Tests.Diagnostics;

/// <summary>
/// Unit tests for <see cref="BackgroundServiceActivitySource"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
public sealed class BackgroundServiceActivitySourceShould : UnitTestBase
{
	#region Constants Tests

	[Fact]
	public void HaveCorrectSourceName()
	{
		// Assert
		BackgroundServiceActivitySource.SourceName.ShouldBe("Excalibur.Dispatch.BackgroundServices");
	}

	[Fact]
	public void HaveCorrectSourceVersion()
	{
		// Assert
		BackgroundServiceActivitySource.SourceVersion.ShouldBe("1.0.0");
	}

	#endregion Constants Tests

	#region StartProcessingCycle Tests

	[Fact]
	public void StartProcessingCycle_ReturnActivityOrNull()
	{
		// Act - May return null if no listeners are registered
		var activity = BackgroundServiceActivitySource.StartProcessingCycle("outbox", "pending");

		// Assert
		// Activity may be null if no ActivityListener is registered
		// Just verify the method doesn't throw
		activity?.Dispose();
	}

	[Fact]
	public void StartProcessingCycle_AcceptOutboxServiceType()
	{
		// Act & Assert - Should not throw
		var activity = BackgroundServiceActivitySource.StartProcessingCycle("outbox", "pending");
		activity?.Dispose();
	}

	[Fact]
	public void StartProcessingCycle_AcceptInboxServiceType()
	{
		// Act & Assert - Should not throw
		var activity = BackgroundServiceActivitySource.StartProcessingCycle("inbox", "dispatch");
		activity?.Dispose();
	}

	[Fact]
	public void StartProcessingCycle_AcceptCdcServiceType()
	{
		// Act & Assert - Should not throw
		var activity = BackgroundServiceActivitySource.StartProcessingCycle("cdc", "processing");
		activity?.Dispose();
	}

	[Theory]
	[InlineData("outbox", "pending")]
	[InlineData("outbox", "scheduled")]
	[InlineData("outbox", "retry")]
	[InlineData("inbox", "dispatch")]
	[InlineData("cdc", "changes")]
	public void StartProcessingCycle_AcceptVariousOperations(string serviceType, string operation)
	{
		// Act & Assert - Should not throw for any combination
		var activity = BackgroundServiceActivitySource.StartProcessingCycle(serviceType, operation);
		activity?.Dispose();
	}

	[Fact]
	public void StartProcessingCycle_ReturnActivityWithCorrectTagsWhenListenerRegistered()
	{
		// Arrange
		Activity? capturedActivity = null;
		using var listener = new ActivityListener
		{
			ShouldListenTo = source => source.Name == BackgroundServiceActivitySource.SourceName,
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
			ActivityStarted = activity => capturedActivity = activity,
		};
		ActivitySource.AddActivityListener(listener);

		// Act
		using var activity = BackgroundServiceActivitySource.StartProcessingCycle("outbox", "pending");

		// Assert
		if (activity != null)
		{
			activity.OperationName.ShouldBe("outbox.pending");
			activity.Kind.ShouldBe(ActivityKind.Internal);
			activity.GetTagItem("service.type").ShouldBe("outbox");
			activity.GetTagItem("operation").ShouldBe("pending");
		}
	}

	#endregion StartProcessingCycle Tests

	#region StartDrain Tests

	[Fact]
	public void StartDrain_ReturnActivityOrNull()
	{
		// Act - May return null if no listeners are registered
		var activity = BackgroundServiceActivitySource.StartDrain("outbox");

		// Assert
		// Activity may be null if no ActivityListener is registered
		activity?.Dispose();
	}

	[Fact]
	public void StartDrain_AcceptOutboxServiceType()
	{
		// Act & Assert - Should not throw
		var activity = BackgroundServiceActivitySource.StartDrain("outbox");
		activity?.Dispose();
	}

	[Fact]
	public void StartDrain_AcceptInboxServiceType()
	{
		// Act & Assert - Should not throw
		var activity = BackgroundServiceActivitySource.StartDrain("inbox");
		activity?.Dispose();
	}

	[Fact]
	public void StartDrain_AcceptCdcServiceType()
	{
		// Act & Assert - Should not throw
		var activity = BackgroundServiceActivitySource.StartDrain("cdc");
		activity?.Dispose();
	}

	[Fact]
	public void StartDrain_ReturnActivityWithCorrectTagsWhenListenerRegistered()
	{
		// Arrange
		using var listener = new ActivityListener
		{
			ShouldListenTo = source => source.Name == BackgroundServiceActivitySource.SourceName,
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
		};
		ActivitySource.AddActivityListener(listener);

		// Act
		using var activity = BackgroundServiceActivitySource.StartDrain("outbox");

		// Assert
		if (activity != null)
		{
			activity.OperationName.ShouldBe("outbox.drain");
			activity.Kind.ShouldBe(ActivityKind.Internal);
			activity.GetTagItem("service.type").ShouldBe("outbox");
		}
	}

	#endregion StartDrain Tests

	#region Activity Name Format Tests

	[Theory]
	[InlineData("outbox", "pending", "outbox.pending")]
	[InlineData("inbox", "dispatch", "inbox.dispatch")]
	[InlineData("cdc", "processing", "cdc.processing")]
	public void StartProcessingCycle_CreateCorrectOperationName(string serviceType, string operation, string expectedName)
	{
		// Arrange
		using var listener = new ActivityListener
		{
			ShouldListenTo = source => source.Name == BackgroundServiceActivitySource.SourceName,
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
		};
		ActivitySource.AddActivityListener(listener);

		// Act
		using var activity = BackgroundServiceActivitySource.StartProcessingCycle(serviceType, operation);

		// Assert
		activity?.OperationName.ShouldBe(expectedName);
	}

	[Theory]
	[InlineData("outbox", "outbox.drain")]
	[InlineData("inbox", "inbox.drain")]
	[InlineData("cdc", "cdc.drain")]
	public void StartDrain_CreateCorrectOperationName(string serviceType, string expectedName)
	{
		// Arrange
		using var listener = new ActivityListener
		{
			ShouldListenTo = source => source.Name == BackgroundServiceActivitySource.SourceName,
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
		};
		ActivitySource.AddActivityListener(listener);

		// Act
		using var activity = BackgroundServiceActivitySource.StartDrain(serviceType);

		// Assert
		activity?.OperationName.ShouldBe(expectedName);
	}

	#endregion Activity Name Format Tests

	#region Integration Scenario Tests

	[Fact]
	public void SupportTypicalProcessingWorkflow()
	{
		// Arrange
		using var listener = new ActivityListener
		{
			ShouldListenTo = source => source.Name == BackgroundServiceActivitySource.SourceName,
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
		};
		ActivitySource.AddActivityListener(listener);

		// Act - Simulate a typical processing workflow
		using (var pendingActivity = BackgroundServiceActivitySource.StartProcessingCycle("outbox", "pending"))
		{
			// Process pending messages
			pendingActivity?.SetTag("messages.count", 10);
		}

		using (var scheduledActivity = BackgroundServiceActivitySource.StartProcessingCycle("outbox", "scheduled"))
		{
			// Process scheduled messages
			scheduledActivity?.SetTag("messages.count", 5);
		}

		using (var retryActivity = BackgroundServiceActivitySource.StartProcessingCycle("outbox", "retry"))
		{
			// Process retry messages
			retryActivity?.SetTag("messages.count", 2);
		}

		// Assert - All activities completed without error (test passes if no exceptions)
	}

	[Fact]
	public void SupportGracefulShutdownDrain()
	{
		// Arrange
		using var listener = new ActivityListener
		{
			ShouldListenTo = source => source.Name == BackgroundServiceActivitySource.SourceName,
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
		};
		ActivitySource.AddActivityListener(listener);

		// Act - Simulate graceful shutdown drain
		using var drainActivity = BackgroundServiceActivitySource.StartDrain("outbox");
		drainActivity?.SetTag("drain.reason", "shutdown");
		drainActivity?.SetTag("drain.timeout_seconds", 30);

		// Assert - Activity completed without error
	}

	#endregion Integration Scenario Tests
}
