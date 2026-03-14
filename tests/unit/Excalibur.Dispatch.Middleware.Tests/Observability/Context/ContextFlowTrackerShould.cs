// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Observability.Context;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Middleware.Tests.Observability.Context;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class ContextFlowTrackerShould : IAsyncDisposable
{
	private readonly IContextFlowMetrics _fakeMetrics;
	private readonly IMessageContext _fakeContext;
	private readonly ContextObservabilityOptions _options;
	private readonly ContextFlowTracker _sut;

	public ContextFlowTrackerShould()
	{
		_fakeMetrics = A.Fake<IContextFlowMetrics>();
		_fakeContext = CreateFakeContext("msg-001", "corr-001");

		_options = new ContextObservabilityOptions
		{
			CaptureCustomItems = true,
		};

		_sut = new ContextFlowTracker(
			NullLogger<ContextFlowTracker>.Instance,
			Microsoft.Extensions.Options.Options.Create(_options),
			_fakeMetrics);
	}

	[Fact]
	public void ThrowOnNullLogger()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ContextFlowTracker(
				null!,
				Microsoft.Extensions.Options.Options.Create(_options),
				_fakeMetrics));
	}

	[Fact]
	public void ThrowOnNullOptions()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ContextFlowTracker(
				NullLogger<ContextFlowTracker>.Instance,
				null!,
				_fakeMetrics));
	}

	[Fact]
	public void ThrowOnNullMetrics()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ContextFlowTracker(
				NullLogger<ContextFlowTracker>.Instance,
				Microsoft.Extensions.Options.Options.Create(_options),
				null!));
	}

#pragma warning disable IL2026, IL3050
	[Fact]
	public void RecordContextStateSuccessfully()
	{
		// Act
		_sut.RecordContextState(_fakeContext, "Validation");

		// Assert
		A.CallTo(() => _fakeMetrics.RecordContextSnapshot("Validation", A<int>._, A<int>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void HandleNullContextInRecordContextState()
	{
		// Act - should not throw, just log a warning
		_sut.RecordContextState(null!, "Validation");

		// Assert - no metrics recorded
		A.CallTo(() => _fakeMetrics.RecordContextSnapshot(A<string>._, A<int>._, A<int>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public void GetMessageSnapshotsAfterRecording()
	{
		// Arrange
		_sut.RecordContextState(_fakeContext, "Validation");
		_sut.RecordContextState(_fakeContext, "Authorization");

		// Act
		var snapshots = _sut.GetMessageSnapshots("msg-001").ToList();

		// Assert
		snapshots.Count.ShouldBeGreaterThanOrEqualTo(2);
		snapshots.All(s => s.MessageId == "msg-001").ShouldBeTrue();
	}

	[Fact]
	public void ReturnEmptySnapshotsForUnknownMessage()
	{
		// Act
		var snapshots = _sut.GetMessageSnapshots("unknown-msg").ToList();

		// Assert
		snapshots.ShouldBeEmpty();
	}

	[Fact]
	public void CorrelateAcrossBoundarySuccessfully()
	{
		// Act
		_sut.CorrelateAcrossBoundary(_fakeContext, "OrderService");

		// Assert
		A.CallTo(() => _fakeMetrics.RecordCrossBoundaryTransition("OrderService", true))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void ThrowOnNullContextInCorrelateAcrossBoundary()
	{
		Should.Throw<ArgumentNullException>(() =>
			_sut.CorrelateAcrossBoundary(null!, "OrderService"));
	}

	[Fact]
	public void ThrowOnNullServiceBoundaryInCorrelateAcrossBoundary()
	{
		Should.Throw<ArgumentNullException>(() =>
			_sut.CorrelateAcrossBoundary(_fakeContext, null!));
	}

	[Fact]
	public void GetContextLineageAfterCorrelation()
	{
		// Arrange
		_sut.CorrelateAcrossBoundary(_fakeContext, "OrderService");

		// Act
		var lineage = _sut.GetContextLineage("corr-001");

		// Assert
		lineage.ShouldNotBeNull();
		lineage.CorrelationId.ShouldBe("corr-001");
		lineage.ServiceBoundaries.Count.ShouldBe(1);
		lineage.ServiceBoundaries[0].ServiceName.ShouldBe("OrderService");
		lineage.ServiceBoundaries[0].ContextPreserved.ShouldBeTrue();
	}

	[Fact]
	public void ReturnNullLineageForUnknownCorrelation()
	{
		var lineage = _sut.GetContextLineage("unknown-corr");
		lineage.ShouldBeNull();
	}

	[Fact]
	public void ValidateContextIntegrityWithRequiredFields()
	{
		// Arrange - context with all default required fields
		var items = new Dictionary<string, object>
		{
			["__MessageType"] = "TestMessage",
		};
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.MessageId).Returns("msg-001");
		A.CallTo(() => context.CorrelationId).Returns("corr-001");
		A.CallTo(() => context.Items).Returns(items);

		// Act
		var result = _sut.ValidateContextIntegrity(context);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void FailIntegrityCheckWhenMessageIdMissing()
	{
		// Arrange
		var items = new Dictionary<string, object>
		{
			["__MessageType"] = "TestMessage",
		};
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.MessageId).Returns((string?)null);
		A.CallTo(() => context.CorrelationId).Returns("corr-001");
		A.CallTo(() => context.Items).Returns(items);

		// Act
		var result = _sut.ValidateContextIntegrity(context);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ThrowOnNullContextInValidateIntegrity()
	{
		Should.Throw<ArgumentNullException>(() =>
			_sut.ValidateContextIntegrity(null!));
	}

	[Fact]
	public void DetectChangesThrowsOnNullContext()
	{
		Should.Throw<ArgumentNullException>(() =>
			_sut.DetectChanges(null!, "stage1", "stage2"));
	}

	[Fact]
	public void DetectChangesThrowsOnNullFromStage()
	{
		Should.Throw<ArgumentNullException>(() =>
			_sut.DetectChanges(_fakeContext, null!, "stage2"));
	}

	[Fact]
	public void DetectChangesThrowsOnNullToStage()
	{
		Should.Throw<ArgumentNullException>(() =>
			_sut.DetectChanges(_fakeContext, "stage1", null!));
	}
#pragma warning restore IL2026, IL3050

	[Fact]
	public async Task DisposeAsyncReleasesResources()
	{
		// Act
		await _sut.DisposeAsync();

		// Assert - no exception, resources cleaned up
		_sut.GetContextLineage("any").ShouldBeNull();
	}

	[Fact]
	public void DisposeSyncReleasesResources()
	{
		// Act
		_sut.Dispose();

		// Assert - no exception
		_sut.GetContextLineage("any").ShouldBeNull();
	}

	[Fact]
	public void DoubleDisposeDoesNotThrow()
	{
		_sut.Dispose();
		_sut.Dispose(); // Second dispose should not throw
	}

	public async ValueTask DisposeAsync()
	{
		await _sut.DisposeAsync();
	}

	private static IMessageContext CreateFakeContext(string messageId, string correlationId)
	{
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.MessageId).Returns(messageId);
		A.CallTo(() => context.CorrelationId).Returns(correlationId);
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());
		return context;
	}
}
