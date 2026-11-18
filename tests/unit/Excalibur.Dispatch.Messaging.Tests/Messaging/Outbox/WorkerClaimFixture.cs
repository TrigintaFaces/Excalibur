// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Runtime.CompilerServices;

using Excalibur.Dispatch;

namespace Excalibur.Dispatch.Tests.Messaging.Outbox;

/// <summary>Explicit claim-capable fakes for positive publisher worker fixtures.</summary>
internal static class WorkerClaimFixture
{
	private static readonly ConditionalWeakTable<IOutboxStore, Dictionary<string, string>> s_claims = new();

	internal static IOutboxStore CreateStore(Action<FakeItEasy.Creation.IFakeOptions<IOutboxStore>>? configure = null)
	{
		var store = A.Fake<IOutboxStore>(options =>
		{
			options.Implements<IClaimScopedOutboxStore>();
			configure?.Invoke(options);
		}).WithHonestCapabilities();
		var claims = s_claims.GetValue(store, _ => new Dictionary<string, string>(StringComparer.Ordinal));
		var typed = (IClaimScopedOutboxStore)store;
		A.CallTo(() => typed.MarkSentAsync(A<string>._, A<string>._, A<CancellationToken>._))
			.ReturnsLazily((string id, string claim, CancellationToken token) => Complete(claims, id, claim, token));
		A.CallTo(() => typed.MarkFailedAsync(A<string>._, A<string>._, A<int>._, A<string>._, A<CancellationToken>._))
			.ReturnsLazily((string id, string _, int _, string claim, CancellationToken token) => Complete(claims, id, claim, token));
		A.CallTo(() => typed.MarkFailedWithBackoffAsync(A<string>._, A<string>._, A<int>._, A<DateTimeOffset>._, A<string>._, A<CancellationToken>._))
			.ReturnsLazily((string id, string _, int _, DateTimeOffset _, string claim, CancellationToken token) => Complete(claims, id, claim, token));
		return store;
	}

	// Only the original stamped routing oracle may register a prescribed identity, once per store.
	private static readonly ConditionalWeakTable<IOutboxStore, object> s_stampedAcquisitions = new();

	internal static IEnumerable<OutboundMessage> Acquire(IOutboxStore store, IEnumerable<OutboundMessage> rows)
		=> AcquireCore(store, rows, null);

	internal static IEnumerable<OutboundMessage> AcquireStampedForRouting(IOutboxStore store, OutboundMessage row, string claim)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(claim);
		s_stampedAcquisitions.Add(store, new object());
		return AcquireCore(store, [row], claim);
	}

	private static IEnumerable<OutboundMessage> AcquireCore(IOutboxStore store, IEnumerable<OutboundMessage> rows, string? stampedClaim)
	{
		var claims = s_claims.GetValue(store, _ => new Dictionary<string, string>(StringComparer.Ordinal));
		var acquired = new List<OutboundMessage>();
		lock (claims)
		{
			foreach (var row in rows)
			{
				var identity = stampedClaim ?? Guid.NewGuid().ToString("N");
				var snapshot = Snapshot(row, identity);
				claims[row.Id] = identity;
				acquired.Add(snapshot);
			}
		}
		return acquired;
	}

	private static OutboundMessage Snapshot(OutboundMessage row, string identity)
	{
		var snapshot = new OutboundMessage
		{
			Id = row.Id,
			MessageType = row.MessageType,
			Payload = row.Payload,
			Destination = row.Destination,
			CreatedAt = row.CreatedAt,
			ScheduledAt = row.ScheduledAt,
			SentAt = row.SentAt,
			Status = row.Status,
			RetryCount = row.RetryCount,
			LastError = row.LastError,
			LastAttemptAt = row.LastAttemptAt,
			CorrelationId = row.CorrelationId,
			CausationId = row.CausationId,
			TenantId = row.TenantId,
			Priority = row.Priority,
			PartitionKey = row.PartitionKey,
			GroupKey = row.GroupKey,
			SequenceNumber = row.SequenceNumber,
			TargetTransports = row.TargetTransports,
			IsMultiTransport = row.IsMultiTransport,
			Headers = row.Headers,
			DispatcherId = identity,
		};
		// Preserve existing per-transport fixture state; only the acquisition envelope is independent.
		foreach (var delivery in row.TransportDeliveries)
		{
			snapshot.TransportDeliveries.Add(delivery);
		}
		return snapshot;
	}

	internal static void AssertNoWeakWrites(IOutboxStore store)
	{
		A.CallTo(() => store.MarkSentAsync(A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
		A.CallTo(() => store.MarkFailedAsync(A<string>._, A<string>._, A<int>._, A<CancellationToken>._)).MustNotHaveHappened();
	}

	private static ValueTask<OutboxCompletionOutcome> Complete(Dictionary<string, string> claims, string id, string claim, CancellationToken token)
	{
		token.ThrowIfCancellationRequested();
		lock (claims)
		{
			if (!claims.TryGetValue(id, out var current) || !string.Equals(current, claim, StringComparison.Ordinal))
			{
				return ValueTask.FromResult(OutboxCompletionOutcome.ClaimLost);
			}
			claims.Remove(id);
			return ValueTask.FromResult(OutboxCompletionOutcome.Applied);
		}
	}
}
