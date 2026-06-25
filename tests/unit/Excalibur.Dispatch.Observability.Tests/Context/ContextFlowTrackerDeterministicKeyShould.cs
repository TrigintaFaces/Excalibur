// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Observability.Context;

using Microsoft.Extensions.Logging.Abstractions;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Observability.Tests.Context;

/// <summary>
/// Regression tests for ContextFlowTracker deterministic snapshot key (Sprint 687, T.6 l2gjr).
/// Validates that GenerateSnapshotKey no longer includes UtcNow.Ticks, making repeated calls
/// with the same message+stage produce the same key.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Context")]
public sealed class ContextFlowTrackerDeterministicKeyShould : IAsyncDisposable
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
	public void RecordAndDetect_UseDeterministicKeys()
	{
		// Arrange -- a real field change (CorrelationId) between the two stages so that a
		// successful key lookup MUST surface a non-empty change set. If GenerateSnapshotKey
		// still included UtcNow.Ticks (the pre-fix bug), DetectChanges' record-time vs
		// detect-time keys would diverge, both TryGetValue lookups would miss, and the method
		// would silently return an EMPTY (but non-null) list -> ShouldNotBeEmpty goes RED.
		_tracker = CreateTracker();
		var context = CreateMessageContext("msg-123");

		// Act -- record at two stages, mutating an observed field between them
		A.CallTo(() => context.CorrelationId).Returns("corr-before");
		_tracker.RecordContextState(context, "BeforeHandler");

		A.CallTo(() => context.CorrelationId).Returns("corr-after");
		_tracker.RecordContextState(context, "AfterHandler");

		var changes = _tracker.DetectChanges(context, "BeforeHandler", "AfterHandler").ToList();

		// Assert -- the deterministic keys matched, so the CorrelationId modification is found
		changes.ShouldNotBeEmpty();
		var change = changes.Single(c => string.Equals(c.FieldName, "CorrelationId", StringComparison.Ordinal));
		change.ChangeType.ShouldBe(ContextChangeType.Modified);
		change.FromValue.ShouldBe("corr-before");
		change.ToValue.ShouldBe("corr-after");
	}

	[Fact]
	public void RecordContextState_OverwritesSnapshot_ForSameMessageAndStage()
	{
		// Arrange
		_tracker = CreateTracker();
		var context = CreateMessageContext("overwrite-msg");

		// Act -- record the same message+stage twice. With deterministic keys both calls map
		// to the SAME key and AddOrUpdate overwrites. With the pre-fix Ticks-bearing key the
		// two calls would land on distinct keys -> two snapshots -> Count == 2 (RED).
		_tracker.RecordContextState(context, "TestStage");
		_tracker.RecordContextState(context, "TestStage");

		// Assert -- exactly one snapshot survives for this message+stage
		var snapshots = _tracker.GetMessageSnapshots("overwrite-msg").ToList();
		snapshots.Count.ShouldBe(1);
	}

	[Fact]
	public void DetectChanges_FindsModifiedField_AcrossStages()
	{
		// Arrange -- the core regression test (deterministic, no wall-clock dependency):
		// Before T.6 the key included UtcNow.Ticks, so RecordContextState at time T1 stored
		// "msg:stage:T1" while DetectChanges at time T2 looked up "msg:stage:T2", which never
		// existed -> the change set was silently empty. With deterministic keys the lookup
		// succeeds and the genuine field change is reported.
		_tracker = CreateTracker();
		var context = CreateMessageContext("timing-test");

		// Record at "Pre" stage, then mutate an observed field and record at "Post" stage
		A.CallTo(() => context.CorrelationId).Returns("pre-value");
		_tracker.RecordContextState(context, "Pre");

		A.CallTo(() => context.CorrelationId).Returns("post-value");
		_tracker.RecordContextState(context, "Post");

		// Act
		var changes = _tracker.DetectChanges(context, "Pre", "Post").ToList();

		// Assert -- the modification is detected because the keys are deterministic
		changes.ShouldNotBeEmpty();
		var change = changes.Single(c => string.Equals(c.FieldName, "CorrelationId", StringComparison.Ordinal));
		change.ChangeType.ShouldBe(ContextChangeType.Modified);
		change.FromValue.ShouldBe("pre-value");
		change.ToValue.ShouldBe("post-value");
	}

	private static ContextFlowTracker CreateTracker()
	{
		return new ContextFlowTracker(
			NullLogger<ContextFlowTracker>.Instance,
			MsOptions.Create(new ContextObservabilityOptions()),
			A.Fake<IContextFlowMetrics>());
	}

	private static IMessageContext CreateMessageContext(string messageId)
	{
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.MessageId).Returns(messageId);
		return context;
	}
}
