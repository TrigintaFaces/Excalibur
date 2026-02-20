// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Observability;

namespace Excalibur.Dispatch.Integration.Tests.Observability.EventSourcing;

/// <summary>
/// Integration tests for EventSourcing OpenTelemetry instrumentation.
/// Validates that ActivitySource spans are correctly created with proper tags.
/// </summary>
[Collection("EventStore Telemetry Tests")]
public sealed class EventSourcingTelemetryShould : IDisposable
{
	private readonly EventSourcingTelemetryTestFixture _fixture;

	public EventSourcingTelemetryShould()
	{
		_fixture = new EventSourcingTelemetryTestFixture();
	}

	#region Event Store Span Creation Tests

	[Fact]
	public void CreateActivityForAppendOperation()
	{
		// Arrange
		const string aggregateId = "order-123";
		const string aggregateType = "Order";
		const int eventCount = 3;
		const long expectedVersion = 5;

		// Act
		using var activity = EventSourcingActivitySource.StartAppendActivity(
			aggregateId,
			aggregateType,
			eventCount,
			expectedVersion);

		// Assert
		_ = activity.ShouldNotBeNull();
		activity.OperationName.ShouldBe(EventSourcingActivities.Append);

		// Verify tags
		activity.GetTagItem(EventSourcingTags.AggregateId).ShouldBe(aggregateId);
		activity.GetTagItem(EventSourcingTags.AggregateType).ShouldBe(aggregateType);
		activity.GetTagItem(EventSourcingTags.EventCount).ShouldBe(eventCount);
		activity.GetTagItem(EventSourcingTags.ExpectedVersion).ShouldBe(expectedVersion);
	}

	[Fact]
	public void CreateActivityForLoadOperation()
	{
		// Arrange
		const string aggregateId = "customer-456";
		const string aggregateType = "Customer";
		const long fromVersion = 10;

		// Act
		using var activity = EventSourcingActivitySource.StartLoadActivity(
			aggregateId,
			aggregateType,
			fromVersion);

		// Assert
		_ = activity.ShouldNotBeNull();
		activity.OperationName.ShouldBe(EventSourcingActivities.Load);

		// Verify tags
		activity.GetTagItem(EventSourcingTags.AggregateId).ShouldBe(aggregateId);
		activity.GetTagItem(EventSourcingTags.AggregateType).ShouldBe(aggregateType);
		activity.GetTagItem(EventSourcingTags.FromVersion).ShouldBe(fromVersion);
	}

	[Fact]
	public void CreateActivityForLoadOperationWithoutFromVersion()
	{
		// Arrange
		const string aggregateId = "product-789";
		const string aggregateType = "Product";

		// Act
		using var activity = EventSourcingActivitySource.StartLoadActivity(
			aggregateId,
			aggregateType);

		// Assert
		_ = activity.ShouldNotBeNull();
		activity.OperationName.ShouldBe(EventSourcingActivities.Load);

		// Verify tags - fromVersion should not be set when -1
		activity.GetTagItem(EventSourcingTags.AggregateId).ShouldBe(aggregateId);
		activity.GetTagItem(EventSourcingTags.AggregateType).ShouldBe(aggregateType);
		activity.GetTagItem(EventSourcingTags.FromVersion).ShouldBeNull();
	}

	[Fact]
	public void CreateActivityForGetUndispatchedOperation()
	{
		// Arrange
		const int batchSize = 100;

		// Act
		using var activity = EventSourcingActivitySource.StartGetUndispatchedActivity(batchSize);

		// Assert
		_ = activity.ShouldNotBeNull();
		activity.OperationName.ShouldBe(EventSourcingActivities.GetUndispatched);

		// Verify tags
		activity.GetTagItem(EventSourcingTags.BatchSize).ShouldBe(batchSize);
	}

	[Fact]
	public void CreateActivityForMarkDispatchedOperation()
	{
		// Arrange
		const string eventId = "evt-abc-123";

		// Act
		using var activity = EventSourcingActivitySource.StartMarkDispatchedActivity(eventId);

		// Assert
		_ = activity.ShouldNotBeNull();
		activity.OperationName.ShouldBe(EventSourcingActivities.MarkDispatched);

		// Verify tags
		activity.GetTagItem(EventSourcingTags.EventId).ShouldBe(eventId);
	}

	#endregion Event Store Span Creation Tests

	#region Snapshot Store Span Creation Tests

	[Fact]
	public void CreateActivityForSaveSnapshotOperation()
	{
		// Arrange
		const string aggregateId = "account-001";
		const string aggregateType = "Account";
		const long version = 50;

		// Act
		using var activity = EventSourcingActivitySource.StartSaveSnapshotActivity(
			aggregateId,
			aggregateType,
			version);

		// Assert
		_ = activity.ShouldNotBeNull();
		activity.OperationName.ShouldBe(EventSourcingActivities.SaveSnapshot);

		// Verify tags
		activity.GetTagItem(EventSourcingTags.AggregateId).ShouldBe(aggregateId);
		activity.GetTagItem(EventSourcingTags.AggregateType).ShouldBe(aggregateType);
		activity.GetTagItem(EventSourcingTags.Version).ShouldBe(version);
	}

