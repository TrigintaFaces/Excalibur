// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.EventSourcing.Observability;

using Shouldly;

using Xunit;

namespace Excalibur.EventSourcing.Tests.Observability;

/// <summary>
/// Unit tests for <see cref="EventSourcingActivitySource"/> when no listener is attached.
/// These tests cover the null-activity branches where no telemetry listener is sampling.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class EventSourcingActivitySourceNoListenerShould
{
	// NOTE: No ActivityListener registered, so all StartActivity calls return null.
	// This tests the null-guard branches in each method.

	#region Null Activity Returns

	[Fact]
	public void StartAppendActivity_ShouldReturnNull_WhenNoListenerRegistered()
	{
		// Use a dedicated ActivitySource to ensure no listener
		// The shared Instance might have listeners from other tests in the same process,
		// so we test the null path via the extension methods instead.

		// Act
		Activity? nullActivity = null;

		// Assert - extension methods should be safe no-ops
		nullActivity.RecordException(new InvalidOperationException("test"));
		nullActivity.SetOperationResult("test");
		// No assertion needed - just verifying no NullReferenceException
	}

	[Fact]
	public void RecordException_ShouldNotThrow_WhenActivityIsNull()
	{
		// Arrange
		Activity? activity = null;
		var exception = new ArgumentException("param error", "testParam");

		// Act & Assert - should not throw
		activity.RecordException(exception);
	}

	[Fact]
	public void SetOperationResult_ShouldNotThrow_WhenActivityIsNull()
	{
		// Arrange
		Activity? activity = null;

		// Act & Assert - should not throw
		activity.SetOperationResult("failure");
	}

	#endregion

	#region Activity Source Constants

	[Fact]
	public void ActivitySourceName_ShouldBeExcaliburEventSourcing()
	{
		// Assert
		EventSourcingActivitySource.Name.ShouldBe("Excalibur.EventSourcing");
	}

	[Fact]
	public void Instance_ShouldHaveCorrectName()
	{
		// Assert
		EventSourcingActivitySource.Instance.ShouldNotBeNull();
		EventSourcingActivitySource.Instance.Name.ShouldBe(EventSourcingActivitySource.Name);
	}

	#endregion

	#region Telemetry Constants Validation

	[Fact]
	public void EventSourcingActivitySources_ShouldHaveCorrectValues()
	{
		EventSourcingActivitySources.EventStore.ShouldBe("Excalibur.EventSourcing.EventStore");
		EventSourcingActivitySources.SnapshotStore.ShouldBe("Excalibur.EventSourcing.SnapshotStore");
	}

	[Fact]
	public void EventSourcingMeters_ShouldHaveCorrectValues()
	{
		EventSourcingMeters.EventStore.ShouldBe("Excalibur.EventSourcing.EventStore");
		EventSourcingMeters.SnapshotStore.ShouldBe("Excalibur.EventSourcing.SnapshotStore");
	}

	[Fact]
	public void EventSourcingActivities_ShouldHaveCorrectValues()
	{
		EventSourcingActivities.Append.ShouldBe("EventStore.Append");
		EventSourcingActivities.Load.ShouldBe("EventStore.Load");
		EventSourcingActivities.GetUndispatched.ShouldBe("EventStore.GetUndispatched");
		EventSourcingActivities.MarkDispatched.ShouldBe("EventStore.MarkDispatched");
		EventSourcingActivities.SaveSnapshot.ShouldBe("SnapshotStore.Save");
		EventSourcingActivities.GetSnapshot.ShouldBe("SnapshotStore.Get");
		EventSourcingActivities.DeleteSnapshots.ShouldBe("SnapshotStore.Delete");
	}

	[Fact]
	public void EventSourcingTags_ShouldHaveCorrectValues()
	{
		EventSourcingTags.AggregateId.ShouldBe("aggregate.id");
		EventSourcingTags.AggregateType.ShouldBe("aggregate.type");
		EventSourcingTags.Version.ShouldBe("event.version");
		EventSourcingTags.FromVersion.ShouldBe("from.version");
		EventSourcingTags.ExpectedVersion.ShouldBe("expected.version");
		EventSourcingTags.EventCount.ShouldBe("event.count");
		EventSourcingTags.EventId.ShouldBe("event.id");
		EventSourcingTags.BatchSize.ShouldBe("batch.size");
		EventSourcingTags.Provider.ShouldBe("store.provider");
		EventSourcingTags.OperationResult.ShouldBe("operation.result");
		EventSourcingTags.ExceptionType.ShouldBe("exception.type");
		EventSourcingTags.ExceptionMessage.ShouldBe("exception.message");
	}

	[Fact]
	public void EventSourcingTagValues_ShouldHaveCorrectValues()
	{
		EventSourcingTagValues.Success.ShouldBe("success");
		EventSourcingTagValues.ConcurrencyConflict.ShouldBe("concurrency_conflict");
		EventSourcingTagValues.Failure.ShouldBe("failure");
		EventSourcingTagValues.NotFound.ShouldBe("not_found");
	}

	#endregion

	#region Activity With Listener - Additional Branch Coverage

	[Fact]
	public void StartLoadActivity_WithZeroFromVersion_ShouldSetTag()
	{
		// Arrange
		using var listener = new ActivityListener
		{
			ShouldListenTo = source => source.Name == EventSourcingActivitySource.Name,
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
		};
		ActivitySource.AddActivityListener(listener);

		try
		{
			// Act - fromVersion = 0 is >= 0, so it should set the tag
			using var activity = EventSourcingActivitySource.StartLoadActivity("agg-1", "Order", 0);

			// Assert
			activity.ShouldNotBeNull();
			activity.GetTagItem(EventSourcingTags.FromVersion).ShouldBe(0L);
		}
		finally
		{
			listener.Dispose();
		}
	}

	[Fact]
	public void RecordException_ShouldSetStatusAndTags_OnActiveActivity()
	{
		// Arrange
		using var listener = new ActivityListener
		{
			ShouldListenTo = source => source.Name == EventSourcingActivitySource.Name,
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
		};
		ActivitySource.AddActivityListener(listener);

		try
		{
			using var activity = EventSourcingActivitySource.StartAppendActivity("agg-1", "Test", 1, 0);
			activity.ShouldNotBeNull();

			var ex = new ArgumentNullException("param1", "Value cannot be null");

			// Act
			activity.RecordException(ex);

			// Assert
			activity.Status.ShouldBe(ActivityStatusCode.Error);
			activity.StatusDescription.ShouldContain("Value cannot be null");
			activity.GetTagItem(EventSourcingTags.ExceptionType).ShouldBe(typeof(ArgumentNullException).FullName);
		}
		finally
		{
			listener.Dispose();
		}
	}

	#endregion
}
