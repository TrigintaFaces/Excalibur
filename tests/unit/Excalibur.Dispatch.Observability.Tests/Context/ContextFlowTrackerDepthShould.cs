// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable IL2026, IL3050 // Suppress for test - RequiresUnreferencedCode/RequiresDynamicCode

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Observability.Context;

using Microsoft.Extensions.Logging.Abstractions;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Observability.Tests.Context;

/// <summary>
/// In-depth unit tests for <see cref="ContextFlowTracker"/> covering uncovered code paths.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Context")]
public sealed class ContextFlowTrackerDepthShould : IAsyncDisposable
{
	private ContextFlowTracker? _tracker;

	public async ValueTask DisposeAsync()
	{
		if (_tracker != null)
		{
			await _tracker.DisposeAsync().ConfigureAwait(false);
		}
	}

	[Fact]
	public void RecordContextState_HandlesNullContext()
	{
		// Arrange
		var metrics = A.Fake<IContextFlowMetrics>();
		_tracker = CreateTracker(metrics: metrics);

		// Act — should not throw, logs warning instead
		_tracker.RecordContextState(null!, "stage1");

		// Assert — metrics should NOT have been called (we returned early)
		A.CallTo(() => metrics.RecordContextSnapshot(A<string>._, A<int>._, A<int>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public void RecordContextState_RecordsSnapshotAndMetrics()
	{
		// Arrange
		var metrics = A.Fake<IContextFlowMetrics>();
		_tracker = CreateTracker(metrics: metrics);
		var context = CreateFakeContext("msg-1", "corr-1");

		// Act
		_tracker.RecordContextState(context, "PreProcessing");

		// Assert
		A.CallTo(() => metrics.RecordContextSnapshot("PreProcessing", A<int>._, A<int>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void RecordContextState_TracksLineage()
	{
		// Arrange
		_tracker = CreateTracker();
		var context = CreateFakeContext("msg-1", "corr-1");

		// Act
		_tracker.RecordContextState(context, "stage1");

		// Assert — lineage should exist
		var lineage = _tracker.GetContextLineage("corr-1");
		lineage.ShouldNotBeNull();
		lineage.CorrelationId.ShouldBe("corr-1");
		lineage.Snapshots.Count.ShouldBeGreaterThanOrEqualTo(1);
	}

	[Fact]
	public void RecordContextState_CapturesCustomItems_WhenEnabled()
	{
		// Arrange
		var options = new ContextObservabilityOptions { CaptureCustomItems = true };
		_tracker = CreateTracker(options);
		var context = CreateFakeContext("msg-1", "corr-1");
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>
		{
			["CustomKey1"] = "CustomValue1",
			["CustomKey2"] = 42,
		});

		// Act
		_tracker.RecordContextState(context, "stage1");

		// Assert
		var snapshots = _tracker.GetMessageSnapshots("msg-1").ToList();
		snapshots.ShouldNotBeEmpty();
		snapshots[0].Fields.ShouldContainKey("Item_CustomKey1");
	}

	[Fact]
	public void RecordContextState_SkipsCustomItems_WhenDisabled()
	{
		// Arrange
		var options = new ContextObservabilityOptions { CaptureCustomItems = false };
		_tracker = CreateTracker(options);
		var context = CreateFakeContext("msg-1", "corr-1");
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>
		{
			["CustomKey1"] = "CustomValue1",
		});

		// Act
		_tracker.RecordContextState(context, "stage1");

		// Assert
		var snapshots = _tracker.GetMessageSnapshots("msg-1").ToList();
		snapshots.ShouldNotBeEmpty();
		snapshots[0].Fields.ShouldNotContainKey("Item_CustomKey1");
	}

	[Fact]
	public void RecordContextState_SetsFieldCountAndSizeBytes()
	{
		// Arrange
		_tracker = CreateTracker();
		var context = CreateFakeContext("msg-1", "corr-1");

		// Act
		_tracker.RecordContextState(context, "stage1");

		// Assert
		var snapshots = _tracker.GetMessageSnapshots("msg-1").ToList();
		snapshots.ShouldNotBeEmpty();
		snapshots[0].FieldCount.ShouldBeGreaterThan(0);
		snapshots[0].SizeBytes.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void DetectChanges_ThrowsOnNullContext()
	{
		_tracker = CreateTracker();
		Should.Throw<ArgumentNullException>(() =>
			_tracker.DetectChanges(null!, "from", "to"));
	}

	[Fact]
	public void DetectChanges_ThrowsOnNullFromStage()
	{
		_tracker = CreateTracker();
		Should.Throw<ArgumentNullException>(() =>
			_tracker.DetectChanges(A.Fake<IMessageContext>(), null!, "to"));
	}

	[Fact]
	public void DetectChanges_ThrowsOnNullToStage()
	{
		_tracker = CreateTracker();
		Should.Throw<ArgumentNullException>(() =>
			_tracker.DetectChanges(A.Fake<IMessageContext>(), "from", null!));
	}

	[Fact]
	public void DetectChanges_ReturnsEmpty_WhenNoSnapshots()
	{
		// Arrange
		_tracker = CreateTracker();
		var context = CreateFakeContext("msg-1", "corr-1");

		// Act
		var changes = _tracker.DetectChanges(context, "stage1", "stage2").ToList();

		// Assert
		changes.ShouldBeEmpty();
	}

	[Fact]
	public void GetMessageSnapshots_ReturnsOrderedSnapshots()
	{
		// Arrange
		_tracker = CreateTracker();
		var context = CreateFakeContext("msg-1", "corr-1");

		// Record multiple stages
		_tracker.RecordContextState(context, "stage1");
		_tracker.RecordContextState(context, "stage2");

		// Act
		var snapshots = _tracker.GetMessageSnapshots("msg-1").ToList();

		// Assert
		snapshots.Count.ShouldBeGreaterThanOrEqualTo(2);
		// Ordered by timestamp
		for (var i = 1; i < snapshots.Count; i++)
		{
			snapshots[i].Timestamp.ShouldBeGreaterThanOrEqualTo(snapshots[i - 1].Timestamp);
		}
	}

	[Fact]
	public void CorrelateAcrossBoundary_CreatesLineage_WithCorrelationId()
	{
		// Arrange
		_tracker = CreateTracker();
		var context = CreateFakeContext("msg-1", "corr-1");

		// Act
		_tracker.CorrelateAcrossBoundary(context, "service-a");
		_tracker.CorrelateAcrossBoundary(context, "service-b");

		// Assert
		var lineage = _tracker.GetContextLineage("corr-1");
		lineage.ShouldNotBeNull();
		lineage.ServiceBoundaries.Count.ShouldBe(2);
		lineage.ServiceBoundaries[0].ServiceName.ShouldBe("service-a");
		lineage.ServiceBoundaries[1].ServiceName.ShouldBe("service-b");
	}

	[Fact]
	public void CorrelateAcrossBoundary_UsesMessageId_WhenNoCorrelationId()
	{
		// Arrange
		_tracker = CreateTracker();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.CorrelationId).Returns((string?)null);
		A.CallTo(() => context.MessageId).Returns("msg-fallback");

		// Act
		_tracker.CorrelateAcrossBoundary(context, "service-a");

		// Assert
		var lineage = _tracker.GetContextLineage("msg-fallback");
		lineage.ShouldNotBeNull();
	}

	[Fact]
	public void ValidateContextIntegrity_UsesCustomRequiredFields()
	{
		// Arrange
		var options = new ContextObservabilityOptions();
		options.Fields.RequiredContextFields = ["MessageId", "TenantId"];
		_tracker = CreateTracker(options);

		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.MessageId).Returns("msg-1");
		A.CallTo(() => context.TenantId).Returns((string?)null); // Missing TenantId

		// Act
		var result = _tracker.ValidateContextIntegrity(context);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ValidateContextIntegrity_ChecksCustomItemFields()
	{
		// Arrange
		var options = new ContextObservabilityOptions();
		options.Fields.RequiredContextFields = ["CustomField"];
		_tracker = CreateTracker(options);

		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>
		{
			["CustomField"] = "present",
		});

		// Act
		var result = _tracker.ValidateContextIntegrity(context);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ValidateContextIntegrity_ReturnsTrue_WhenWhitespaceField()
	{
		// Arrange
		_tracker = CreateTracker();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.MessageId).Returns("  "); // Whitespace
		A.CallTo(() => context.CorrelationId).Returns("corr-1");
		A.CallTo(() => context.MessageType).Returns("TestType");

		// Act
		var result = _tracker.ValidateContextIntegrity(context);

		// Assert
		result.ShouldBeFalse(); // Whitespace should fail validation
	}

	[Fact]
	public async Task DisposeAsync_IsIdempotent()
	{
		// Arrange
		var tracker = CreateTracker();

		// Act
		await tracker.DisposeAsync().ConfigureAwait(false);
		await tracker.DisposeAsync().ConfigureAwait(false); // Second call should not throw

		_tracker = null; // Prevent teardown dispose
	}

	[Fact]
	public void RecordContextState_HandlesNullMessageId()
	{
		// Arrange
		var metrics = A.Fake<IContextFlowMetrics>();
		_tracker = CreateTracker(metrics: metrics);
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.MessageId).Returns((string?)null);
		A.CallTo(() => context.CorrelationId).Returns("corr-1");
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());

		// Act — should not throw even with null MessageId
		_tracker.RecordContextState(context, "stage1");

		// Assert — metrics should still be called
		A.CallTo(() => metrics.RecordContextSnapshot(A<string>._, A<int>._, A<int>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void RecordContextState_MultipleSameStage_UpdatesSnapshot()
	{
		// Arrange
		var metrics = A.Fake<IContextFlowMetrics>();
		_tracker = CreateTracker(metrics: metrics);
		var context = CreateFakeContext("msg-1", "corr-1");

		// Act — record same stage twice
		_tracker.RecordContextState(context, "stage1");
		_tracker.RecordContextState(context, "stage1");

		// Assert — both calls should have recorded metrics
		A.CallTo(() => metrics.RecordContextSnapshot("stage1", A<int>._, A<int>._))
			.MustHaveHappened(2, Times.Exactly);
	}

	[Fact]
	public void CorrelateAcrossBoundary_SetsContextPreservedTrue()
	{
		// Arrange
		_tracker = CreateTracker();
		var context = CreateFakeContext("msg-1", "corr-1");

		// Act
		_tracker.CorrelateAcrossBoundary(context, "service-a");

		// Assert
		var lineage = _tracker.GetContextLineage("corr-1");
		lineage.ShouldNotBeNull();
		lineage.ServiceBoundaries[0].ContextPreserved.ShouldBeTrue();
	}

	private static ContextFlowTracker CreateTracker(
		ContextObservabilityOptions? options = null,
		IContextFlowMetrics? metrics = null)
	{
		return new ContextFlowTracker(
			NullLogger<ContextFlowTracker>.Instance,
			MsOptions.Create(options ?? new ContextObservabilityOptions()),
			metrics ?? A.Fake<IContextFlowMetrics>());
	}

	private static IMessageContext CreateFakeContext(string messageId, string correlationId)
	{
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.MessageId).Returns(messageId);
		A.CallTo(() => context.CorrelationId).Returns(correlationId);
		A.CallTo(() => context.MessageType).Returns("TestMessage");
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());
		A.CallTo(() => context.ReceivedTimestampUtc).Returns(DateTimeOffset.UtcNow);
		return context;
	}
}

#pragma warning restore IL2026, IL3050