	[Fact]
	public void CreateActivityForGetSnapshotOperation()
	{
		// Arrange
		const string aggregateId = "inventory-002";
		const string aggregateType = "Inventory";

		// Act
		using var activity = EventSourcingActivitySource.StartGetSnapshotActivity(
			aggregateId,
			aggregateType);

		// Assert
		_ = activity.ShouldNotBeNull();
		activity.OperationName.ShouldBe(EventSourcingActivities.GetSnapshot);

		// Verify tags
		activity.GetTagItem(EventSourcingTags.AggregateId).ShouldBe(aggregateId);
		activity.GetTagItem(EventSourcingTags.AggregateType).ShouldBe(aggregateType);
	}

	[Fact]
	public void CreateActivityForDeleteSnapshotsOperation()
	{
		// Arrange
		const string aggregateId = "user-003";
		const string aggregateType = "User";

		// Act
		using var activity = EventSourcingActivitySource.StartDeleteSnapshotsActivity(
			aggregateId,
			aggregateType);

		// Assert
		_ = activity.ShouldNotBeNull();
		activity.OperationName.ShouldBe(EventSourcingActivities.DeleteSnapshots);

		// Verify tags
		activity.GetTagItem(EventSourcingTags.AggregateId).ShouldBe(aggregateId);
		activity.GetTagItem(EventSourcingTags.AggregateType).ShouldBe(aggregateType);
	}

	#endregion Snapshot Store Span Creation Tests

	#region Exception Handling Tests

	[Fact]
	public void RecordExceptionOnActivity()
	{
		// Arrange
		using var activity = EventSourcingActivitySource.StartAppendActivity(
			"test-aggregate",
			"TestAggregate",
			1,
			0);

		var exception = new InvalidOperationException("Test exception message");

		// Act
		activity.RecordException(exception);

		// Assert
		_ = activity.ShouldNotBeNull();
		activity.Status.ShouldBe(ActivityStatusCode.Error);
		activity.StatusDescription.ShouldBe("Test exception message");
		activity.GetTagItem(EventSourcingTags.ExceptionType).ShouldBe(typeof(InvalidOperationException).FullName);
		activity.GetTagItem(EventSourcingTags.ExceptionMessage).ShouldBe("Test exception message");
	}

	[Fact]
	public void RecordExceptionGracefullyOnNullActivity()
	{
		// Arrange
		Activity? nullActivity = null;
		var exception = new ArgumentException("Test");

		// Act & Assert - Should not throw
		Should.NotThrow(() => nullActivity.RecordException(exception));
	}

	#endregion Exception Handling Tests

	#region Operation Result Tests

	[Fact]
	public void SetOperationResultTag()
	{
		// Arrange
		using var activity = EventSourcingActivitySource.StartLoadActivity(
			"result-test",
			"TestAggregate");

		// Act
		activity.SetOperationResult(EventSourcingTagValues.Success);

		// Assert
		_ = activity.ShouldNotBeNull();
		activity.GetTagItem(EventSourcingTags.OperationResult).ShouldBe(EventSourcingTagValues.Success);
	}

	[Fact]
	public void SetConcurrencyConflictResult()
	{
		// Arrange
		using var activity = EventSourcingActivitySource.StartAppendActivity(
			"conflict-test",
			"TestAggregate",
			1,
			5);

		// Act
		activity.SetOperationResult(EventSourcingTagValues.ConcurrencyConflict);

		// Assert
		_ = activity.ShouldNotBeNull();
		activity.GetTagItem(EventSourcingTags.OperationResult).ShouldBe(EventSourcingTagValues.ConcurrencyConflict);
	}

	[Fact]
	public void SetNotFoundResult()
	{
		// Arrange
		using var activity = EventSourcingActivitySource.StartGetSnapshotActivity(
			"notfound-test",
			"TestAggregate");

		// Act
		activity.SetOperationResult(EventSourcingTagValues.NotFound);

		// Assert
		_ = activity.ShouldNotBeNull();
		activity.GetTagItem(EventSourcingTags.OperationResult).ShouldBe(EventSourcingTagValues.NotFound);
	}

	[Fact]
	public void SetOperationResultGracefullyOnNullActivity()
	{
		// Arrange
		Activity? nullActivity = null;

		// Act & Assert - Should not throw
		Should.NotThrow(() => nullActivity.SetOperationResult(EventSourcingTagValues.Success));
	}

	#endregion Operation Result Tests

	#region Parent-Child Span Relationship Tests

