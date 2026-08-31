// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

namespace Excalibur.Data.Tests.InboxProtocol;

/// <summary>
/// Records which idempotency protocol the caller reached for, so a fact can assert the branch taken
/// rather than the outcome that branch happened to produce.
/// </summary>
internal abstract class ProtocolRecordingStore : IInboxStore, IProcessingTrackingInboxStore
{
	public bool LeaseAttempted { get; protected set; }

	public ValueTask<InboxEntry> CreateEntryAsync(
		string messageId,
		string handlerType,
		string messageType,
		byte[] payload,
		IDictionary<string, object> metadata,
		CancellationToken cancellationToken) =>
		new(new InboxEntry
		{
			MessageId = messageId,
			HandlerType = handlerType,
			MessageType = messageType,
			Status = InboxStatus.Received,
		});

	public ValueTask<InboxEntry?> GetEntryAsync(string messageId, string handlerType, CancellationToken cancellationToken) =>
		new((InboxEntry?)null);

	public ValueTask<bool> IsProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken) =>
		new(false);

	public ValueTask<bool> TryMarkAsProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken) =>
		new(true);

	public ValueTask MarkProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken) =>
		ValueTask.CompletedTask;

	public ValueTask MarkFailedAsync(string messageId, string handlerType, string errorMessage, CancellationToken cancellationToken) =>
		ValueTask.CompletedTask;

	public ValueTask MarkProcessingAsync(string messageId, string handlerType, CancellationToken cancellationToken) =>
		ValueTask.CompletedTask;
}

/// <summary>
/// Models the three shipped stores that offer the caller-governed claim and no lease.
/// </summary>
internal sealed class ClaimOnlyStore : ProtocolRecordingStore, IClaimableInboxStore
{
	public ValueTask<bool> TryClaimAsync(string messageId, string handlerType, CancellationToken cancellationToken) =>
		new(true);

	public ValueTask ReleaseAsync(string messageId, string handlerType, CancellationToken cancellationToken) =>
		ValueTask.CompletedTask;
}

/// <summary>
/// Declares the lease interface — as a decorator must, so it can forward — while reporting whether the
/// capability is effectively available. The two are deliberately separable here: that separation is the
/// condition a type check cannot see and the capability probe can.
/// </summary>
internal sealed class LeaseDeclaringStore(bool effectivelyLeased)
	: ProtocolRecordingStore, IClaimableInboxStore, ILeasedInboxStore, IInboxStoreCapabilities
{
	public bool SupportsClaim => true;

	public bool SupportsLeasedClaim { get; } = effectivelyLeased;

	public bool SupportsProcessingTracking => true;

	public bool SupportsTransactional => false;

public bool SupportsScopedTransactional => false;

	public bool SupportsBackoffScheduling => false;

	public ValueTask<LeaseToken?> TryAcquireLeaseAsync(
		string messageId,
		string handlerType,
		TimeSpan leaseDuration,
		CancellationToken cancellationToken)
	{
		LeaseAttempted = true;
		return new ValueTask<LeaseToken?>(new LeaseToken("test-lease"));
	}

	public ValueTask<bool> CompleteAsync(string messageId, string handlerType, LeaseToken lease, CancellationToken cancellationToken) =>
		new(true);

	public ValueTask<bool> FailAsync(string messageId, string handlerType, LeaseToken lease, string errorMessage, CancellationToken cancellationToken) =>
		new(true);

	public ValueTask<bool> TryClaimAsync(string messageId, string handlerType, CancellationToken cancellationToken) =>
		new(true);

	public ValueTask ReleaseAsync(string messageId, string handlerType, CancellationToken cancellationToken) =>
		ValueTask.CompletedTask;
}
