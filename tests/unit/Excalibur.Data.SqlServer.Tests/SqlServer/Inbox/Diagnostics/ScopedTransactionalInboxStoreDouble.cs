// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

namespace Excalibur.Data.SqlServer.Tests.SqlServer.Inbox.Diagnostics;

/// <summary>
/// The durable substrate shared between store instances, so state written by one instance is readable from a
/// FRESH instance — the stand-in for "read it back on a new client" in a container-backed lock.
/// </summary>
internal sealed class InboxBacking
{
	public HashSet<string> ProcessedKeys { get; } = new(StringComparer.Ordinal);

	public HashSet<string> CommittedEffects { get; } = new(StringComparer.Ordinal);
}

/// <summary>The provider-native scope handed to the handler; buffers the handler's enlisted writes.</summary>
internal sealed class TestInboxTransactionScope : IInboxTransactionScope
{
	public List<string> Pending { get; } = [];

	public void Enlist(string marker) => Pending.Add(marker);
}

/// <summary>
/// A document-store-shaped inbox store implementing <see cref="IScopedTransactionalInboxStore"/> DIRECTLY —
/// only the interfaces appear in its base list and the transactional member is supplied by hand, so a lock
/// built on it binds the INTERFACE contract rather than re-testing an inherited first-party base.
/// </summary>
/// <remarks>
/// It deliberately does NOT implement <see cref="IInboxStoreCapabilities"/>, <see cref="IClaimableInboxStore"/>
/// or <see cref="ITransactionalInboxStore"/> — that is the real Cosmos/Mongo shape, and it is exactly the shape
/// a decorator's capability report must recognise. The transaction is genuine within the substrate: the mark
/// and every enlisted write commit together on success and are discarded together on failure.
/// </remarks>
internal sealed class ScopedTransactionalInboxStoreDouble : IInboxStore, IScopedTransactionalInboxStore
{
	private readonly InboxBacking _backing;

	public ScopedTransactionalInboxStoreDouble(InboxBacking backing) => _backing = backing;

	private static string Key(string messageId, string handlerType) => $"{messageId}::{handlerType}";

	public async ValueTask<bool> TryProcessTransactionallyAsync(
		string messageId,
		string handlerType,
		Func<IInboxTransactionScope, CancellationToken, ValueTask> handler,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(messageId);
		ArgumentException.ThrowIfNullOrEmpty(handlerType);
		ArgumentNullException.ThrowIfNull(handler);

		if (_backing.ProcessedKeys.Contains(Key(messageId, handlerType)))
		{
			return false;
		}

		var scope = new TestInboxTransactionScope();

		// Nothing is visible outside the transaction until it commits: the handler's enlisted writes and the
		// processed-mark are applied to the shared substrate together, or not at all.
		await handler(scope, cancellationToken).ConfigureAwait(false);

		_ = _backing.ProcessedKeys.Add(Key(messageId, handlerType));
		foreach (var marker in scope.Pending)
		{
			_ = _backing.CommittedEffects.Add(marker);
		}

		return true;
	}

	public ValueTask<bool> IsProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken) =>
		ValueTask.FromResult(_backing.ProcessedKeys.Contains(Key(messageId, handlerType)));

	public ValueTask MarkProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		_ = _backing.ProcessedKeys.Add(Key(messageId, handlerType));
		return ValueTask.CompletedTask;
	}

	public ValueTask<bool> TryMarkAsProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken) =>
		ValueTask.FromResult(_backing.ProcessedKeys.Add(Key(messageId, handlerType)));

	public ValueTask<InboxEntry> CreateEntryAsync(
		string messageId,
		string handlerType,
		string messageType,
		byte[] payload,
		IDictionary<string, object> metadata,
		CancellationToken cancellationToken) =>
		ValueTask.FromResult(new InboxEntry
		{
			MessageId = messageId,
			HandlerType = handlerType,
			MessageType = messageType,
			Payload = payload,
			Status = InboxStatus.Received,
		});

	public ValueTask<InboxEntry?> GetEntryAsync(string messageId, string handlerType, CancellationToken cancellationToken) =>
		ValueTask.FromResult<InboxEntry?>(null);

	public ValueTask MarkFailedAsync(string messageId, string handlerType, string errorMessage, CancellationToken cancellationToken) =>
		ValueTask.CompletedTask;
}