	[Fact]
	public void MaintainParentChildSpanRelationship()
	{
		// Arrange
		using var parentActivity = new ActivitySource("Test.Parent", "1.0.0")
			.StartActivity("ParentOperation");

		_ = parentActivity.ShouldNotBeNull();

		// Act - Create child activities within parent context
		using var loadActivity = EventSourcingActivitySource.StartLoadActivity(
			"child-test",
			"TestAggregate");

		// Assert
		_ = loadActivity.ShouldNotBeNull();
		loadActivity.ParentId.ShouldBe(parentActivity.Id);
		loadActivity.TraceId.ShouldBe(parentActivity.TraceId);
	}

	[Fact]
	public void PropagateTraceContextThroughNestedOperations()
	{
		// Arrange
		using var outerActivity = new ActivitySource("Test.Outer", "1.0.0")
			.StartActivity("OuterOperation");

		_ = outerActivity.ShouldNotBeNull();

		// Act - Nested event sourcing operations
		using var loadActivity = EventSourcingActivitySource.StartLoadActivity(
			"nested-test",
			"Order");

		using var snapshotActivity = EventSourcingActivitySource.StartGetSnapshotActivity(
			"nested-test",
			"Order");

		// Assert - All activities should share the same trace
		_ = loadActivity.ShouldNotBeNull();
		_ = snapshotActivity.ShouldNotBeNull();

		loadActivity.TraceId.ShouldBe(outerActivity.TraceId);
		snapshotActivity.TraceId.ShouldBe(outerActivity.TraceId);
	}

	#endregion Parent-Child Span Relationship Tests

	#region Activity Source Verification Tests

	[Fact]
	public void UseCorrectActivitySourceName()
	{
		// Assert
		EventSourcingActivitySource.Name.ShouldBe("Excalibur.EventSourcing");
		EventSourcingActivitySource.Instance.Name.ShouldBe("Excalibur.EventSourcing");
	}

	[Fact]
	public void UseConsistentActivityNames()
	{
		// Verify all activity names follow the pattern
		EventSourcingActivities.Append.ShouldBe("EventStore.Append");
		EventSourcingActivities.Load.ShouldBe("EventStore.Load");
		EventSourcingActivities.GetUndispatched.ShouldBe("EventStore.GetUndispatched");
		EventSourcingActivities.MarkDispatched.ShouldBe("EventStore.MarkDispatched");
		EventSourcingActivities.SaveSnapshot.ShouldBe("SnapshotStore.Save");
		EventSourcingActivities.GetSnapshot.ShouldBe("SnapshotStore.Get");
		EventSourcingActivities.DeleteSnapshots.ShouldBe("SnapshotStore.Delete");
	}

	[Fact]
	public void UseSemanticTagNames()
	{
		// Verify tag names follow OpenTelemetry semantic conventions
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

	#endregion Activity Source Verification Tests

	#region Recorded Activity Verification Tests

	[Fact]
	public void RecordCompletedActivitiesInFixture()
	{
		// Arrange
		_fixture.ClearRecordedData();

		// Act
		using (var activity = EventSourcingActivitySource.StartAppendActivity(
			"recorded-test",
			"TestAggregate",
			2,
			0))
		{
			activity.SetOperationResult(EventSourcingTagValues.Success);
		}

		// Assert
		var recordedActivities = _fixture.GetRecordedActivities();
		var appendActivity = recordedActivities
			.FirstOrDefault(a => a.OperationName == EventSourcingActivities.Append);

		_ = appendActivity.ShouldNotBeNull();
		appendActivity.GetTagItem(EventSourcingTags.AggregateId).ShouldBe("recorded-test");
		appendActivity.GetTagItem(EventSourcingTags.OperationResult).ShouldBe(EventSourcingTagValues.Success);
	}

	[Fact]
	public void RecordMultipleActivitiesInSequence()
	{
		// Arrange
		_fixture.ClearRecordedData();

		// Act
		using (var loadActivity = EventSourcingActivitySource.StartLoadActivity(
			"sequence-test",
			"Order"))
		{
			loadActivity.SetOperationResult(EventSourcingTagValues.Success);
		}

		using (var appendActivity = EventSourcingActivitySource.StartAppendActivity(
			"sequence-test",
			"Order",
			1,
			5))
		{
			appendActivity.SetOperationResult(EventSourcingTagValues.Success);
		}

		using (var snapshotActivity = EventSourcingActivitySource.StartSaveSnapshotActivity(
			"sequence-test",
			"Order",
			6))
		{
			snapshotActivity.SetOperationResult(EventSourcingTagValues.Success);
		}

		// Assert
		var recordedActivities = _fixture.GetRecordedActivities()
			.Where(a => a.Source.Name == EventSourcingActivitySource.Name)
			.ToList();

		recordedActivities.Count.ShouldBe(3);
		recordedActivities.ShouldContain(a => a.OperationName == EventSourcingActivities.Load);
		recordedActivities.ShouldContain(a => a.OperationName == EventSourcingActivities.Append);
		recordedActivities.ShouldContain(a => a.OperationName == EventSourcingActivities.SaveSnapshot);
	}

	#endregion Recorded Activity Verification Tests

	public void Dispose()
	{
		_fixture?.Dispose();
	}
}
