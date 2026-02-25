// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Observability.Context;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Observability.Tests.Context;

/// <summary>
/// Deep coverage tests for <see cref="ContextFlowTracker"/> covering RecordContextState,
/// DetectChanges, CorrelateAcrossBoundary, ValidateContextIntegrity, disposal paths,
/// and cleanup timer behavior.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
public sealed class ContextFlowTrackerDepthCoverageShould : IAsyncDisposable
{
	private readonly IContextFlowMetrics _metrics = A.Fake<IContextFlowMetrics>();
	private readonly ContextObservabilityOptions _options;
	private readonly ContextFlowTracker _sut;

	public ContextFlowTrackerDepthCoverageShould()
	{
		_options = new ContextObservabilityOptions
		{
			CaptureCustomItems = true,
		};
		_options.Fields.RequiredContextFields = ["MessageId", "CorrelationId", "MessageType"];
		_options.Limits.MaxContextSizeBytes = 100_000;
		_options.Limits.MaxSnapshotsPerLineage = 5;
		_options.Limits.MaxCustomItemsToCapture = 10;
		_options.Limits.SnapshotRetentionPeriod = TimeSpan.FromHours(1);

		var optionsWrapper = Microsoft.Extensions.Options.Options.Create(_options);
		_sut = new ContextFlowTracker(
			NullLogger<ContextFlowTracker>.Instance,
			optionsWrapper,
			_metrics);
	}

	public async ValueTask DisposeAsync() => await _sut.DisposeAsync();

