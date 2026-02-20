// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.EventSourcing.Observability;

using Shouldly;

using Xunit;

namespace Excalibur.EventSourcing.Tests.Observability;

/// <summary>
/// Unit tests for <see cref="EventSourcingActivitySource"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class EventSourcingActivitySourceShould : IDisposable
{
	private readonly ActivityListener _listener;
	private readonly List<Activity> _capturedActivities = new();

	public EventSourcingActivitySourceShould()
	{
		_listener = new ActivityListener
		{
			ShouldListenTo = source => source.Name == EventSourcingActivitySource.Name,
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
			ActivityStarted = activity => _capturedActivities.Add(activity)
		};
		ActivitySource.AddActivityListener(_listener);
	}

	public void Dispose()
	{
		_listener.Dispose();
		foreach (var activity in _capturedActivities)
		{
			activity.Dispose();
		}
	}

	#region Name and Instance Tests

	[Fact]
	public void HaveCorrectName()
	{
		EventSourcingActivitySource.Name.ShouldBe("Excalibur.EventSourcing");
	}

	[Fact]
	public void HaveNonNullInstance()
	{
		EventSourcingActivitySource.Instance.ShouldNotBeNull();
		EventSourcingActivitySource.Instance.Name.ShouldBe("Excalibur.EventSourcing");
	}

	#endregion

	#region StartAppendActivity Tests

	[Fact]
	public void StartAppendActivity_ShouldCreateActivityWithCorrectTags()
	{
		// Act
		using var activity = EventSourcingActivitySource.StartAppendActivity("agg-1", "Order", 3, 5);

		// Assert
		activity.ShouldNotBeNull();
		activity.OperationName.ShouldBe(EventSourcingActivities.Append);
		activity.GetTagItem(EventSourcingTags.AggregateId).ToString().ShouldBe("agg-1");
		activity.GetTagItem(EventSourcingTags.AggregateType).ToString().ShouldBe("Order");
		activity.GetTagItem(EventSourcingTags.EventCount).ShouldBe(3);
		activity.GetTagItem(EventSourcingTags.ExpectedVersion).ShouldBe(5L);
	}

	#endregion

	#region StartLoadActivity Tests

	[Fact]
	public void StartLoadActivity_ShouldCreateActivityWithCorrectTags()
	{
		// Act
		using var activity = EventSourcingActivitySource.StartLoadActivity("agg-2", "Customer");

		// Assert
		activity.ShouldNotBeNull();
		activity.OperationName.ShouldBe(EventSourcingActivities.Load);
		activity.GetTagItem(EventSourcingTags.AggregateId).ToString().ShouldBe("agg-2");
		activity.GetTagItem(EventSourcingTags.AggregateType).ToString().ShouldBe("Customer");
		// Default fromVersion is -1, so FromVersion tag should not be set
		activity.GetTagItem(EventSourcingTags.FromVersion).ShouldBeNull();
	}

	[Fact]
	public void StartLoadActivity_ShouldSetFromVersion_WhenNonNegative()
	{
		// Act
		using var activity = EventSourcingActivitySource.StartLoadActivity("agg-3", "Product", 10);

		// Assert
		activity.ShouldNotBeNull();
		activity.GetTagItem(EventSourcingTags.FromVersion).ShouldBe(10L);
	}

	[Fact]
	public void StartLoadActivity_ShouldNotSetFromVersion_WhenNegative()
	{
		// Act
		using var activity = EventSourcingActivitySource.StartLoadActivity("agg-4", "Invoice", -1);

		// Assert
		activity.ShouldNotBeNull();
		activity.GetTagItem(EventSourcingTags.FromVersion).ShouldBeNull();
	}

	#endregion

	#region StartGetUndispatchedActivity Tests

	[Fact]
	public void StartGetUndispatchedActivity_ShouldCreateActivityWithBatchSize()
	{
		// Act
		using var activity = EventSourcingActivitySource.StartGetUndispatchedActivity(25);

		// Assert
		activity.ShouldNotBeNull();
		activity.OperationName.ShouldBe(EventSourcingActivities.GetUndispatched);
		activity.GetTagItem(EventSourcingTags.BatchSize).ShouldBe(25);
	}

	#endregion

	#region StartMarkDispatchedActivity Tests

	[Fact]
	public void StartMarkDispatchedActivity_ShouldCreateActivityWithEventId()
	{
		// Act
		using var activity = EventSourcingActivitySource.StartMarkDispatchedActivity("evt-42");

		// Assert
		activity.ShouldNotBeNull();
		activity.OperationName.ShouldBe(EventSourcingActivities.MarkDispatched);
		activity.GetTagItem(EventSourcingTags.EventId).ToString().ShouldBe("evt-42");
	}

	#endregion

	#region StartSaveSnapshotActivity Tests

	[Fact]
	public void StartSaveSnapshotActivity_ShouldCreateActivityWithCorrectTags()
	{
		// Act
		using var activity = EventSourcingActivitySource.StartSaveSnapshotActivity("agg-5", "Basket", 100);

		// Assert
		activity.ShouldNotBeNull();
		activity.OperationName.ShouldBe(EventSourcingActivities.SaveSnapshot);
		activity.GetTagItem(EventSourcingTags.AggregateId).ToString().ShouldBe("agg-5");
		activity.GetTagItem(EventSourcingTags.AggregateType).ToString().ShouldBe("Basket");
		activity.GetTagItem(EventSourcingTags.Version).ShouldBe(100L);
	}

	#endregion

	#region StartGetSnapshotActivity Tests

	[Fact]
	public void StartGetSnapshotActivity_ShouldCreateActivityWithCorrectTags()
	{
		// Act
		using var activity = EventSourcingActivitySource.StartGetSnapshotActivity("agg-6", "Shipment");

		// Assert
		activity.ShouldNotBeNull();
		activity.OperationName.ShouldBe(EventSourcingActivities.GetSnapshot);
		activity.GetTagItem(EventSourcingTags.AggregateId).ToString().ShouldBe("agg-6");
		activity.GetTagItem(EventSourcingTags.AggregateType).ToString().ShouldBe("Shipment");
	}

	#endregion

	#region StartDeleteSnapshotsActivity Tests

	[Fact]
	public void StartDeleteSnapshotsActivity_ShouldCreateActivityWithCorrectTags()
	{
		// Act
		using var activity = EventSourcingActivitySource.StartDeleteSnapshotsActivity("agg-7", "Payment");

		// Assert
		activity.ShouldNotBeNull();
		activity.OperationName.ShouldBe(EventSourcingActivities.DeleteSnapshots);
		activity.GetTagItem(EventSourcingTags.AggregateId).ToString().ShouldBe("agg-7");
		activity.GetTagItem(EventSourcingTags.AggregateType).ToString().ShouldBe("Payment");
	}

	#endregion

	#region RecordException Tests

	[Fact]
	public void RecordException_ShouldSetErrorStatusAndTags()
	{
		// Arrange
		using var activity = EventSourcingActivitySource.StartAppendActivity("agg-8", "Test", 1, 0);
		activity.ShouldNotBeNull();

		var exception = new InvalidOperationException("Test error message");

		// Act
		activity.RecordException(exception);

		// Assert
		activity.Status.ShouldBe(ActivityStatusCode.Error);
		activity.StatusDescription.ShouldBe("Test error message");
		activity.GetTagItem(EventSourcingTags.ExceptionType).ShouldBe(typeof(InvalidOperationException).FullName);
		activity.GetTagItem(EventSourcingTags.ExceptionMessage).ToString().ShouldBe("Test error message");
	}

	[Fact]
	public void RecordException_ShouldBeNoOp_WhenActivityIsNull()
	{
		// Arrange
		Activity? nullActivity = null;
		var exception = new InvalidOperationException("Test");

		// Act & Assert - should not throw
		nullActivity.RecordException(exception);
	}

	#endregion

	#region SetOperationResult Tests

	[Fact]
	public void SetOperationResult_ShouldSetResultTag()
	{
		// Arrange
		using var activity = EventSourcingActivitySource.StartAppendActivity("agg-9", "Test", 1, 0);
		activity.ShouldNotBeNull();

		// Act
		activity.SetOperationResult("success");

		// Assert
		activity.GetTagItem(EventSourcingTags.OperationResult).ToString().ShouldBe("success");
	}

	[Fact]
	public void SetOperationResult_ShouldBeNoOp_WhenActivityIsNull()
	{
		// Arrange
		Activity? nullActivity = null;

		// Act & Assert - should not throw
		nullActivity.SetOperationResult("success");
	}

	#endregion
}
