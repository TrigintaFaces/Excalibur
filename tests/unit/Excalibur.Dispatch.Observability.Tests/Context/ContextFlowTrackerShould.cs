// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Observability.Context;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Observability.Tests.Context;

/// <summary>
/// Unit tests for <see cref="ContextFlowTracker"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Context")]
public sealed class ContextFlowTrackerShould : IAsyncDisposable
{
	private ContextFlowTracker? _tracker;

	public async ValueTask DisposeAsync()
	{
		if (_tracker != null)
		{
			await _tracker.DisposeAsync();
		}
	}

	[Fact]
	public void ThrowOnNullLogger()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ContextFlowTracker(
				null!,
				MsOptions.Create(new ContextObservabilityOptions()),
				A.Fake<IContextFlowMetrics>()));
	}

	[Fact]
	public void ThrowOnNullOptions()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ContextFlowTracker(
				NullLogger<ContextFlowTracker>.Instance,
				null!,
				A.Fake<IContextFlowMetrics>()));
	}

	[Fact]
	public void ThrowOnNullMetrics()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ContextFlowTracker(
				NullLogger<ContextFlowTracker>.Instance,
				MsOptions.Create(new ContextObservabilityOptions()),
				null!));
	}

	[Fact]
	public void ValidateContextIntegrity_WithAllRequiredFields()
	{
		// Arrange
		_tracker = CreateTracker();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.MessageId).Returns("msg-1");
		A.CallTo(() => context.CorrelationId).Returns("corr-1");
		A.CallTo(() => context.MessageType).Returns("TestMessage");

		// Act
		var result = _tracker.ValidateContextIntegrity(context);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ValidateContextIntegrity_FailsOnMissingField()
	{
		// Arrange
		_tracker = CreateTracker();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.MessageId).Returns((string?)null);
		A.CallTo(() => context.CorrelationId).Returns("corr-1");
		A.CallTo(() => context.MessageType).Returns("TestMessage");

		// Act
		var result = _tracker.ValidateContextIntegrity(context);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ValidateContextIntegrity_ThrowsOnNull()
	{
		_tracker = CreateTracker();
		Should.Throw<ArgumentNullException>(() => _tracker.ValidateContextIntegrity(null!));
	}

	[Fact]
	public void CorrelateAcrossBoundary_RecordsBoundary()
	{
		// Arrange
		var fakeMetrics = A.Fake<IContextFlowMetrics>();
		_tracker = CreateTracker(metrics: fakeMetrics);
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.CorrelationId).Returns("corr-1");
		A.CallTo(() => context.MessageId).Returns("msg-1");

		// Act
		_tracker.CorrelateAcrossBoundary(context, "order-service");

		// Assert
		A.CallTo(() => fakeMetrics.RecordCrossBoundaryTransition("order-service", true))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void CorrelateAcrossBoundary_ThrowsOnNullContext()
	{
		_tracker = CreateTracker();
		Should.Throw<ArgumentNullException>(() =>
			_tracker.CorrelateAcrossBoundary(null!, "service"));
	}

	[Fact]
	public void CorrelateAcrossBoundary_ThrowsOnNullBoundary()
	{
		_tracker = CreateTracker();
		Should.Throw<ArgumentNullException>(() =>
			_tracker.CorrelateAcrossBoundary(A.Fake<IMessageContext>(), null!));
	}

	[Fact]
	public void GetContextLineage_ReturnsNull_WhenNotTracked()
	{
		_tracker = CreateTracker();
		var result = _tracker.GetContextLineage("nonexistent");
		result.ShouldBeNull();
	}

	[Fact]
	public void GetContextLineage_ReturnsLineage_AfterCorrelation()
	{
		// Arrange
		_tracker = CreateTracker();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.CorrelationId).Returns("corr-1");
		A.CallTo(() => context.MessageId).Returns("msg-1");

		// Act
		_tracker.CorrelateAcrossBoundary(context, "order-service");
		var lineage = _tracker.GetContextLineage("corr-1");

		// Assert
		lineage.ShouldNotBeNull();
		lineage.CorrelationId.ShouldBe("corr-1");
		lineage.ServiceBoundaries.Count.ShouldBe(1);
	}

	[Fact]
	public void GetMessageSnapshots_ReturnsEmpty_WhenNone()
	{
		_tracker = CreateTracker();
		var snapshots = _tracker.GetMessageSnapshots("nonexistent");
		snapshots.ShouldBeEmpty();
	}

	[Fact]
	public void ImplementIContextFlowTracker()
	{
		_tracker = CreateTracker();
		_tracker.ShouldBeAssignableTo<IContextFlowTracker>();
	}

	[Fact]
	public void ImplementIDisposable()
	{
		_tracker = CreateTracker();
		_tracker.ShouldBeAssignableTo<IDisposable>();
	}

	[Fact]
	public void ImplementIAsyncDisposable()
	{
		_tracker = CreateTracker();
		_tracker.ShouldBeAssignableTo<IAsyncDisposable>();
	}

	[Fact]
	public void DisposeWithoutError()
	{
		var tracker = CreateTracker();
		tracker.Dispose();
		// Second dispose should also not throw
		tracker.Dispose();
		_tracker = null; // Prevent async dispose in teardown
	}

	private static ContextFlowTracker CreateTracker(IContextFlowMetrics? metrics = null)
	{
		return new ContextFlowTracker(
			NullLogger<ContextFlowTracker>.Instance,
			MsOptions.Create(new ContextObservabilityOptions()),
			metrics ?? A.Fake<IContextFlowMetrics>());
	}
}