	[Fact]
	public void RecordContextState_HandleNullContext()
	{
		// Act - should not throw, just log a warning
		_sut.RecordContextState(null!, "TestStage");

		// Assert - no exception thrown, metrics not called for snapshot
		A.CallTo(() => _metrics.RecordContextSnapshot(A<string>._, A<int>._, A<int>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public void RecordContextState_CaptureSnapshotAndMetrics()
	{
		// Arrange
		var context = CreateValidContext("msg-rec-1", "corr-rec-1");

		// Act
		_sut.RecordContextState(context, "PreProcessing");

		// Assert
		A.CallTo(() => _metrics.RecordContextSnapshot("PreProcessing", A<int>._, A<int>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void RecordContextState_CaptureCustomItems()
	{
		// Arrange
		var items = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["custom1"] = "value1",
			["custom2"] = 42,
		};
		var context = CreateValidContext("msg-custom", "corr-custom", items);

		// Act
		_sut.RecordContextState(context, "WithItems");

		// Assert
		A.CallTo(() => _metrics.RecordContextSnapshot("WithItems", A<int>._, A<int>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void RecordContextState_TrackLineageForSameCorrelation()
	{
		// Arrange
		var context = CreateValidContext("msg-lin-1", "corr-lineage");

		// Act
		_sut.RecordContextState(context, "Stage1");
		_sut.RecordContextState(context, "Stage2");

		// Assert
		var lineage = _sut.GetContextLineage("corr-lineage");
		lineage.ShouldNotBeNull();
		lineage.Snapshots.Count.ShouldBe(2);
	}

	[Fact]
	public void RecordContextState_TrimLineageWhenExceedsLimit()
	{
		// Arrange
		var context = CreateValidContext("msg-lin-trim", "corr-trim");

		// Act - record more snapshots than MaxSnapshotsPerLineage (5)
		for (var i = 0; i < 8; i++)
		{
			_sut.RecordContextState(context, $"Stage-{i}");
		}

		// Assert
		var lineage = _sut.GetContextLineage("corr-trim");
		lineage.ShouldNotBeNull();
		lineage.Snapshots.Count.ShouldBeLessThanOrEqualTo(5);
	}

	[Fact]
	public void CorrelateAcrossBoundary_RecordServiceBoundary()
	{
		// Arrange
		var context = CreateValidContext("msg-boundary", "corr-boundary");

		// Act
		_sut.CorrelateAcrossBoundary(context, "OrderService");

		// Assert
		var lineage = _sut.GetContextLineage("corr-boundary");
		lineage.ShouldNotBeNull();
		lineage.ServiceBoundaries.Count.ShouldBe(1);
		lineage.ServiceBoundaries[0].ServiceName.ShouldBe("OrderService");
		lineage.ServiceBoundaries[0].ContextPreserved.ShouldBeTrue();

		A.CallTo(() => _metrics.RecordCrossBoundaryTransition("OrderService", true))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void CorrelateAcrossBoundary_ThrowOnNullContext()
	{
		Should.Throw<ArgumentNullException>(() =>
			_sut.CorrelateAcrossBoundary(null!, "service"));
	}

	[Fact]
	public void CorrelateAcrossBoundary_ThrowOnNullServiceBoundary()
	{
		var context = CreateValidContext("msg-1", "corr-1");
		Should.Throw<ArgumentNullException>(() =>
			_sut.CorrelateAcrossBoundary(context, null!));
	}

	[Fact]
	public void CorrelateAcrossBoundary_UseFallbackWhenNoCorrelationId()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.CorrelationId).Returns(null);
		A.CallTo(() => context.MessageId).Returns("msg-nocorr");

		// Act
		_sut.CorrelateAcrossBoundary(context, "TestService");

		// Assert - uses MessageId as fallback
		var lineage = _sut.GetContextLineage("msg-nocorr");
		lineage.ShouldNotBeNull();
	}

	[Fact]
	public void DetectChanges_ThrowOnNullContext()
	{
		Should.Throw<ArgumentNullException>(() =>
			_sut.DetectChanges(null!, "from", "to"));
	}

	[Fact]
	public void DetectChanges_ThrowOnNullFromStage()
	{
		var context = CreateValidContext("msg-1", "corr-1");
		Should.Throw<ArgumentNullException>(() =>
			_sut.DetectChanges(context, null!, "to"));
	}

	[Fact]
	public void DetectChanges_ThrowOnNullToStage()
	{
		var context = CreateValidContext("msg-1", "corr-1");
		Should.Throw<ArgumentNullException>(() =>
			_sut.DetectChanges(context, "from", null!));
	}

	[Fact]
	public void ValidateContextIntegrity_ReturnTrue_WhenAllRequiredFieldsPresent()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.MessageId).Returns("msg-valid");
		A.CallTo(() => context.CorrelationId).Returns("corr-valid");
		A.CallTo(() => context.MessageType).Returns("TestCommand");

		// Act
		var result = _sut.ValidateContextIntegrity(context);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ValidateContextIntegrity_ReturnFalse_WhenRequiredFieldMissing()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.MessageId).Returns("msg-1");
		A.CallTo(() => context.CorrelationId).Returns(null); // missing
		A.CallTo(() => context.MessageType).Returns("TestCommand");

		// Act
		var result = _sut.ValidateContextIntegrity(context);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ValidateContextIntegrity_ReturnFalse_WhenFieldIsEmptyString()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.MessageId).Returns("msg-1");
		A.CallTo(() => context.CorrelationId).Returns("corr-1");
		A.CallTo(() => context.MessageType).Returns("   "); // whitespace

		// Act
		var result = _sut.ValidateContextIntegrity(context);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ValidateContextIntegrity_ThrowOnNullContext()
	{
		Should.Throw<ArgumentNullException>(() =>
			_sut.ValidateContextIntegrity(null!));
	}

	[Fact]
	public void ValidateContextIntegrity_CheckCustomItemsForUnknownFields()
	{
		// Arrange - configure a custom required field
		_options.Fields.RequiredContextFields = ["MessageId", "CustomField"];
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.MessageId).Returns("msg-1");
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["CustomField"] = "has-value",
		});

		// Act
		var result = _sut.ValidateContextIntegrity(context);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void GetContextLineage_ReturnNull_WhenNotFound()
	{
		var result = _sut.GetContextLineage("nonexistent-corr");
		result.ShouldBeNull();
	}

	[Fact]
	public void GetMessageSnapshots_ReturnEmpty_WhenNoSnapshots()
	{
		var result = _sut.GetMessageSnapshots("nonexistent-msg").ToList();
		result.ShouldBeEmpty();
	}

	[Fact]
	public void GetMessageSnapshots_ReturnOrderedByTimestamp()
	{
		// Arrange
		var context = CreateValidContext("msg-snap-order", "corr-snap");

		_sut.RecordContextState(context, "Stage1");
		_sut.RecordContextState(context, "Stage2");
		_sut.RecordContextState(context, "Stage3");

		// Act
		var snapshots = _sut.GetMessageSnapshots("msg-snap-order").ToList();

		// Assert
		snapshots.Count.ShouldBe(3);
		for (var i = 1; i < snapshots.Count; i++)
		{
			snapshots[i].Timestamp.ShouldBeGreaterThanOrEqualTo(snapshots[i - 1].Timestamp);
		}
	}

	[Fact]
	public void Dispose_CanBeCalledMultipleTimes()
	{
		// Arrange
		var tracker = new ContextFlowTracker(
			NullLogger<ContextFlowTracker>.Instance,
			Microsoft.Extensions.Options.Options.Create(new ContextObservabilityOptions()),
			_metrics);

		// Act & Assert - no exception
		tracker.Dispose();
		tracker.Dispose();
	}

	[Fact]
	public async Task DisposeAsync_CanBeCalledMultipleTimes()
	{
		// Arrange
		var tracker = new ContextFlowTracker(
			NullLogger<ContextFlowTracker>.Instance,
			Microsoft.Extensions.Options.Options.Create(new ContextObservabilityOptions()),
			_metrics);

		// Act & Assert - no exception
		await tracker.DisposeAsync();
		await tracker.DisposeAsync();
	}

	private static IMessageContext CreateValidContext(string messageId, string correlationId, Dictionary<string, object>? items = null)
	{
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.MessageId).Returns(messageId);
		A.CallTo(() => context.CorrelationId).Returns(correlationId);
		A.CallTo(() => context.MessageType).Returns("TestCommand");
		A.CallTo(() => context.Items).Returns(items ?? new Dictionary<string, object>(StringComparer.Ordinal));
		A.CallTo(() => context.Source).Returns("TestSource");
		A.CallTo(() => context.ContentType).Returns("application/json");
		A.CallTo(() => context.TenantId).Returns("tenant-1");
		A.CallTo(() => context.UserId).Returns("user-1");
		A.CallTo(() => context.ExternalId).Returns("ext-1");
		A.CallTo(() => context.CausationId).Returns("cause-1");
		A.CallTo(() => context.TraceParent).Returns("00-trace-1");
		A.CallTo(() => context.DeliveryCount).Returns(1);
		return context;
	}
}
