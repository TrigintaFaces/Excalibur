// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Observability.Context;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Observability.Tests.Context;

/// <summary>
/// Functional tests for <see cref="ContextFlowTracker"/> verifying context tracking, lineage, and integrity validation.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "ContextFlow")]
[SuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = "Test code only")]
[SuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Test code only")]
public sealed class ContextFlowTrackerFunctionalShould : IAsyncDisposable
{
	private readonly ContextFlowTracker _tracker;
	private readonly IContextFlowMetrics _fakeMetrics;

	public ContextFlowTrackerFunctionalShould()
	{
		_fakeMetrics = A.Fake<IContextFlowMetrics>();
		_tracker = new ContextFlowTracker(
			NullLogger<ContextFlowTracker>.Instance,
			Microsoft.Extensions.Options.Options.Create(new ContextObservabilityOptions()),
			_fakeMetrics);
	}

	public async ValueTask DisposeAsync() => await _tracker.DisposeAsync();

	[Fact]
	public void ThrowOnNullLogger()
	{
		Should.Throw<ArgumentNullException>(() => new ContextFlowTracker(
			null!,
			Microsoft.Extensions.Options.Options.Create(new ContextObservabilityOptions()),
			A.Fake<IContextFlowMetrics>()));
	}

	[Fact]
	public void ThrowOnNullOptions()
	{
		Should.Throw<ArgumentNullException>(() => new ContextFlowTracker(
			NullLogger<ContextFlowTracker>.Instance,
			null!,
			A.Fake<IContextFlowMetrics>()));
	}

	[Fact]
	public void ThrowOnNullMetrics()
	{
		Should.Throw<ArgumentNullException>(() => new ContextFlowTracker(
			NullLogger<ContextFlowTracker>.Instance,
			Microsoft.Extensions.Options.Options.Create(new ContextObservabilityOptions()),
			null!));
	}

	[Fact]
	public void RecordContextState_AndPublishMetrics()
	{
		var context = CreateFakeContext("msg-1", "corr-1");

		_tracker.RecordContextState(context, "PreProcessing");

		A.CallTo(() => _fakeMetrics.RecordContextSnapshot("PreProcessing", A<int>._, A<int>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void HandleNullContext_Gracefully()
	{
		// Should not throw
		_tracker.RecordContextState(null!, "PreProcessing");

		// Metrics should not have been called
		A.CallTo(() => _fakeMetrics.RecordContextSnapshot(A<string>._, A<int>._, A<int>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public void TrackLineage_AcrossMultipleStages()
	{
		var context = CreateFakeContext("msg-1", "corr-1");

		_tracker.RecordContextState(context, "Start");
		_tracker.RecordContextState(context, "Middleware");
		_tracker.RecordContextState(context, "Handler");

		var lineage = _tracker.GetContextLineage("corr-1");
		lineage.ShouldNotBeNull();
		lineage.CorrelationId.ShouldBe("corr-1");
		lineage.Snapshots.Count.ShouldBeGreaterThanOrEqualTo(3);
	}

	[Fact]
	public void ReturnNull_ForUnknownLineage()
	{
		_tracker.GetContextLineage("nonexistent-correlation").ShouldBeNull();
	}

	[Fact]
	public void GetMessageSnapshots()
	{
		var context = CreateFakeContext("msg-2", "corr-2");

		_tracker.RecordContextState(context, "Stage1");
		_tracker.RecordContextState(context, "Stage2");

		var snapshots = _tracker.GetMessageSnapshots("msg-2").ToList();
		snapshots.Count.ShouldBeGreaterThanOrEqualTo(2);
		snapshots.ShouldAllBe(s => s.MessageId == "msg-2");
	}

	[Fact]
	public void ReturnEmpty_ForUnknownMessageSnapshots()
	{
		_tracker.GetMessageSnapshots("nonexistent").ShouldBeEmpty();
	}

	[Fact]
	public void CorrelateAcrossBoundary()
	{
		var context = CreateFakeContext("msg-3", "corr-3");

		_tracker.CorrelateAcrossBoundary(context, "PaymentService");

		var lineage = _tracker.GetContextLineage("corr-3");
		lineage.ShouldNotBeNull();
		lineage.ServiceBoundaries.ShouldContain(b => b.ServiceName == "PaymentService" && b.ContextPreserved);

		A.CallTo(() => _fakeMetrics.RecordCrossBoundaryTransition("PaymentService", true))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void ThrowOnNullContext_ForCorrelation()
	{
		Should.Throw<ArgumentNullException>(() => _tracker.CorrelateAcrossBoundary(null!, "Service"));
	}

	[Fact]
	public void ThrowOnNullServiceBoundary_ForCorrelation()
	{
		var context = CreateFakeContext("msg-4", "corr-4");
		Should.Throw<ArgumentNullException>(() => _tracker.CorrelateAcrossBoundary(context, null!));
	}

	[Fact]
	public void ValidateContextIntegrity_PassesWithAllRequiredFields()
	{
		var context = CreateFakeContext("msg-5", "corr-5", messageType: "OrderCreated");

		var isValid = _tracker.ValidateContextIntegrity(context);

		isValid.ShouldBeTrue();
	}

	[Fact]
	public void ValidateContextIntegrity_FailsWhenMessageIdMissing()
	{
		var context = CreateFakeContext(null, "corr-6", messageType: "OrderCreated");

		var isValid = _tracker.ValidateContextIntegrity(context);

		isValid.ShouldBeFalse();
	}

	[Fact]
	public void ValidateContextIntegrity_FailsWhenCorrelationIdMissing()
	{
		var context = CreateFakeContext("msg-7", null, messageType: "OrderCreated");

		var isValid = _tracker.ValidateContextIntegrity(context);

		isValid.ShouldBeFalse();
	}

	[Fact]
	public void ValidateContextIntegrity_ThrowsOnNull()
	{
		Should.Throw<ArgumentNullException>(() => _tracker.ValidateContextIntegrity(null!));
	}

	[Fact]
	public void DetectChanges_ThrowsOnNullContext()
	{
		Should.Throw<ArgumentNullException>(() => _tracker.DetectChanges(null!, "from", "to").ToList());
	}

	[Fact]
	public void DetectChanges_ThrowsOnNullFromStage()
	{
		var context = CreateFakeContext("msg", "corr");
		Should.Throw<ArgumentNullException>(() => _tracker.DetectChanges(context, null!, "to").ToList());
	}

	[Fact]
	public void DetectChanges_ThrowsOnNullToStage()
	{
		var context = CreateFakeContext("msg", "corr");
		Should.Throw<ArgumentNullException>(() => _tracker.DetectChanges(context, "from", null!).ToList());
	}

	[Fact]
	public async Task DisposeAsync_ClearsResources()
	{
		var metrics = A.Fake<IContextFlowMetrics>();
		var tracker = new ContextFlowTracker(
			NullLogger<ContextFlowTracker>.Instance,
			Microsoft.Extensions.Options.Options.Create(new ContextObservabilityOptions()),
			metrics);

		var context = CreateFakeContext("msg-dispose", "corr-dispose");
		tracker.RecordContextState(context, "Stage1");

		await tracker.DisposeAsync();

		// After disposal, snapshots should be cleared
		tracker.GetMessageSnapshots("msg-dispose").ShouldBeEmpty();
	}

	[Fact]
	public void Dispose_ClearsResources()
	{
		var metrics = A.Fake<IContextFlowMetrics>();
		var tracker = new ContextFlowTracker(
			NullLogger<ContextFlowTracker>.Instance,
			Microsoft.Extensions.Options.Options.Create(new ContextObservabilityOptions()),
			metrics);

		var context = CreateFakeContext("msg-disp", "corr-disp");
		tracker.RecordContextState(context, "Stage1");

		tracker.Dispose();

		tracker.GetMessageSnapshots("msg-disp").ShouldBeEmpty();
	}

	private static IMessageContext CreateFakeContext(
		string? messageId = "msg-default",
		string? correlationId = "corr-default",
		string? messageType = "TestMessage")
	{
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.MessageId).Returns(messageId);
		A.CallTo(() => context.CorrelationId).Returns(correlationId);
		A.CallTo(() => context.MessageType).Returns(messageType);
		A.CallTo(() => context.ExternalId).Returns(null);
		A.CallTo(() => context.UserId).Returns(null);
		A.CallTo(() => context.CausationId).Returns(null);
		A.CallTo(() => context.TraceParent).Returns(null);
		A.CallTo(() => context.TenantId).Returns(null);
		A.CallTo(() => context.Source).Returns(null);
		A.CallTo(() => context.ContentType).Returns(null);
		A.CallTo(() => context.DeliveryCount).Returns(1);
		A.CallTo(() => context.ReceivedTimestampUtc).Returns(DateTimeOffset.UtcNow);
		A.CallTo(() => context.SentTimestampUtc).Returns(null);
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());
		return context;
	}
}
